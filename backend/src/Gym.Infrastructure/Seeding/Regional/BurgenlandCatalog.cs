namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Burgenland gym reference data. Researched 2026-08-31 from official chain sources.
/// NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class BurgenlandCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("Fit Fabrik Plus Parndorf", "fit-fabrik", "Parndorf", "7111", "Gewerbestrasse 9", null),
        new("Mrs.Sporty Eisenstadt", "mrs-sporty", "Eisenstadt", "7000", "Josef-Reichl-Gasse 7", null),
    ];
}
