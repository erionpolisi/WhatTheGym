namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Vorarlberg gym reference data. Researched 2026-08-31 from official chain sources.
/// NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class VorarlbergCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Bregenz", "clever-fit", "Bregenz", "6900", "Mariahilfstrasse 1", null),
        new("FitInn Bludenz Buers", "fitinn", "Buers", "6706", "Hauptstrasse 4", null),
        new("FitInn Dornbirn", "fitinn", "Dornbirn", "6850", "Schwefel 67", null),
        new("FitInn Feldkirch", "fitinn", "Feldkirch", "6800", "Koenigshofstrasse 57", null),
    ];
}
