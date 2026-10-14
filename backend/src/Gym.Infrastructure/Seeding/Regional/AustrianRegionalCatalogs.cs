namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Aggregated view over all inert regional catalogs (everything except Vienna), keyed by
/// Bundesland display name. The editable mapping is embedded JSON and remains referenced by
/// nothing at runtime today (ADR 0011).
/// </summary>
public static class AustrianRegionalCatalogs
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<RegionalSeedGym>> ByBundesland =
        EmbeddedSeedData.Require(EmbeddedSeedData.Catalog.RegionalCatalogs, "catalog.regionalCatalogs");
}
