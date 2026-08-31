namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Lower Austria (Niederoesterreich) gym reference data. Researched 2026-08-31 from official
/// chain sources (clever-fit.com, fitinn.at, fitfabrik.at, mrssporty.at, mygym.at sitemaps and
/// studio pages). NOT seeded, NOT tested, NOT wired to the domain — inert data per ADR 0011.
/// Entries with an empty address had a confirmed branch but no machine-readable street address.
/// </summary>
public static class LowerAustriaCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Brunn am Gebirge", "clever-fit", "Brunn am Gebirge", "2345", "Wiener Strasse 131-133", null),
        new("clever fit Krems", "clever-fit", "Krems", "3500", "Utzstrasse 1", null),
        new("clever fit Melk", "clever-fit", "Melk", "3390", "Umfahrungsstrasse 1", null),
        new("clever fit Mistelbach", "clever-fit", "Mistelbach", "2130", "Museumgasse 2", null),
        new("clever fit Stockerau", "clever-fit", "Stockerau", "2000", "Hauptstrasse 13", null),
        new("clever fit Strasshof", "clever-fit", "Strasshof an der Nordbahn", "2231", "Gutshofstrasse 3", null),
        new("clever fit Wiener Neustadt", "clever-fit", "Wiener Neustadt", "2700", "Pottendorfer Strasse 39", null),
        new("clever fit Zwettl", "clever-fit", "Zwettl", "3910", "Zukunftsstrasse 6", null),
        new("FitInn St. Poelten Mariazellerstrasse", "fitinn", "St. Poelten", "3100", "Mariazellerstrasse 75", null),
        new("FitInn St. Poelten Traisencenter", "fitinn", "St. Poelten", "3107", "Dr.-Adolf-Schaerf-Strasse 10", null),
        new("FitInn Schwechat Einkaufszentrum", "fitinn", "Schwechat", "2320", "", null),
        new("FitInn Wiener Neudorf SCS", "fitinn", "Wiener Neudorf", "2334", "", null),
        new("FitInn Wiener Neustadt FMZ Nord", "fitinn", "Wiener Neustadt", "2700", "", null),
        new("Fit Fabrik Plus Flughafen", "fit-fabrik", "Schwechat", "1300", "Office Park 3", null),
        new("Fit Fabrik Plus Gerasdorf", "fit-fabrik", "Gerasdorf bei Wien", "2201", "", null),
        new("Fit Fabrik Plus Poysdorf", "fit-fabrik", "Poysdorf", "2170", "Baumfeldstrasse 4", null),
        new("Fit Fabrik Plus St. Poelten", "fit-fabrik", "St. Poelten", "3100", "Daniel-Gran-Strasse 13", null),
        new("Fit Fabrik Plus Ternitz", "fit-fabrik", "Ternitz", "2630", "Franz-Samwald-Strasse 65", null),
        new("Mrs.Sporty Amstetten", "mrs-sporty", "Amstetten", "3300", "Mozartstrasse 22", null),
        new("Mrs.Sporty Baden", "mrs-sporty", "Baden", "2500", "Voeslauer Strasse 9", null),
        new("Mrs.Sporty Bruck an der Leitha", "mrs-sporty", "Bruck an der Leitha", "2460", "", null),
        new("Mrs.Sporty Gaenserndorf", "mrs-sporty", "Gaenserndorf", "2230", "Bahnstrasse 52", null),
        new("Mrs.Sporty Gmuend", "mrs-sporty", "Gmuend", "3950", "Stadtplatz 46", null),
        new("Mrs.Sporty Hollabrunn", "mrs-sporty", "Hollabrunn", "2020", "Koliskoplatz 2", null),
        new("Mrs.Sporty Horn", "mrs-sporty", "Horn", "3580", "Wiener Strasse 49", null),
        new("Mrs.Sporty Klosterneuburg", "mrs-sporty", "Klosterneuburg", "3400", "Stadtplatz 15", null),
        new("Mrs.Sporty Korneuburg", "mrs-sporty", "Korneuburg", "2100", "Wiener Ring 15", null),
        new("Mrs.Sporty Kottingbrunn", "mrs-sporty", "Kottingbrunn", "2542", "", null),
        new("Mrs.Sporty Krems Stadtkern", "mrs-sporty", "Krems", "3500", "Schwedengasse 1c", null),
        new("Mrs.Sporty Langenzersdorf", "mrs-sporty", "Langenzersdorf", "2103", "Wiener Strasse 5", null),
        new("Mrs.Sporty Mistelbach", "mrs-sporty", "Mistelbach", "2130", "Bahnstrasse 9", null),
        new("Mrs.Sporty Moedling", "mrs-sporty", "Moedling", "2340", "Wiener Strasse 2", null),
        new("Mrs.Sporty Neulengbach", "mrs-sporty", "Neulengbach", "3040", "Rathausplatz 9", null),
        new("Mrs.Sporty Perchtoldsdorf", "mrs-sporty", "Perchtoldsdorf", "2380", "Brunnergasse 2", null),
        new("Mrs.Sporty Purkersdorf", "mrs-sporty", "Purkersdorf", "3002", "Hauptplatz 4", null),
        new("Mrs.Sporty Stockerau", "mrs-sporty", "Stockerau", "2000", "Hauptstrasse 41", null),
        new("Mrs.Sporty St. Poelten Sued", "mrs-sporty", "St. Poelten", "3100", "Josefstrasse 110", null),
        new("Mrs.Sporty Traiskirchen", "mrs-sporty", "Traiskirchen", "2514", "Hauptplatz 17", null),
        new("Mrs.Sporty Tulln", "mrs-sporty", "Tulln", "3430", "Wilhelmstrasse 4-6", null),
        new("Mrs.Sporty Waidhofen an der Thaya", "mrs-sporty", "Waidhofen an der Thaya", "3830", "", null),
        new("Mrs.Sporty Wolkersdorf", "mrs-sporty", "Wolkersdorf", "2120", "Hofgartenstrasse 28", null),
        new("Mrs.Sporty Zwettl", "mrs-sporty", "Zwettl", "3910", "Hamerlingstrasse 1", null),
        new("MYGYM Hollabrunn", "mygym", "Hollabrunn", "2020", "Gewerbering 11", null),
    ];
}
