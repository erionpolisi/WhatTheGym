using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Domain.Tests;

public class ReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private static Review CreatePublished(ReviewRatings? ratings = null) =>
        Review.Create(Guid.NewGuid(), Guid.NewGuid(), ratings ?? new ReviewRatings { Equipment = 4 }, "Passt.", Now).Value;

    [Fact]
    public void Create_requires_at_least_one_rating()
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings(), null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.ratings");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_rejects_out_of_range_ratings(int value)
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings { Staff = value }, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.ratings.range");
    }

    [Fact]
    public void Create_rejects_overlong_text()
    {
        var result = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings { Staff = 3 }, new string('x', Review.MaxTextLength + 1), Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_publishes_automatically()
    {
        CreatePublished().Status.Should().Be(ReviewStatus.Published);
    }

    [Fact]
    public void Soft_delete_is_reversible()
    {
        var review = CreatePublished();

        review.SoftDelete(ReviewDeletionOrigin.Author, null, Now).IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.SoftDeleted);
        review.CountsTowardsScores.Should().BeFalse();

        review.Restore(Now).IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        review.DeletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Under_review_content_is_not_public_and_not_score_relevant()
    {
        var review = CreatePublished();

        review.PlaceUnderLegalReview(Now).IsSuccess.Should().BeTrue();

        review.IsPublic.Should().BeFalse();
        review.CountsTowardsScores.Should().BeFalse();
        review.Edit(new ReviewRatings { Staff = 1 }, null, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Legal_removal_is_terminal_for_authors()
    {
        var review = CreatePublished();
        review.RemoveLegal(Now);

        review.Status.Should().Be(ReviewStatus.RemovedLegal);
        review.Edit(new ReviewRatings { Staff = 5 }, null, Now).IsFailure.Should().BeTrue();
        review.SoftDelete(ReviewDeletionOrigin.Author, null, Now).IsFailure.Should().BeTrue();
        review.Restore(Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Reinstate_after_successful_appeal_restores_publication()
    {
        var review = CreatePublished();
        review.RemoveLegal(Now);

        review.ReinstateFromLegalRemoval(Now).IsSuccess.Should().BeTrue();

        review.Status.Should().Be(ReviewStatus.Published);
    }

    [Fact]
    public void Edit_increments_edit_count_and_sanitizes_text()
    {
        var review = CreatePublished();

        review.Edit(new ReviewRatings { Staff = 2 }, "  Neuer Text\u0000 mit Steuerzeichen ", Now).IsSuccess.Should().BeTrue();

        review.EditCount.Should().Be(1);
        review.Text.Should().Be("Neuer Text mit Steuerzeichen");
        review.Ratings.Staff.Should().Be(2);
    }
}

public class SlugTests
{
    [Theory]
    [InlineData("FitInn Landstraßer Hauptstraße", "fitinn-landstrasser-hauptstrasse")]
    [InlineData("John Harris  Fitness!", "john-harris-fitness")]
    [InlineData("Café Übung äöü", "cafe-uebung-aeoeue")]
    [InlineData("--- Wien 22 ---", "wien-22")]
    public void Generates_stable_ascii_slugs(string input, string expected)
    {
        Domain.Common.Slug.Generate(input).Should().Be(expected);
    }

    [Fact]
    public void Same_input_produces_same_slug()
    {
        Domain.Common.Slug.Generate("McFIT Wien Favoriten").Should().Be(Domain.Common.Slug.Generate("McFIT Wien Favoriten"));
    }
}

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Anonymize_removes_all_identifying_values()
    {
        var user = User.CreateFromGoogle("google-sub-1", "max@example.com", true, "Max Muster", Now);

        user.Anonymize(Now);

        user.Email.Should().NotContain("max@");
        user.GoogleSubject.Should().StartWith("deleted:");
        user.DisplayName.Should().Be("Geloeschtes Konto");
        user.EmailVerified.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Deleted);
        user.IsVerifiedGoogleAccount.Should().BeFalse();
    }

    [Fact]
    public void Unverified_google_account_gets_no_badge()
    {
        var user = User.CreateFromGoogle("sub", "a@b.c", emailVerified: false, "A", Now);

        user.IsVerifiedGoogleAccount.Should().BeFalse();
    }
}
