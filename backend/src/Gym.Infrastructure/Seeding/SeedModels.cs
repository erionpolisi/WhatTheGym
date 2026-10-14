namespace Gym.Infrastructure.Seeding;

/// <summary>Seed entry for a Vienna gym; maps 1:1 to the seeded domain model (district-based).</summary>
public sealed record SeedGym(
    string Name,
    string? ChainSlug,
    int District,
    string Address,
    string PostalCode,
    DateOnly? LocationVerifiedOn = null,
    bool? TemporarilyClosed = null);

public sealed record ChainSeed(string Name, string? Website, DateOnly? WebsiteVerifiedOn = null);

/// <summary>
/// Seed entry for a gym outside Vienna. Reference data only: the MVP domain is Vienna-scoped
/// (districts 1-23), so regional entries are city-based and are NOT seeded or exposed anywhere
/// until a deliberate expansion decision wires them up (see ADR 0011).
/// </summary>
public sealed record RegionalSeedGym(
    string Name,
    string? ChainSlug,
    string City,
    string PostalCode,
    string Address,
    string? Website);

internal sealed class CatalogSeedData
{
    public IReadOnlyList<ChainSeed>? Chains { get; init; }

    public IReadOnlyList<string>? Amenities { get; init; }

    public IReadOnlyList<SeedGym>? ViennaGyms { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<RegionalSeedGym>>? RegionalCatalogs { get; init; }
}

internal sealed class LegalDocumentsSeedData
{
    public IReadOnlyList<LegalDocumentSeed>? Documents { get; init; }
}

internal sealed record LegalDocumentSeed(
    Gym.Domain.Enums.LegalDocumentType Type,
    string Title,
    string Content);

internal sealed class DemoSeedData
{
    public IReadOnlyList<DemoUserSeed>? Users { get; init; }

    public IReadOnlyList<Gym.Domain.Entities.ReviewRatings>? Ratings { get; init; }

    public IReadOnlyList<string>? Texts { get; init; }

    public DemoLegalCaseSeed? LegalCase { get; init; }
}

internal sealed record DemoUserSeed(string GoogleSubject, string Email, bool EmailVerified, string DisplayName);

internal sealed record DemoLegalCaseSeed(
    Gym.Domain.Enums.LegalCaseCategory Category,
    string ReporterName,
    string ReporterEmail,
    string Description,
    string ReportTokenHash,
    Gym.Domain.Enums.LegalCaseEventType EventType,
    Gym.Domain.Enums.LegalActorType ActorType,
    string MetadataJson);
