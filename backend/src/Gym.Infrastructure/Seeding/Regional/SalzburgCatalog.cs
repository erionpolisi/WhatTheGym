namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Salzburg gym reference data. Researched 2026-08-31 from official chain sources (MYGYM is a
/// Salzburg-based chain). NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class SalzburgCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Salzburg Premium", "clever-fit", "Salzburg", "5020", "Fuerbergstrasse 18", null),
        new("FitInn Salzburg Hauptbahnhof", "fitinn", "Salzburg", "5020", "Rainerstrasse 30", null),
        new("John Harris Salzburg", "john-harris-fitness", "Salzburg", "5020", "Innsbrucker Bundesstrasse 35", null),
        new("MYGYM Bruck", "mygym", "Bruck an der Grossglocknerstrasse", "5671", "", null),
        new("MYGYM Eugendorf", "mygym", "Eugendorf", "5301", "Wiener Strasse 2-4", null),
        new("MYGYM Obertauern", "mygym", "Obertauern", "5562", "Ringstrasse 37", null),
        new("MYGYM Obertrum", "mygym", "Obertrum am See", "5162", "Jakobistrasse 13", null),
        new("MYGYM Saalfelden", "mygym", "Saalfelden", "5760", "Leopold-Luger-Strasse 1", null),
        new("MYGYM Salzburg Nord", "mygym", "Salzburg", "5020", "Itzlinger Hauptstrasse 93a", null),
        new("MYGYM Salzburg Wals", "mygym", "Wals bei Salzburg", "5071", "Josef-Lindner-Strasse 8a", null),
        new("MYGYM Salzburg ZIB", "mygym", "Salzburg", "5020", "Fuerbergstrasse 18-20", null),
        new("MYGYM St. Johann", "mygym", "St. Johann im Pongau", "5600", "Bundesstrasse 31", null),
        new("MYGYM St. Michael", "mygym", "St. Michael im Lungau", "5582", "Kaltbachstrasse 668", null),
        new("MYGYM Tamsweg", "mygym", "Tamsweg", "5580", "Woeltingerstrasse 9", null),
        new("MYGYM Unken", "mygym", "Unken", "5091", "", null),
        new("Mrs.Sporty St. Johann im Pongau", "mrs-sporty", "St. Johann im Pongau", "5600", "Hauptstrasse 81", null),
    ];
}
