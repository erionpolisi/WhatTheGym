namespace Gym.Infrastructure.Seeding;

/// <summary>
/// Official Vienna gym catalogue used for local seeding. Rebuilt 2026-08-31 from official chain
/// studio finders and sitemaps (fitinn.at, clever-fit.com, mrssporty.at, johnharris.at,
/// holmesplace.at, fitfabrik.at, johnreed.fitness, crossfitvienna.at); verification ledger in
/// docs/seed-data.md. Entries marked "unverified" below kept from prior data because the chain
/// site was not machine-readable; re-check before production import (ADR 0009/0011).
/// Chains and amenities live in <see cref="SeedVocabulary"/>.
/// </summary>
public static class ViennaCatalog
{
    public static readonly IReadOnlyList<SeedGym> Gyms =
    [
        // FitInn (fitinn.at studio pages, verified 2026-08-31)
        new("FitInn Wien Rathausplatz", "fitinn", 1, "Rathausplatz 2", "1010"),
        new("FitInn Wien Schwedenplatz", "fitinn", 1, "Laurenzerberg 2", "1010"),
        new("FitInn Wien Stadion Center", "fitinn", 2, "Olympiaplatz 2", "1020"),
        new("FitInn Wien Dietrichgasse", "fitinn", 3, "Dietrichgasse 25", "1030"),
        new("FitInn Wien Mitte", "fitinn", 3, "Invalidenstraße 4", "1030"),
        new("FitInn Wien Hartmanngasse", "fitinn", 5, "Hartmanngasse 1", "1050"),
        new("FitInn Wien Hofmühlgasse", "fitinn", 6, "Hofmühlgasse 3-5", "1060"),
        new("FitInn Wien Gerngross", "fitinn", 7, "Mariahilfer Straße 42-48", "1070"),
        new("FitInn Wien Mariahilfer Straße", "fitinn", 7, "Mariahilfer Straße 122", "1070"),
        new("FitInn Wien Alser Straße", "fitinn", 9, "Alser Straße 28-30", "1090"),
        new("FitInn Wien Friedensbrücke", "fitinn", 9, "Roßauer Lände 47-49", "1090"),
        new("FitInn Wien Ada-Christen-Gasse", "fitinn", 10, "Ada-Christen-Gasse 12", "1100"),
        new("FitInn Wien Favoritenstraße", "fitinn", 10, "Favoritenstraße 88-90", "1100"),
        new("FitInn Wien Keplerplatz", "fitinn", 10, "Keplerplatz 14", "1100"),
        new("FitInn Wien Gasometer", "fitinn", 11, "Guglgasse 14", "1110"),
        new("FitInn Wien Edelsinnstraße", "fitinn", 12, "Edelsinnstraße 4", "1120"),
        new("FitInn Wien Sagedergasse", "fitinn", 12, "Sagedergasse 18-22", "1120"),
        new("FitInn Wien U4 Center", "fitinn", 12, "Schönbrunner Straße 222-228", "1120"),
        new("FitInn Wien Hütteldorf", "fitinn", 14, "Deutschordenstraße 3", "1140"),
        new("FitInn Wien Johnstraße", "fitinn", 15, "Johnstraße 65", "1150"),
        new("FitInn Wien Meiselmarkt", "fitinn", 15, "Hütteldorfer Straße 81b", "1150"),
        new("FitInn Wien Kendlerstraße", "fitinn", 16, "Kendlerstraße 47", "1160"),
        new("FitInn Wien Ottakringer Straße", "fitinn", 17, "Ottakringer Straße 72", "1170"),
        new("FitInn Wien Q19", "fitinn", 19, "Kreilplatz 1", "1190"),
        new("FitInn Wien Handelskai", "fitinn", 20, "Wehlistraße 65", "1200"),
        new("FitInn Wien Floridsdorf", "fitinn", 21, "Franz-Jonas-Platz 2-3", "1210"),
        new("FitInn Wien SCN", "fitinn", 21, "Ignaz-Köck-Straße 1-7", "1210"),
        new("FitInn Wien Donau-Zentrum", "fitinn", 22, "Dr.-Adolf-Schaerf-Platz 4", "1220"),
        new("FitInn Wien Gewerbepark Stadlau", "fitinn", 22, "Zwerchaeckerweg 20-26", "1220"),
        new("FitInn Wien Kagraner Platz", "fitinn", 22, "Kagraner Platz 1-4", "1220"),
        new("FitInn Wien Alterlaa", "fitinn", 23, "Altmannsdorfer Straße 158", "1230"),

        // clever fit (clever-fit.com studio pages, verified 2026-08-31)
        new("clever fit Wien Leopoldstadt", "clever-fit", 2, "Jakov-Lind-Straße 2", "1020"),
        new("clever fit Wien Landstraße", "clever-fit", 3, "Markhofgasse 15-17", "1030"),
        new("clever fit Wien Mariahilfer Straße", "clever-fit", 6, "Mariahilfer Straße 71", "1060"),
        new("clever fit Wien Neubau", "clever-fit", 7, "Seidengasse 9-11", "1070"),
        new("clever fit Wien Favoriten", "clever-fit", 10, "Kundratstraße 6", "1100"),
        new("clever fit Wien Keplerplatz", "clever-fit", 10, "Favoritenstraße 92", "1100"),
        new("clever fit Wien Simmering", "clever-fit", 11, "Etrichstraße 23", "1110"),
        new("clever fit Wien Penzing", "clever-fit", 14, "Hütteldorfer Straße 219", "1140"),
        new("clever fit Wien Döbling", "clever-fit", 19, "Franz-Klein-Gasse 5", "1190"),
        new("clever fit Wien Brigittenau", "clever-fit", 20, "Dresdner Straße 107", "1200"),
        new("clever fit Wien Floridsdorf", "clever-fit", 21, "Trillergasse 4", "1210"),
        new("clever fit Wien Stadlau", "clever-fit", 22, "Gewerbeparkstraße 8", "1220"),
        new("clever fit Wien Liesing", "clever-fit", 23, "Breitenfurter Straße 233", "1230"),

        // John Harris Fitness (johnharris.at studio data, verified 2026-08-31)
        new("John Harris Executive Club", "john-harris-fitness", 1, "Opernring 13-15", "1010"),
        new("John Harris Medical Center", "john-harris-fitness", 1, "Getreidemarkt 8", "1010"),
        new("John Harris Schillerplatz", "john-harris-fitness", 1, "Nibelungengasse 5", "1010"),
        new("John Harris UNIQA Tower", "john-harris-fitness", 2, "Untere Donaustraße 21", "1020"),
        new("John Harris Sofiensäle", "john-harris-fitness", 3, "Marxergasse 17", "1030"),
        new("John Harris Margaretenplatz", "john-harris-fitness", 5, "Strobachgasse 7-9", "1050"),
        new("John Harris Hauptbahnhof", "john-harris-fitness", 10, "Wiedner Guertel 9", "1100"),
        new("John Harris DC Tower", "john-harris-fitness", 22, "Donau-City-Straße 7", "1220"),

        // Holmes Place (holmesplace.at club pages, verified 2026-08-31)
        new("Holmes Place Börseplatz", "holmes-place", 1, "Wipplingerstraße 30", "1010"),
        new("Holmes Place Hütteldorf", "holmes-place", 14, "Hütteldorfer Straße 130a", "1140"),
        new("Holmes Place Millennium", "holmes-place", 20, "Wehlistraße 66", "1200"),

        // Fit Fabrik (fitfabrik.at studio pages, verified 2026-08-31)
        new("Fit Fabrik Plus Messecarree", "fit-fabrik", 2, "Vorgartenstraße 204", "1020"),
        new("Fit Fabrik Hietzing", "fit-fabrik", 13, "Hietzinger Kai 133", "1130"),
        new("Fit Fabrik Plus Hütteldorf", "fit-fabrik", 14, "Bergmillergasse 5", "1140"),
        new("Fit Fabrik Plus Döbling", "fit-fabrik", 19, "Billrothstraße 2", "1190"),
        new("Fit Fabrik Plus Handelskai", "fit-fabrik", 20, "Wehlistraße 35-43", "1200"),
        new("Fit Fabrik Maculangasse", "fit-fabrik", 22, "Maculangasse 1", "1220"),
        new("Fit Fabrik Plus Stadlau", "fit-fabrik", 22, "Gewerbeparkstraße 3", "1220"),

        // Mrs.Sporty (mrssporty.at club pages, verified 2026-08-31)
        new("Mrs.Sporty Wien Leopoldstadt", "mrs-sporty", 2, "Untere Augartenstraße 26", "1020"),
        new("Mrs.Sporty Wien Landstraße", "mrs-sporty", 3, "Löwengasse 34", "1030"),
        new("Mrs.Sporty Wien Margareten", "mrs-sporty", 5, "Schönbrunner Straße 16", "1050"),
        new("Mrs.Sporty Wien Alsergrund", "mrs-sporty", 9, "Porzellangasse 33a", "1090"),
        new("Mrs.Sporty Wien Favoriten", "mrs-sporty", 10, "Knöllgasse 33", "1100"),
        new("Mrs.Sporty Wien Simmering", "mrs-sporty", 11, "Braunhubergasse 23", "1110"),
        new("Mrs.Sporty Wien Meidling", "mrs-sporty", 12, "Längenfeldgasse 29", "1120"),
        new("Mrs.Sporty Wien Hietzing", "mrs-sporty", 13, "Auhofstraße 51-55", "1130"),
        new("Mrs.Sporty Wien Penzing", "mrs-sporty", 14, "Matznergasse 28", "1140"),
        new("Mrs.Sporty Wien Schwendermarkt", "mrs-sporty", 15, "Mariahilfer Straße 192", "1150"),
        new("Mrs.Sporty Wien Hernals", "mrs-sporty", 17, "Hormayrgasse 19", "1170"),
        new("Mrs.Sporty Wien Döbling", "mrs-sporty", 19, "Obkirchergasse 36", "1190"),
        new("Mrs.Sporty Wien Brigittenau", "mrs-sporty", 20, "Wallensteinplatz 7", "1200"),
        new("Mrs.Sporty Wien Donaufeld", "mrs-sporty", 21, "Ostmarkgasse 2", "1210"),
        new("Mrs.Sporty Wien Aspern", "mrs-sporty", 22, "Bergengasse 3", "1220"),
        new("Mrs.Sporty Wien Alterlaa", "mrs-sporty", 23, "Anton-Baumgartner-Straße 125", "1230"),

        // JOHN REED (johnreed.fitness club page, verified 2026-08-31)
        new("JOHN REED Wien Oper", "john-reed", 1, "Kärntner Ring 5", "1010"),

        // McFIT (chain present in Vienna; mcfit.com not machine-readable — addresses unverified)
        new("McFIT Wien Favoriten", "mcfit", 10, "Davidgasse 90", "1100"),
        new("McFIT Wien Landstraße", "mcfit", 3, "Franzosengraben 12", "1030"),
        new("McFIT Wien Ottakring", "mcfit", 16, "Thaliastraße 125", "1160"),
        new("McFIT Wien Floridsdorf", "mcfit", 21, "Brünner Straße 25", "1210"),
        new("McFIT Wien Donaustadt", "mcfit", 22, "Stadlauer Straße 41", "1220"),

        // Club Danube (site not machine-readable - addresses unverified)
        new("Club Danube Erdberg", "club-danube", 3, "Franzosengraben 2", "1030"),
        new("Club Danube Ottakring", "club-danube", 16, "Sandleitengasse 39", "1160"),
        new("Club Danube Donauzentrum", "club-danube", 22, "Wagramer Straße 81", "1220"),
        new("Club Danube Alterlaa", "club-danube", 23, "Anton-Baumgartner-Straße 44", "1230"),

        // Kieser Training (kieser.com not reachable by script - addresses unverified)
        new("Kieser Training Wien Alsergrund", "kieser-training", 9, "Nußdorfer Straße 4", "1090"),
        new("Kieser Training Wien Hietzing", "kieser-training", 13, "Lainzer Straße 2", "1130"),

        // Independents (crossfitvienna.at verified; Trainingslager site unreachable - unverified)
        new("CrossFit Vienna The Starship", null, 3, "Rennweg 97-99", "1030"),
        new("Trainingslager Wien", null, 7, "Kaiserstraße 43", "1070"),
    ];
}
