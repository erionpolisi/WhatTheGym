using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class UserAndTokenEdgeTests
{
    public static TheoryData<UserRole> Roles
    {
        get
        {
            var data = new TheoryData<UserRole>();
            foreach (var role in Enum.GetValues<UserRole>())
            {
                data.Add(role);
            }

            return data;
        }
    }

    public static TheoryData<string, bool> DisplayNameCases => new()
    {
        { "", false },
        { " ", false },
        { "A", false },
        { "Ab", true },
        { new string('x', 80), true },
        { new string('x', 81), false },
        { "  Anna \u0000 Muster  ", true },
    };

    [Theory]
    [InlineData("Max", "Max")]
    [InlineData("  Max Muster  ", "Max Muster")]
    [InlineData("", "Mitglied")]
    [InlineData("   ", "Mitglied")]
    public void Create_from_google_trims_email_and_defaults_blank_display_name(string displayName, string expectedDisplayName)
    {
        var user = User.CreateFromGoogle("sub", "  max@example.com  ", true, displayName, DomainTestHelpers.Now);

        user.Email.Should().Be("max@example.com");
        user.DisplayName.Should().Be(expectedDisplayName);
        user.Role.Should().Be(UserRole.User);
        user.Status.Should().Be(UserStatus.Active);
        user.CreatedAtUtc.Should().Be(DomainTestHelpers.Now);
        user.LastLoginAtUtc.Should().Be(DomainTestHelpers.Now);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Verified_badge_requires_verified_email_and_active_status(bool emailVerified, bool expectedBadge)
    {
        var user = User.CreateFromGoogle("sub", "max@example.com", emailVerified, "Max", DomainTestHelpers.Now);

        user.IsVerifiedGoogleAccount.Should().Be(expectedBadge);
        user.Anonymize(DomainTestHelpers.Now.AddDays(1));
        user.IsVerifiedGoogleAccount.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(DisplayNameCases))]
    public void Update_profile_validates_sanitized_display_name_length(string displayName, bool expectedSuccess)
    {
        var user = User.CreateFromGoogle("sub", "max@example.com", true, "Max", DomainTestHelpers.Now);

        var result = user.UpdateProfile(displayName, DomainTestHelpers.Now.AddMinutes(1));

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            user.UpdatedAtUtc.Should().Be(DomainTestHelpers.Now.AddMinutes(1));
            user.DisplayName.Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            result.Error.Code.Should().Be("user.displayName");
        }
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public void Set_role_accepts_all_declared_roles(UserRole role)
    {
        var user = User.CreateFromGoogle("sub", "max@example.com", true, "Max", DomainTestHelpers.Now);
        var changedAt = DomainTestHelpers.Now.AddMinutes(1);

        user.SetRole(role, changedAt);

        user.Role.Should().Be(role);
        user.UpdatedAtUtc.Should().Be(changedAt);
    }

    [Fact]
    public void Record_login_updates_email_verification_and_timestamps()
    {
        var user = User.CreateFromGoogle("sub", "old@example.com", false, "Max", DomainTestHelpers.Now);
        var loginAt = DomainTestHelpers.Now.AddHours(3);

        user.RecordLogin("  new@example.com ", true, loginAt);

        user.Email.Should().Be("new@example.com");
        user.EmailVerified.Should().BeTrue();
        user.LastLoginAtUtc.Should().Be(loginAt);
        user.UpdatedAtUtc.Should().Be(loginAt);
    }

    [Fact]
    public void Anonymize_is_deterministic_and_sets_deleted_timestamp()
    {
        var user = User.CreateFromGoogle("sub", "max@example.com", true, "Max", DomainTestHelpers.Now);
        var deletedAt = DomainTestHelpers.Now.AddDays(1);

        user.Anonymize(deletedAt);

        user.GoogleSubject.Should().Be($"deleted:{user.Id:N}");
        user.Email.Should().Be($"deleted-{user.Id:N}@anonymized.invalid");
        user.DisplayName.Should().Be("Geloeschtes Konto");
        user.Status.Should().Be(UserStatus.Deleted);
        user.DeletedAtUtc.Should().Be(deletedAt);
        user.UpdatedAtUtc.Should().Be(deletedAt);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    public void Refresh_token_active_requires_not_revoked_and_expiry_strictly_in_future(int secondsFromNow, bool expectedActive)
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", DomainTestHelpers.Now, TimeSpan.FromSeconds(secondsFromNow));

        token.IsActive(DomainTestHelpers.Now).Should().Be(expectedActive);
    }

    [Fact]
    public void Refresh_token_revoke_is_idempotent_and_preserves_first_replacement_hash()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", DomainTestHelpers.Now, TimeSpan.FromDays(7));
        var first = DomainTestHelpers.Now.AddMinutes(1);

        token.Revoke(first, "new-hash");
        token.Revoke(first.AddMinutes(1), "other-hash");

        token.IsActive(first.AddMinutes(2)).Should().BeFalse();
        token.RevokedAtUtc.Should().Be(first);
        token.ReplacedByTokenHash.Should().Be("new-hash");
    }

    // Layering decision: the only callers are the Google OIDC callback (Google guarantees a
    // subject) and the dev-login endpoint (validator enforces the email). The entity does not
    // re-validate identity format. Documented here.
    [Fact]
    public void Create_from_google_trusts_upstream_identity_validation_by_design()
    {
        User.CreateFromGoogle(" ", " ", true, "Max", DomainTestHelpers.Now).DisplayName.Should().Be("Max");
    }
}
