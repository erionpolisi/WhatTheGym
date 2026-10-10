namespace Gym.Infrastructure.Seeding;

/// <summary>
/// Platform-wide seed vocabulary shared by every regional catalog. Its editable values are
/// embedded JSON resources; regional gym lists live in their own catalog classes.
/// </summary>
public static class SeedVocabulary
{
    /// <summary>Gym chains operating in Austria that seeded or reference data points to.</summary>
    public static readonly IReadOnlyList<(string Name, string? Website)> Chains =
        EmbeddedSeedData.Require(EmbeddedSeedData.Catalog.Chains, "catalog.chains")
            .Select(chain => (chain.Name, chain.Website))
            .ToArray();

    /// <summary>Amenity vocabulary offered to reviews/filters; German display names.</summary>
    public static readonly IReadOnlyList<string> Amenities =
        EmbeddedSeedData.Require(EmbeddedSeedData.Catalog.Amenities, "catalog.amenities");
}
