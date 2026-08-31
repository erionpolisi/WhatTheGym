namespace Gym.Infrastructure.Seeding;

/// <summary>
/// Platform-wide seed vocabulary shared by every regional catalog (single source of truth:
/// chains and amenities are not specific to one Bundesland). Regional gym lists live in their
/// own catalog classes (ViennaCatalog plus the inert catalogs under Regional/).
/// </summary>
public static class SeedVocabulary
{
    /// <summary>Gym chains operating in Austria that seeded or reference data points to.</summary>
    public static readonly IReadOnlyList<(string Name, string? Website)> Chains =
    [
        ("FitInn", "https://www.fitinn.at"),
        ("McFIT", "https://www.mcfit.com"),
        ("clever fit", "https://www.clever-fit.com"),
        ("John Harris Fitness", "https://www.johnharris.at"),
        ("Holmes Place", "https://www.holmesplace.at"),
        ("Fit Fabrik", "https://www.fitfabrik.at"),
        ("Kieser Training", "https://www.kieser.com"),
        ("Mrs.Sporty", "https://www.mrssporty.at"),
        ("Club Danube", "https://www.clubdanube.at"),
        ("MYGYM", "https://www.mygym.at"),
        ("JOHN REED", "https://johnreed.fitness"),
    ];

    /// <summary>Amenity vocabulary offered to reviews/filters; German display names.</summary>
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
}
