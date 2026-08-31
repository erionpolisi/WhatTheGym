namespace Gym.Infrastructure.Seeding.Regional;

/// <summary>
/// Aggregated view over all inert regional catalogs (everything except Vienna), keyed by
/// Bundesland display name. Convenience for the future expansion wiring; referenced by nothing
/// at runtime today (ADR 0011).
/// </summary>
public static class AustrianRegionalCatalogs
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<RegionalSeedGym>> ByBundesland =
        new Dictionary<string, IReadOnlyList<RegionalSeedGym>>
        {
            ["Burgenland"] = BurgenlandCatalog.Gyms,
            ["Kaernten"] = CarinthiaCatalog.Gyms,
            ["Niederoesterreich"] = LowerAustriaCatalog.Gyms,
            ["Oberoesterreich"] = UpperAustriaCatalog.Gyms,
            ["Salzburg"] = SalzburgCatalog.Gyms,
            ["Steiermark"] = StyriaCatalog.Gyms,
            ["Tirol"] = TyrolCatalog.Gyms,
            ["Vorarlberg"] = VorarlbergCatalog.Gyms,
        };
}
