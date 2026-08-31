namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Styria (Steiermark) gym reference data. Researched 2026-08-31 from official chain sources.
/// NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class StyriaCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Graz Europaplatz", "clever-fit", "Graz", "8020", "Europaplatz 12", null),
        new("clever fit Graz Puntigam", "clever-fit", "Graz", "8055", "Brauquartier 5", null),
        new("clever fit Graz Wetzelsdorf", "clever-fit", "Graz", "8053", "Peter-Rosegger-Strasse 25", null),
        new("clever fit Deutschlandsberg", "clever-fit", "Frauental an der Lassnitz", "8523", "Marktring 1", null),
        new("clever fit Knittelfeld", "clever-fit", "Knittelfeld", "8720", "Kaerntner Strasse 100", null),
        new("clever fit Leoben", "clever-fit", "Leoben", "8700", "Kaerntner Strasse 315-319", null),
        new("clever fit Bruck an der Mur", "clever-fit", "Bruck an der Mur", "8600", "", null),
        new("clever fit Kapfenberg", "clever-fit", "Kapfenberg", "8605", "", null),
        new("clever fit Leibnitz", "clever-fit", "Leibnitz", "8430", "", null),
        new("clever fit Liezen", "clever-fit", "Liezen", "8940", "", null),
        new("FitInn Graz Hauptbahnhof", "fitinn", "Graz", "8020", "", null),
        new("FitInn Graz Liebenau", "fitinn", "Graz", "8041", "", null),
        new("FitInn Graz Steirerhof Jakominiplatz", "fitinn", "Graz", "8010", "", null),
        new("FitInn ShoppingCity Seiersberg", "fitinn", "Seiersberg-Pirka", "8055", "ShoppingCity Seiersberg Haus 1", null),
        new("John Harris Thalia Graz", "john-harris-fitness", "Graz", "8010", "Girardigasse 1c", null),
        new("Mrs.Sporty Fernitz", "mrs-sporty", "Fernitz-Mellach", "8072", "Kalsdorfer Strasse 6", null),
        new("MYGYM Murau", "mygym", "Murau", "8850", "Bahnhofviertel 14", null),
    ];
}
