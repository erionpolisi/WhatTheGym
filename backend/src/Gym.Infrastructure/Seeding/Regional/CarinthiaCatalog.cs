namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Carinthia (Kaernten) gym reference data. Researched 2026-08-31 from official chain sources.
/// NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class CarinthiaCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Klagenfurt Ost", "clever-fit", "Klagenfurt", "9020", "Voelkermarkter Strasse 242", null),
        new("clever fit Klagenfurt Zentrum", "clever-fit", "Klagenfurt", "9020", "Heiligengeistplatz 4", null),
        new("clever fit Spittal", "clever-fit", "Spittal an der Drau", "9800", "Neuer Platz 1", null),
        new("clever fit Villach", "clever-fit", "Villach", "9500", "Trattengasse 28", null),
        new("clever fit Wolfsberg", "clever-fit", "Wolfsberg", "9400", "Hermann-Fischer-Strasse 1", null),
        new("FitInn Klagenfurt Suedring", "fitinn", "Klagenfurt", "9020", "Suedring 211", null),
        new("FitInn Klagenfurt Schleppe Platz", "fitinn", "Klagenfurt", "9020", "", null),
        new("FitInn Villach V-Center", "fitinn", "Villach", "9500", "", null),
        new("MYGYM Hermagor", "mygym", "Hermagor", "9620", "Hauptstrasse 26", null),
        new("MYGYM Spittal", "mygym", "Spittal an der Drau", "9800", "Bahnhofstrasse 7", null),
    ];
}
