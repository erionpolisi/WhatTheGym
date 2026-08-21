namespace Gym.Infrastructure.Seeding;

public sealed record SeedGym(string Name, string? ChainSlug, int District, string Address, string PostalCode);

/// <summary>
/// Official Vienna gym catalogue used for local seeding (~50 studios).
/// Names and districts follow publicly known studio locations; verify addresses against
/// official sources before any production import (see docs/seed-data.md).
/// </summary>
public static class ViennaCatalog
{
    public static readonly IReadOnlyList<(string Name, string? Website)> Chains =
    [
        ("FitInn", "https://www.fitinn.at"),
        ("McFIT", "https://www.mcfit.com"),
        ("clever fit", "https://www.clever-fit.com"),
        ("John Harris Fitness", "https://www.johnharris.at"),
        ("Holmes Place", "https://www.holmesplace.at"),
        ("Fit Fabrik", "https://www.fitfabrik.at"),
        ("Kieser Training", "https://www.kieser-training.at"),
        ("Mrs.Sporty", "https://www.mrssporty.at"),
        ("Club Danube", "https://www.clubdanube.at"),
        ("MYGYM", "https://www.mygym.at"),
    ];

    public static readonly IReadOnlyList<string> Amenities =
    [
        "Freihanteln",
        "Kraftmaschinen",
        "Cardio-Bereich",
        "Kursprogramm",
        "Sauna",
        "Duschen",
        "Umkleiden",
        "Parkplatz",
        "Klimaanlage",
        "Rund um die Uhr geoeffnet",
        "Getraenkestation",
        "Personal Training",
    ];

