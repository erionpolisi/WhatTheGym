namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Salzburg gym reference data. Researched 2026-08-31 from official chain sources (MYGYM is a
/// Salzburg-based chain). NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class SalzburgCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
        EmbeddedSeedData.RequiredRegionalCatalog("Salzburg");
}
