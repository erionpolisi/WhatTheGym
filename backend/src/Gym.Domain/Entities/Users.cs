using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Domain.Entities;

public sealed class User : Entity
{
    private User()
    {
        GoogleSubject = null!;
        Email = null!;
        DisplayName = null!;
    }

    public string GoogleSubject { get; private set; }

    public string Email { get; private set; }

    public bool EmailVerified { get; private set; }

    public string DisplayName { get; private set; }

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsVerifiedGoogleAccount => EmailVerified && Status == UserStatus.Active;

    public static User CreateFromGoogle(string googleSubject, string email, bool emailVerified, string displayName, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        GoogleSubject = googleSubject,
        Email = email.Trim(),
        EmailVerified = emailVerified,
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Mitglied" : displayName.Trim(),
        Role = UserRole.User,
        Status = UserStatus.Active,
        CreatedAtUtc = utcNow,
        UpdatedAtUtc = utcNow,
        LastLoginAtUtc = utcNow,
    };

    public void RecordLogin(string email, bool emailVerified, DateTimeOffset utcNow)
    {
        Email = email.Trim();
        EmailVerified = emailVerified;
        LastLoginAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Result UpdateProfile(string displayName, DateTimeOffset utcNow)
    {
        var sanitized = TextSanitizer.Sanitize(displayName);
        if (sanitized is null || sanitized.Length is < 2 or > 80)
        {
            return Result.Failure(Error.Validation("user.displayName", "Display name must be between 2 and 80 characters."));
        }

        DisplayName = sanitized;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public void SetRole(UserRole role, DateTimeOffset utcNow)
    {
        Role = role;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>GDPR account deletion: replaces all personal fields with non-identifying tombstone values.</summary>
    public void Anonymize(DateTimeOffset utcNow)
    {
        GoogleSubject = $"deleted:{Id:N}";
        Email = $"deleted-{Id:N}@anonymized.invalid";
        EmailVerified = false;
        DisplayName = "Geloeschtes Konto";
        Status = UserStatus.Deleted;
        DeletedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTimeOffset utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset utcNow, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAtUtc = utcNow,
        ExpiresAtUtc = utcNow.Add(lifetime),
    };

    public void Revoke(DateTimeOffset utcNow, string? replacedByTokenHash = null)
    {
        RevokedAtUtc ??= utcNow;
        ReplacedByTokenHash ??= replacedByTokenHash;
    }
}
