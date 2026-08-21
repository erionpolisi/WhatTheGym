using FluentAssertions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Reviews;
using Gym.Application.Features.Users;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gym.Application.Tests;

public class CreateReviewCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly RatingsDto ValidRatings = new(null, null, null, null, 4, null, 5, null, null, null, null);

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryGymRepository _gyms = new();
    private readonly InMemoryReviewRepository _reviews = new();
    private readonly InMemorySummaryStore _summaries = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateReviewCommandHandler CreateSut() => new(
        _reviews, _gyms, _users,
        new GymScoreUpdater(_reviews, _summaries),
        _unitOfWork, new FakeClock(Now), new CreateReviewCommandValidator());

    private User AddUser(bool verified = true)
    {
        var user = User.CreateFromGoogle($"sub-{Guid.NewGuid():N}", "user@example.com", verified, "Testerin", Now);
        _users.Add(user);
        return user;
    }

    private GymEntry AddGym(GymStatus status = GymStatus.Active)
    {
        var gym = GymEntry.Create("Test Gym", $"test-gym-{_gyms.Gyms.Count}", null, 10, "Strasse 1", "1100", null, null, null, status, Now).Value;
        _gyms.Add(gym);
        return gym;
    }

    [Fact]
    public async Task Verified_user_creates_published_review_and_summary_is_materialized()
    {
        var user = AddUser();
        var gym = AddGym();

        var result = await CreateSut().Handle(new CreateReviewCommand(user.Id, gym.Slug, ValidRatings, "Solide."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Published");
        _summaries.Scores[gym.Id].StudioScore.Should().Be(4.5);
        _summaries.Scores[gym.Id].Basis.Should().Be(ScoreBasis.StudioOnly);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Unverified_google_account_is_rejected()
    {
        var user = AddUser(verified: false);
        var gym = AddGym();

        var result = await CreateSut().Handle(new CreateReviewCommand(user.Id, gym.Slug, ValidRatings, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Second_active_review_for_same_gym_is_a_conflict()
    {
        var user = AddUser();
        var gym = AddGym();
        var sut = CreateSut();
        (await sut.Handle(new CreateReviewCommand(user.Id, gym.Slug, ValidRatings, null), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var second = await sut.Handle(new CreateReviewCommand(user.Id, gym.Slug, ValidRatings, null), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("review.exists");
    }

    [Fact]
    public async Task Review_without_any_rating_fails_validation()
    {
        var user = AddUser();
        var gym = AddGym();
        var empty = new RatingsDto(null, null, null, null, null, null, null, null, null, null, null);

        var result = await CreateSut().Handle(new CreateReviewCommand(user.Id, gym.Slug, empty, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Permanently_closed_gym_accepts_no_reviews()
    {
        var user = AddUser();
        var gym = AddGym(GymStatus.PermanentlyClosed);

        var result = await CreateSut().Handle(new CreateReviewCommand(user.Id, gym.Slug, ValidRatings, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gym.closed");
    }
}

public class UpsertGoogleUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private static UpsertGoogleUserCommandHandler CreateSut(InMemoryUserRepository users, string? bootstrapEmail) => new(
        users, new FakeUnitOfWork(), new FakeClock(Now),
        Options.Create(new AuthOptions { BootstrapAdminEmail = bootstrapEmail }));

    [Fact]
    public async Task First_matching_verified_login_becomes_admin()
    {
        var users = new InMemoryUserRepository();
        var sut = CreateSut(users, "chef@example.com");

        var result = await sut.Handle(new UpsertGoogleUserCommand("sub-1", "Chef@Example.com", true, "Chef"), CancellationToken.None);

        result.Value.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Unverified_email_never_bootstraps_admin()
    {
        var users = new InMemoryUserRepository();
        var sut = CreateSut(users, "chef@example.com");

        var result = await sut.Handle(new UpsertGoogleUserCommand("sub-1", "chef@example.com", false, "Chef"), CancellationToken.None);

        result.Value.Role.Should().Be("User");
    }

    [Fact]
    public async Task Bootstrap_only_applies_while_no_admin_exists()
    {
        var users = new InMemoryUserRepository();
        var existingAdmin = User.CreateFromGoogle("sub-0", "old@example.com", true, "Old Admin", Now);
        existingAdmin.SetRole(UserRole.Admin, Now);
        users.Add(existingAdmin);
        var sut = CreateSut(users, "chef@example.com");

        var result = await sut.Handle(new UpsertGoogleUserCommand("sub-1", "chef@example.com", true, "Chef"), CancellationToken.None);

        result.Value.Role.Should().Be("User");
    }

    [Fact]
    public async Task Deleted_accounts_cannot_log_in_again()
    {
        var users = new InMemoryUserRepository();
        var user = User.CreateFromGoogle("sub-1", "a@b.c", true, "A", Now);
        user.Anonymize(Now);
        users.Add(user);
        var sut = CreateSut(users, null);

        var result = await sut.Handle(new UpsertGoogleUserCommand("sub-1", "a@b.c", true, "A"), CancellationToken.None);

        // Anonymized subject no longer matches: a fresh account is created instead of reviving the old one.
        result.IsSuccess.Should().BeTrue();
        users.Users.Should().HaveCount(2);
    }
}

public class SetUserRoleCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Last_admin_cannot_be_demoted()
    {
        var users = new InMemoryUserRepository();
        var admin = User.CreateFromGoogle("sub-1", "admin@example.com", true, "Admin", Now);
        admin.SetRole(UserRole.Admin, Now);
        users.Add(admin);
        var sut = new SetUserRoleCommandHandler(users, new FakeUnitOfWork(), new FakeClock(Now));

        var result = await sut.Handle(new SetUserRoleCommand(admin.Id, "User"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("user.lastAdmin");
    }

    [Fact]
    public async Task Admin_can_promote_moderators()
    {
        var users = new InMemoryUserRepository();
        var user = User.CreateFromGoogle("sub-1", "mod@example.com", true, "Mod", Now);
        users.Add(user);
        var sut = new SetUserRoleCommandHandler(users, new FakeUnitOfWork(), new FakeClock(Now));

        var result = await sut.Handle(new SetUserRoleCommand(user.Id, "Moderator"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(UserRole.Moderator);
    }
}
