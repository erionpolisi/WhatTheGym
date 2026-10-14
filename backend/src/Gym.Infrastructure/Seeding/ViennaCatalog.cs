namespace Gym.Infrastructure.Seeding;

/// <summary>
/// Official Vienna gym catalogue used for local seeding. Editable data is embedded from JSON;
/// the verification ledger is in docs/seed-data.md.
/// </summary>
public static class ViennaCatalog
{
    public static readonly IReadOnlyList<SeedGym> Gyms =
        EmbeddedSeedData.Require(EmbeddedSeedData.Catalog.ViennaGyms, "catalog.viennaGyms");
}
