namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Upper Austria (Oberoesterreich) gym reference data. Researched 2026-08-31 from official
/// chain sources. NOT seeded, NOT tested, NOT wired — inert data per ADR 0011.
/// </summary>
public static class UpperAustriaCatalog
{
    public static readonly IReadOnlyList<RegionalSeedGym> Gyms =
    [
        new("clever fit Linz Wegscheid", "clever-fit", "Linz", "4030", "Baeckermuehlweg 59", null),
        new("clever fit Linz Zentrum", "clever-fit", "Linz", "4020", "Dametzstrasse 7", null),
        new("clever fit Ried", "clever-fit", "Ried im Innkreis", "4910", "", null),
        new("clever fit Steyr", "clever-fit", "Steyr", "4400", "Haager Strasse 46", null),
        new("clever fit Traun", "clever-fit", "Traun", "4050", "Kremstalerstrasse 113", null),
        new("clever fit Wels", "clever-fit", "Wels", "4600", "", null),
        new("clever fit Wels West", "clever-fit", "Wels", "4600", "", null),
        new("FitInn Linz Bulgariplatz", "fitinn", "Linz", "4020", "", null),
        new("FitInn Linz Infra Center", "fitinn", "Linz", "4020", "Wegscheider Strasse 3", null),
        new("FitInn Linz Rainerstrasse", "fitinn", "Linz", "4020", "Rainerstrasse 6-8", null),
        new("FitInn Linz Urfahr", "fitinn", "Linz", "4040", "", null),
        new("FitInn Pasching PlusCity", "fitinn", "Pasching", "4061", "", null),
        new("FitInn Wels ShoppingCity", "fitinn", "Wels", "4600", "", null),
        new("John Harris Donaupark Linz", "john-harris-fitness", "Linz", "4020", "Untere Donaulaende 21-25", null),
        new("John Harris Atrium Linz", "john-harris-fitness", "Linz", "4020", "Mozartstrasse 7-11", null),
        new("Mrs.Sporty Enns", "mrs-sporty", "Enns", "4470", "Eichbergstrasse 1", null),
        new("Mrs.Sporty Linz Zentrum", "mrs-sporty", "Linz", "4020", "", null),
        new("Mrs.Sporty Marchtrenk", "mrs-sporty", "Marchtrenk", "4614", "", null),
        new("Mrs.Sporty Traun", "mrs-sporty", "Traun", "4050", "Bahnhofstrasse 21", null),
        new("Mrs.Sporty Voecklabruck", "mrs-sporty", "Voecklabruck", "4840", "Gmundner Strasse 47-49", null),
        new("Mrs.Sporty Wels", "mrs-sporty", "Wels", "4600", "Kaiser-Josef-Platz 41", null),
        new("MYGYM Ried", "mygym", "Ried im Innkreis", "4910", "Wohlmayrgasse 4", null),
    ];
}