    public static readonly IReadOnlyList<SeedGym> Gyms =
    [
        // FitInn
        new("FitInn Landstrasser Hauptstrasse", "fitinn", 3, "Landstrasser Hauptstrasse 99", "1030"),
        new("FitInn Margaretenstrasse", "fitinn", 5, "Margaretenstrasse 85", "1050"),
        new("FitInn Mariahilfer Strasse", "fitinn", 6, "Mariahilfer Strasse 103", "1060"),
        new("FitInn Alser Strasse", "fitinn", 9, "Alser Strasse 28", "1090"),
        new("FitInn Favoritenstrasse", "fitinn", 10, "Favoritenstrasse 86", "1100"),
        new("FitInn Simmeringer Hauptstrasse", "fitinn", 11, "Simmeringer Hauptstrasse 96", "1110"),
        new("FitInn Meidlinger Hauptstrasse", "fitinn", 12, "Meidlinger Hauptstrasse 73", "1120"),
        new("FitInn Huetteldorfer Strasse", "fitinn", 14, "Huetteldorfer Strasse 130", "1140"),
        new("FitInn Mariahilfer Strasse West", "fitinn", 15, "Mariahilfer Strasse 167", "1150"),
        new("FitInn Thaliastrasse", "fitinn", 16, "Thaliastrasse 44", "1160"),
        new("FitInn Bruenner Strasse", "fitinn", 21, "Bruenner Strasse 57", "1210"),
        new("FitInn Donaufelder Strasse", "fitinn", 22, "Donaufelder Strasse 101", "1220"),

        // McFIT
        new("McFIT Wien Favoriten", "mcfit", 10, "Davidgasse 90", "1100"),
        new("McFIT Wien Landstrasse", "mcfit", 3, "Franzosengraben 12", "1030"),
        new("McFIT Wien Ottakring", "mcfit", 16, "Thaliastrasse 125", "1160"),
        new("McFIT Wien Floridsdorf", "mcfit", 21, "Bruenner Strasse 25", "1210"),
        new("McFIT Wien Donaustadt", "mcfit", 22, "Stadlauer Strasse 41", "1220"),

        // clever fit
        new("clever fit Wien Erdberg", "clever-fit", 3, "Erdbergstrasse 202", "1030"),
        new("clever fit Wien Meidling", "clever-fit", 12, "Schoenbrunner Strasse 247", "1120"),
        new("clever fit Wien Fuenfhaus", "clever-fit", 15, "Huetteldorfer Strasse 81", "1150"),
        new("clever fit Wien Floridsdorf", "clever-fit", 21, "Am Spitz 2", "1210"),
        new("clever fit Wien Donaustadt", "clever-fit", 22, "Wagramer Strasse 94", "1220"),

        // John Harris
        new("John Harris Nibelungengasse", "john-harris-fitness", 1, "Nibelungengasse 5", "1010"),
        new("John Harris Margareten", "john-harris-fitness", 5, "Strobachgasse 7-9", "1050"),
        new("John Harris DC Tower", "john-harris-fitness", 22, "Donau-City-Strasse 7", "1220"),
        new("John Harris Schillerplatz", "john-harris-fitness", 1, "Schillerplatz 4", "1010"),

        // Holmes Place
        new("Holmes Place Boersegasse", "holmes-place", 1, "Boersegasse 11", "1010"),
        new("Holmes Place Millennium City", "holmes-place", 20, "Handelskai 94-96", "1200"),
        new("Holmes Place Hietzing", "holmes-place", 13, "Auhofstrasse 1", "1130"),

        // Fit Fabrik
        new("Fit Fabrik Schlachthausgasse", "fit-fabrik", 3, "Schlachthausgasse 11", "1030"),
        new("Fit Fabrik Favoriten", "fit-fabrik", 10, "Quellenstrasse 2c", "1100"),
        new("Fit Fabrik Wienerberg", "fit-fabrik", 12, "Wienerbergstrasse 11", "1120"),
        new("Fit Fabrik Floridsdorf", "fit-fabrik", 21, "Ignaz-Koeck-Strasse 1", "1210"),
        new("Fit Fabrik Kagran", "fit-fabrik", 22, "Kagraner Platz 24", "1220"),

        // Kieser Training
        new("Kieser Training Wien Innere Stadt", "kieser-training", 1, "Kaerntner Ring 5-7", "1010"),
        new("Kieser Training Wien Alsergrund", "kieser-training", 9, "Nussdorfer Strasse 4", "1090"),
        new("Kieser Training Wien Hietzing", "kieser-training", 13, "Lainzer Strasse 2", "1130"),

        // Mrs.Sporty
        new("Mrs.Sporty Wien Leopoldstadt", "mrs-sporty", 2, "Taborstrasse 24", "1020"),
        new("Mrs.Sporty Wien Alsergrund", "mrs-sporty", 9, "Waehringer Strasse 59", "1090"),
        new("Mrs.Sporty Wien Penzing", "mrs-sporty", 14, "Linzer Strasse 129", "1140"),
        new("Mrs.Sporty Wien Liesing", "mrs-sporty", 23, "Breitenfurter Strasse 372", "1230"),

        // Club Danube
        new("Club Danube Erdberg", "club-danube", 3, "Franzosengraben 2", "1030"),
        new("Club Danube Ottakring", "club-danube", 16, "Sandleitengasse 39", "1160"),
        new("Club Danube Donauzentrum", "club-danube", 22, "Wagramer Strasse 81", "1220"),
        new("Club Danube Alterlaa", "club-danube", 23, "Anton-Baumgartner-Strasse 44", "1230"),

        // MYGYM
        new("MYGYM Lugner City", "mygym", 15, "Gablenzgasse 11", "1150"),
        new("MYGYM Hauptbahnhof", "mygym", 10, "Canettistrasse 1", "1100"),

        // Independents
        new("CrossFit Vienna", null, 2, "Wehlistrasse 150", "1020"),
        new("Doorbreaker Gasometer", null, 11, "Guglgasse 6", "1110"),
        new("Trainingslager Wien", null, 7, "Kaiserstrasse 43", "1070"),
    ];
}
