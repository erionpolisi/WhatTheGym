using Gym.Domain.Common;

namespace Gym.CatalogCli;

internal sealed class CatalogService(CatalogDocument catalog)
{
    public CatalogDocument Catalog { get; } = catalog;

    public CatalogChain AddChain(string name, string? website)
    {
        var normalizedName = RequireText(name, "Name");
        var normalizedWebsite = NormalizeWebsite(website);
        EnsureUniqueChainSlug(Slug.Generate(normalizedName), except: null);

        var chain = new CatalogChain
        {
            Name = normalizedName,
            Website = normalizedWebsite,
        };
        Catalog.Chains.Add(chain);
        return chain;
    }

    public void UpdateChain(CatalogChain chain, string name, string? website)
    {
        var normalizedName = RequireText(name, "Name");
        var normalizedWebsite = NormalizeWebsite(website);
        var oldSlug = Slug.Generate(chain.Name);
        var newSlug = Slug.Generate(normalizedName);
        EnsureUniqueChainSlug(newSlug, chain);

        if (!string.Equals(chain.Website, normalizedWebsite, StringComparison.Ordinal))
        {
            chain.WebsiteVerifiedOn = null;
        }

        chain.Name = normalizedName;
        chain.Website = normalizedWebsite;

        if (!string.Equals(oldSlug, newSlug, StringComparison.Ordinal))
        {
            foreach (var gym in Catalog.ViennaGyms.Where(gym =>
                         string.Equals(gym.ChainSlug, oldSlug, StringComparison.Ordinal)))
            {
                gym.ChainSlug = newSlug;
            }
        }
    }

    public CatalogGym AddGym(
        CatalogChain chain,
        string name,
        int district,
        string address,
        string postalCode)
    {
        var normalizedName = RequireText(name, "Name");
        var normalizedAddress = RequireText(address, "Adresse");
        var normalizedPostalCode = RequireText(postalCode, "Postleitzahl");
        EnsureUniqueGymSlug(Slug.Generate(normalizedName), except: null);
        ValidateDistrictAndPostalCode(district, normalizedPostalCode);

        var gym = new CatalogGym
        {
            Name = normalizedName,
            ChainSlug = Slug.Generate(chain.Name),
            District = district,
            Address = normalizedAddress,
            PostalCode = normalizedPostalCode,
        };
        Catalog.ViennaGyms.Add(gym);
        return gym;
    }

    public void UpdateGym(CatalogGym gym, string name, int district, string address, string postalCode)
    {
        var normalizedName = RequireText(name, "Name");
        var normalizedAddress = RequireText(address, "Adresse");
        var normalizedPostalCode = RequireText(postalCode, "Postleitzahl");
        EnsureUniqueGymSlug(Slug.Generate(normalizedName), gym);
        ValidateDistrictAndPostalCode(district, normalizedPostalCode);

        if (!string.Equals(gym.Address, normalizedAddress, StringComparison.Ordinal)
            || gym.District != district
            || !string.Equals(gym.PostalCode, normalizedPostalCode, StringComparison.Ordinal))
        {
            gym.LocationVerifiedOn = null;
        }

        gym.Name = normalizedName;
        gym.District = district;
        gym.Address = normalizedAddress;
        gym.PostalCode = normalizedPostalCode;
    }

    public static void ToggleTemporaryClosure(CatalogGym gym)
    {
        gym.TemporarilyClosed = gym.TemporarilyClosed == true ? null : true;
    }

    public IReadOnlyList<CatalogGym> GetGyms(CatalogChain chain) =>
        Catalog.ViennaGyms
            .Where(gym => string.Equals(gym.ChainSlug, Slug.Generate(chain.Name), StringComparison.Ordinal))
            .OrderBy(gym => gym.District)
            .ThenBy(gym => gym.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public VerificationSummary GetLocationVerificationSummary(CatalogChain chain, DateOnly today)
    {
        var gyms = GetGyms(chain);
        return new VerificationSummary(
            gyms.Count(gym => gym.LocationVerifiedOn is null),
            gyms.Count(gym => gym.LocationVerifiedOn is { } verifiedOn
                               && VerificationStatus.IsStale(verifiedOn, today)));
    }

    public static string CreateGoogleMapsUrl(CatalogGym gym)
    {
        var query = Uri.EscapeDataString($"{gym.Address}, {gym.PostalCode} Wien");
        return $"https://www.google.com/maps/search/?api=1&query={query}";
    }

    public static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} darf nicht leer sein.");
        }

        return value.Trim();
    }

    private static string? NormalizeWebsite(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return null;
        }

        var normalized = website.Trim();
        if (!IsAbsoluteHttpUrl(normalized))
        {
            throw new InvalidOperationException("Die Website muss eine absolute HTTP(S)-URL sein.");
        }

        return normalized;
    }

    private static void ValidateDistrictAndPostalCode(int district, string postalCode)
    {
        if (district is < 1 or > 23)
        {
            throw new InvalidOperationException("Der Bezirk muss zwischen 1 und 23 liegen.");
        }

        var expectedPostalCode = (1000 + (district * 10)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.Equals(postalCode, expectedPostalCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Die Postleitzahl muss für Bezirk {district} '{expectedPostalCode}' sein.");
        }
    }

    private void EnsureUniqueChainSlug(string slug, CatalogChain? except)
    {
        if (Catalog.Chains.Any(chain => !ReferenceEquals(chain, except)
                                        && string.Equals(Slug.Generate(chain.Name), slug, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Der Ketten-Slug '{slug}' existiert bereits.");
        }
    }

    private void EnsureUniqueGymSlug(string slug, CatalogGym? except)
    {
        if (Catalog.ViennaGyms.Any(gym => !ReferenceEquals(gym, except)
                                          && string.Equals(Slug.Generate(gym.Name), slug, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Der Standort-Slug '{slug}' existiert bereits.");
        }
    }
}
