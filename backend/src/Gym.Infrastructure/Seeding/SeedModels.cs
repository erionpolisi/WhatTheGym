namespace Gym.Infrastructure.Seeding;

/// <summary>Seed entry for a Vienna gym; maps 1:1 to the seeded domain model (district-based).</summary>
public sealed record SeedGym(string Name, string? ChainSlug, int District, string Address, string PostalCode);

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
