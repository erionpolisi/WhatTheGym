using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.CatalogCli;

internal sealed class CatalogFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    public CatalogDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        var catalog = JsonSerializer.Deserialize<CatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Die Datei '{path}' enthält keinen gültigen Katalog.");
        Validate(catalog);
        return catalog;
    }

    public void Save(string path, CatalogDocument catalog)
    {
        Validate(catalog);

        var content = Format(catalog);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal static void Validate(CatalogDocument catalog)
    {
        var chainSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in catalog.Chains)
        {
            if (string.IsNullOrWhiteSpace(chain.Name))
            {
                throw new InvalidOperationException("Eine Kette hat keinen Namen.");
            }

            if (!chainSlugs.Add(Slug.Generate(chain.Name)))
            {
                throw new InvalidOperationException($"Der Ketten-Slug für '{chain.Name}' ist nicht eindeutig.");
            }

            if (chain.Website is not null && !CatalogService.IsAbsoluteHttpUrl(chain.Website))
            {
                throw new InvalidOperationException($"Die Website von '{chain.Name}' ist keine gültige HTTP(S)-URL.");
            }

            if (chain.TrainingTypes.Count == 0
                || chain.TrainingTypes.Contains(TrainingType.None)
                || chain.TrainingTypes.Any(value => !Enum.IsDefined(value))
                || chain.TrainingTypes.Distinct().Count() != chain.TrainingTypes.Count)
            {
                throw new InvalidOperationException($"Die Trainingsarten von '{chain.Name}' fehlen oder sind nicht eindeutig.");
            }

            if (!Enum.IsDefined(chain.TargetAudience))
            {
                throw new InvalidOperationException($"Die Zielgruppe von '{chain.Name}' ist ungültig.");
            }
        }

        var gymSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gym in catalog.ViennaGyms)
        {
            if (string.IsNullOrWhiteSpace(gym.Name) || string.IsNullOrWhiteSpace(gym.Address))
            {
                throw new InvalidOperationException("Ein Wiener Standort hat keinen Namen oder keine Adresse.");
            }

            if (!gymSlugs.Add(Slug.Generate(gym.Name)))
            {
                throw new InvalidOperationException($"Der Standort-Slug für '{gym.Name}' ist nicht eindeutig.");
            }

            if (gym.District is < 1 or > 23)
            {
                throw new InvalidOperationException($"Der Bezirk von '{gym.Name}' muss zwischen 1 und 23 liegen.");
            }

            var expectedPostalCode = (1000 + (gym.District * 10)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(gym.PostalCode, expectedPostalCode, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Die Postleitzahl von '{gym.Name}' muss für Bezirk {gym.District} '{expectedPostalCode}' sein.");
            }

            if (gym.ChainSlug is not null && !chainSlugs.Contains(gym.ChainSlug))
            {
                throw new InvalidOperationException(
                    $"Der Standort '{gym.Name}' verweist auf die unbekannte Kette '{gym.ChainSlug}'.");
            }

            if (gym.Status is not null && gym.TemporarilyClosed is not null)
            {
                throw new InvalidOperationException(
                    $"Der Standort '{gym.Name}' darf nicht gleichzeitig Status und TemporarilyClosed setzen.");
            }

            if (gym.TrainingTypes is { } trainingTypes
                && (trainingTypes.Count == 0
                    || trainingTypes.Contains(TrainingType.None)
                    || trainingTypes.Any(value => !Enum.IsDefined(value))
                    || trainingTypes.Distinct().Count() != trainingTypes.Count))
            {
                throw new InvalidOperationException(
                    $"Die Trainingsarten von '{gym.Name}' sind leer oder ungültig.");
            }

            if (gym.TargetAudience is { } targetAudience && !Enum.IsDefined(targetAudience))
            {
                throw new InvalidOperationException($"Die Zielgruppe von '{gym.Name}' ist ungültig.");
            }
        }
    }

    private static string Format(CatalogDocument catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        AppendArray(builder, "Chains", catalog.Chains, trailingComma: true);
        AppendAmenities(builder, catalog.Amenities);
        AppendArray(builder, "ViennaGyms", catalog.ViennaGyms, trailingComma: true);
        builder.AppendLine("  \"RegionalCatalogs\": {");

        var regions = catalog.RegionalCatalogs.ToArray();
        for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
        {
            var region = regions[regionIndex];
            builder.Append("    ");
            builder.Append(JsonSerializer.Serialize(region.Key, JsonOptions));
            builder.AppendLine(": [");
            AppendItems(builder, region.Value, indent: "      ");
            builder.Append("    ]");
            builder.AppendLine(regionIndex < regions.Length - 1 ? "," : string.Empty);
        }

        builder.AppendLine("  }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendArray<T>(
        StringBuilder builder,
        string propertyName,
        IReadOnlyList<T> items,
        bool trailingComma)
    {
        builder.Append("  \"");
        builder.Append(propertyName);
        builder.AppendLine("\": [");
        AppendItems(builder, items, indent: "    ");
        builder.Append("  ]");
        builder.AppendLine(trailingComma ? "," : string.Empty);
    }

    private static void AppendItems<T>(StringBuilder builder, IReadOnlyList<T> items, string indent)
    {
        for (var index = 0; index < items.Count; index++)
        {
            builder.Append(indent);
            builder.Append(JsonSerializer.Serialize(items[index], JsonOptions));
            builder.AppendLine(index < items.Count - 1 ? "," : string.Empty);
        }
    }

    private static void AppendAmenities(StringBuilder builder, IReadOnlyList<string> amenities)
    {
        builder.AppendLine("  \"Amenities\": [");
        const int itemsPerLine = 6;
        for (var index = 0; index < amenities.Count; index += itemsPerLine)
        {
            var chunk = amenities.Skip(index).Take(itemsPerLine)
                .Select(item => JsonSerializer.Serialize(item, JsonOptions));
            builder.Append("    ");
            builder.Append(string.Join(", ", chunk));
            builder.AppendLine(index + itemsPerLine < amenities.Count ? "," : string.Empty);
        }

        builder.AppendLine("  ],");
    }
}
