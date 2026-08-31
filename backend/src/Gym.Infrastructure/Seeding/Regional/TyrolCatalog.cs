namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Tyrol (Tirol, incl. Osttirol) gym reference data. Researched 2026-08-31 from official chain
/// sources. NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class TyrolCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Imst", "clever-fit", "Imst", "6460", "Industriezone 24", null),
        new("clever fit Innsbruck Rum", "clever-fit", "Rum", "6063", "Steinbockallee 29", null),
        new("FitInn Innsbruck EKZ West", "fitinn", "Innsbruck", "6020", "Hoettinger Au 73", null),
        new("FitInn Innsbruck Greif Center", "fitinn", "Innsbruck", "6020", "Andechsstrasse 85", null),
        new("FitInn Innsbruck Hunoldstrasse", "fitinn", "Innsbruck", "6020", "Hunoldstrasse 5", null),
        new("Mrs.Sporty Innsbruck Altstadt", "mrs-sporty", "Innsbruck", "6020", "Buergerstrasse 11", null),
        new("MYGYM Imst", "mygym", "Imst", "6460", "Langgasse 19", null),
        new("MYGYM Matrei", "mygym", "Matrei in Osttirol", "9971", "Alban-Bichler-Strasse 3", null),
    ];
}
