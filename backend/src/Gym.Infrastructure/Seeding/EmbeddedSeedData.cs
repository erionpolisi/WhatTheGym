using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gym.Infrastructure.Seeding;

internal static class EmbeddedSeedData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    public static readonly CatalogSeedData Catalog = Load<CatalogSeedData>("Gym.Infrastructure.Seeding.Data.catalog.json");
    public static readonly LegalDocumentsSeedData LegalDocuments = Load<LegalDocumentsSeedData>("Gym.Infrastructure.Seeding.Data.legal-documents.json");
    public static readonly DemoSeedData Demo = Load<DemoSeedData>("Gym.Infrastructure.Seeding.Data.demo-data.json");

    private static T Load<T>(string resourceName)
    {
        var assembly = typeof(EmbeddedSeedData).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded seed data resource '{resourceName}' was not found.");

        try
        {
            return JsonSerializer.Deserialize<T>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Embedded seed data resource '{resourceName}' contains null data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Embedded seed data resource '{resourceName}' is malformed.", exception);
        }
    }

    public static T Require<T>(T? value, string dataName) where T : class =>
        value ?? throw new InvalidOperationException($"Embedded seed data '{dataName}' is missing or null.");

    public static IReadOnlyList<RegionalSeedGym> RequiredRegionalCatalog(string bundesland)
    {
        var catalogs = Require(Catalog.RegionalCatalogs, "catalog.regionalCatalogs");
        return catalogs.TryGetValue(bundesland, out var gyms)
            ? Require(gyms, $"catalog.regionalCatalogs.{bundesland}")
            : throw new InvalidOperationException($"Embedded seed data regional catalog '{bundesland}' is missing.");
    }
}
