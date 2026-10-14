namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Tyrol (Tirol, incl. Osttirol) gym reference data. Researched 2026-08-31 from official chain
/// sources. NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class TyrolCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
        EmbeddedSeedData.RequiredRegionalCatalog("Tirol");
}
