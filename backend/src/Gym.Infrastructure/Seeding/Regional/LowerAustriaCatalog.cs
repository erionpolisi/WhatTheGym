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
        EmbeddedSeedData.RequiredRegionalCatalog("Niederoesterreich");
}
