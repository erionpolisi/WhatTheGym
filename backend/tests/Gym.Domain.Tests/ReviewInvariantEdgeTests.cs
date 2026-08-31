using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class ReviewInvariantEdgeTests
{
    public static TheoryData<RatingCategory, int> ValidRatings
    {
        get
        {
            var data = new TheoryData<RatingCategory, int>();
            foreach (var category in Enum.GetValues<RatingCategory>())
            {
                foreach (var value in Enumerable.Range(1, 5))
                {
                    data.Add(category, value);
                }
            }

            return data;
        }
    }

    public static TheoryData<RatingCategory, int> InvalidRatings
    {
        get
        {
            var data = new TheoryData<RatingCategory, int>();
            foreach (var category in Enum.GetValues<RatingCategory>())
            {
                foreach (var value in new[] { 0, 6, -1, 100 })
                {
                    data.Add(category, value);
                }
            }

            return data;
        }
    }

    public static TheoryData<ReviewDeletionOrigin> DeletionOrigins
    {
        get
        {
            var data = new TheoryData<ReviewDeletionOrigin>();
            foreach (var origin in Enum.GetValues<ReviewDeletionOrigin>())
            {
                data.Add(origin);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ValidRatings))]
    public void Create_accepts_every_boundary_rating_for_every_category(RatingCategory category, int value)
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), DomainTestHelpers.Rating(category, value), null, DomainTestHelpers.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Ratings.Get(category).Should().Be(value);
        result.Value.Status.Should().Be(ReviewStatus.Published);
        result.Value.IsPublic.Should().BeTrue();
        result.Value.CountsTowardsScores.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidRatings))]
    public void Create_rejects_every_out_of_range_rating_for_every_category(RatingCategory category, int value)
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), DomainTestHelpers.Rating(category, value), null, DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.ratings.range");
    }

    [Theory]
    [MemberData(nameof(ValidRatings))]
    public void Edit_accepts_every_boundary_rating_for_every_category(RatingCategory category, int value)
    {
        var review = DomainTestHelpers.CreateReview();
        var updatedAt = DomainTestHelpers.Now.AddMinutes(1);

        var result = review.Edit(DomainTestHelpers.Rating(category, value), "Aktualisiert", updatedAt);

        result.IsSuccess.Should().BeTrue();
        review.Ratings.Get(category).Should().Be(value);
        review.EditCount.Should().Be(1);
        review.UpdatedAtUtc.Should().Be(updatedAt);
    }

    [Theory]
    [MemberData(nameof(InvalidRatings))]
    public void Edit_rejects_every_out_of_range_rating_for_every_category(RatingCategory category, int value)
    {
        var review = DomainTestHelpers.CreateReview();

        var result = review.Edit(DomainTestHelpers.Rating(category, value), null, DomainTestHelpers.Now.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.ratings.range");
        review.EditCount.Should().Be(0);
    }

    [Fact]
    public void Edit_is_rejected_for_soft_deleted_reviews()
    {
        var review = DomainTestHelpers.CreateReview();
        review.SoftDelete(ReviewDeletionOrigin.Moderator, "Grund", DomainTestHelpers.Now.AddMinutes(1));

        var result = review.Edit(new ReviewRatings { Staff = 4 }, "Nachtraeglich", DomainTestHelpers.Now.AddMinutes(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.locked");
        review.EditCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Create_rejects_missing_ratings_regardless_of_optional_text(string? text)
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings(), text, DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.ratings");
    }

    [Theory]
    [InlineData("  sauber  ", "sauber")]
    [InlineData("Zeile\r\nZwei", "Zeile\nZwei")]
    [InlineData("Text\u0000mit\u0008Kontrollen", "TextmitKontrollen")]
    [InlineData("ä ö ü ß 😀", "ä ö ü ß 😀")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Create_sanitizes_optional_text_before_storing(string? input, string? expected)
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings { Staff = 3 }, input, DomainTestHelpers.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be(expected);
    }

    [Fact]
    public void Text_exactly_max_length_after_sanitization_is_allowed()
    {
        var text = "  " + new string('x', Review.MaxTextLength) + "  ";

        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings { Staff = 3 }, text, DomainTestHelpers.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().HaveLength(Review.MaxTextLength);
    }

    [Fact]
    public void Text_longer_than_max_after_sanitization_is_rejected()
    {
        var result = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings { Staff = 3 }, new string('x', Review.MaxTextLength + 1), DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.text");
    }

    [Fact]
    public void Edit_longer_than_max_after_sanitization_is_rejected_without_mutating_review()
    {
        var review = DomainTestHelpers.CreateReview(text: "Alt");

        var result = review.Edit(new ReviewRatings { Staff = 3 }, new string('x', Review.MaxTextLength + 1), DomainTestHelpers.Now.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        review.Text.Should().Be("Alt");
        review.EditCount.Should().Be(0);
    }

    [Fact]
    public void Multiple_successful_edits_increment_edit_count_and_update_timestamp_each_time()
    {
        var review = DomainTestHelpers.CreateReview();
        var first = DomainTestHelpers.Now.AddMinutes(1);
        var second = DomainTestHelpers.Now.AddMinutes(2);

        review.Edit(new ReviewRatings { Staff = 2 }, "Erste", first).IsSuccess.Should().BeTrue();
        review.Edit(new ReviewRatings { Cleanliness = 5 }, "Zweite", second).IsSuccess.Should().BeTrue();

        review.EditCount.Should().Be(2);
        review.UpdatedAtUtc.Should().Be(second);
        review.Ratings.Cleanliness.Should().Be(5);
        review.Ratings.Staff.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(DeletionOrigins))]
    public void Soft_delete_records_every_origin_and_hides_review(ReviewDeletionOrigin origin)
    {
        var review = DomainTestHelpers.CreateReview();
        var deletedAt = DomainTestHelpers.Now.AddMinutes(3);

        var result = review.SoftDelete(origin, "  Grund\u0000 ", deletedAt);

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.SoftDeleted);
        review.IsPublic.Should().BeFalse();
        review.CountsTowardsScores.Should().BeFalse();
        review.DeletedAtUtc.Should().Be(deletedAt);
        review.RemovedAtUtc.Should().Be(deletedAt);
        review.DeletionOrigin.Should().Be(origin);
        review.DeletionReason.Should().Be("Grund");
    }

    [Fact]
    public void Soft_delete_is_idempotent_and_keeps_original_delete_metadata()
    {
        var review = DomainTestHelpers.CreateReview();
        var first = DomainTestHelpers.Now.AddMinutes(1);
        var second = DomainTestHelpers.Now.AddMinutes(2);

        review.SoftDelete(ReviewDeletionOrigin.Author, "first", first).IsSuccess.Should().BeTrue();
        review.SoftDelete(ReviewDeletionOrigin.Admin, "second", second).IsSuccess.Should().BeTrue();

        review.DeletedAtUtc.Should().Be(first);
        review.RemovedAtUtc.Should().Be(first);
        review.DeletionOrigin.Should().Be(ReviewDeletionOrigin.Author);
        review.DeletionReason.Should().Be("first");
    }

    [Fact]
    public void Restore_clears_delete_metadata_and_returns_to_score_relevant_published_state()
    {
        var review = DomainTestHelpers.CreateReview();
        var restoredAt = DomainTestHelpers.Now.AddMinutes(2);
        review.SoftDelete(ReviewDeletionOrigin.Moderator, "reason", DomainTestHelpers.Now.AddMinutes(1));

        var result = review.Restore(restoredAt);

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        review.DeletedAtUtc.Should().BeNull();
        review.RemovedAtUtc.Should().BeNull();
        review.DeletionOrigin.Should().BeNull();
        review.DeletionReason.Should().BeNull();
        review.UpdatedAtUtc.Should().Be(restoredAt);
        review.CountsTowardsScores.Should().BeTrue();
    }

    [Theory]
    [InlineData(ReviewStatus.Published)]
    [InlineData(ReviewStatus.UnderReview)]
    [InlineData(ReviewStatus.RemovedLegal)]
    public void Restore_rejects_non_soft_deleted_states(ReviewStatus targetStatus)
    {
        var review = DomainTestHelpers.CreateReview();
        if (targetStatus == ReviewStatus.UnderReview)
        {
            review.PlaceUnderLegalReview(DomainTestHelpers.Now.AddMinutes(1));
        }
        else if (targetStatus == ReviewStatus.RemovedLegal)
        {
            review.RemoveLegal(DomainTestHelpers.Now.AddMinutes(1));
        }

        var result = review.Restore(DomainTestHelpers.Now.AddMinutes(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.notSoftDeleted");
    }

    [Fact]
    public void Removed_legal_sets_removed_at_and_blocks_edit_delete_restore()
    {
        var review = DomainTestHelpers.CreateReview();
        var removedAt = DomainTestHelpers.Now.AddMinutes(4);

        review.RemoveLegal(removedAt).IsSuccess.Should().BeTrue();

        review.Status.Should().Be(ReviewStatus.RemovedLegal);
        review.RemovedAtUtc.Should().Be(removedAt);
        review.IsPublic.Should().BeFalse();
        review.CountsTowardsScores.Should().BeFalse();
        review.Edit(new ReviewRatings { Staff = 4 }, "x", removedAt.AddMinutes(1)).IsFailure.Should().BeTrue();
        review.SoftDelete(ReviewDeletionOrigin.Admin, "x", removedAt.AddMinutes(1)).IsFailure.Should().BeTrue();
        review.Restore(removedAt.AddMinutes(1)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Legal_review_release_restores_publication_without_removed_at()
    {
        var review = DomainTestHelpers.CreateReview();
        review.PlaceUnderLegalReview(DomainTestHelpers.Now.AddMinutes(1)).IsSuccess.Should().BeTrue();

        var result = review.ReleaseFromLegalReview(DomainTestHelpers.Now.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        review.RemovedAtUtc.Should().BeNull();
        review.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Reinstate_from_legal_removal_clears_removed_at()
    {
        var review = DomainTestHelpers.CreateReview();
        review.RemoveLegal(DomainTestHelpers.Now.AddMinutes(1));

        var result = review.ReinstateFromLegalRemoval(DomainTestHelpers.Now.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        review.RemovedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Soft_deleted_review_cannot_be_placed_under_legal_review()
    {
        var review = DomainTestHelpers.CreateReview();
        review.SoftDelete(ReviewDeletionOrigin.Author, "deleted", DomainTestHelpers.Now.AddMinutes(1));

        review.PlaceUnderLegalReview(DomainTestHelpers.Now.AddMinutes(2)).IsFailure.Should().BeTrue();
    }
}
