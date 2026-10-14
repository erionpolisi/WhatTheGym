using System.Text.Json.Serialization;

namespace Gym.CatalogCli;

internal sealed class CatalogDocument
{
    public List<CatalogChain> Chains { get; init; } = [];

    public List<string> Amenities { get; init; } = [];

    public List<CatalogGym> ViennaGyms { get; init; } = [];

    public Dictionary<string, List<RegionalCatalogGym>> RegionalCatalogs { get; init; } = [];
}

internal sealed class CatalogChain
{
    public required string Name { get; set; }

    public string? Website { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? WebsiteVerifiedOn { get; set; }
}

internal sealed class CatalogGym
{
    public required string Name { get; set; }

    public string? ChainSlug { get; set; }

    public int District { get; set; }

    public required string Address { get; set; }

    public required string PostalCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? LocationVerifiedOn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TemporarilyClosed { get; set; }
}

internal sealed class RegionalCatalogGym
{
    public required string Name { get; init; }

    public string? ChainSlug { get; init; }

    public required string City { get; init; }

    public required string PostalCode { get; init; }

    public required string Address { get; init; }

    public string? Website { get; init; }
}
