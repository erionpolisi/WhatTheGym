namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Styria (Steiermark) gym reference data. Researched 2026-08-31 from official chain sources.
/// NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class StyriaCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
        EmbeddedSeedData.RequiredRegionalCatalog("Steiermark");
}
