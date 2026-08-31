using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Domain.Entities;

/// <summary>All direct 1-5 category ratings of a review. Null means "not rated"; never zero.</summary>
public sealed class ReviewRatings
{
    public int? PriceValue { get; init; }

    public int? ContractTerms { get; init; }

    public int? Billing { get; init; }

    public int? CancellationExperience { get; init; }

    public int? Equipment { get; init; }

    public int? Cleanliness { get; init; }

    public int? Staff { get; init; }

    public int? Crowding { get; init; }

    public int? ChangingRoom { get; init; }

    public int? Showers { get; init; }

    public int? Atmosphere { get; init; }

    public int? Get(RatingCategory category) => category switch
    {
        RatingCategory.PriceValue => PriceValue,
        RatingCategory.ContractTerms => ContractTerms,
        RatingCategory.Billing => Billing,
        RatingCategory.CancellationExperience => CancellationExperience,
        RatingCategory.Equipment => Equipment,
        RatingCategory.Cleanliness => Cleanliness,
        RatingCategory.Staff => Staff,
        RatingCategory.Crowding => Crowding,
        RatingCategory.ChangingRoom => ChangingRoom,
        RatingCategory.Showers => Showers,
        RatingCategory.Atmosphere => Atmosphere,
        _ => null,
    };

    public IEnumerable<(RatingCategory Category, int Value)> Provided()
    {
        foreach (var category in Enum.GetValues<RatingCategory>())
        {
            if (Get(category) is int value)
            {
                yield return (category, value);
            }
        }
    }

    public bool HasAnyRating => Provided().Any();

    public bool AllWithinRange => Provided().All(p => p.Value is >= 1 and <= 5);
}

public sealed class Review : Entity
{
    public const int MaxTextLength = 4000;

    private Review()
    {
        Ratings = null!;
    }

    public Guid GymId { get; private set; }

    public Guid UserId { get; private set; }

    public ReviewRatings Ratings { get; private set; }

    public string? Text { get; private set; }

    public ReviewStatus Status { get; private set; }

    public int EditCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public ReviewDeletionOrigin? DeletionOrigin { get; private set; }

    public string? DeletionReason { get; private set; }

    /// <summary>Set when the review left public visibility for good (basis for revision retention).</summary>
    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public bool IsPublic => Status == ReviewStatus.Published;

    public bool CountsTowardsScores => Status == ReviewStatus.Published;

    public static Result<Review> Create(Guid gymId, Guid userId, ReviewRatings ratings, string? text, DateTimeOffset utcNow)
    {
        var ratingsError = ValidateRatings(ratings);
        if (ratingsError is not null)
        {
            return Result.Failure<Review>(ratingsError);
        }

        var sanitized = TextSanitizer.Sanitize(text);
        if (sanitized is { Length: > MaxTextLength })
        {
            return Result.Failure<Review>(Error.Validation("review.text", $"Text must not exceed {MaxTextLength} characters."));
        }

        return new Review
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            UserId = userId,
            Ratings = ratings,
            Text = sanitized,
            Status = ReviewStatus.Published,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public Result Edit(ReviewRatings ratings, string? text, DateTimeOffset utcNow)
    {
        if (Status is ReviewStatus.RemovedLegal or ReviewStatus.UnderReview)
        {
            return Result.Failure(Error.Conflict("review.locked", "Review cannot be edited in its current state."));
        }

        var ratingsError = ValidateRatings(ratings);
        if (ratingsError is not null)
        {
            return Result.Failure(ratingsError);
        }

        var sanitized = TextSanitizer.Sanitize(text);
        if (sanitized is { Length: > MaxTextLength })
        {
            return Result.Failure(Error.Validation("review.text", $"Text must not exceed {MaxTextLength} characters."));
        }

        Ratings = ratings;
        Text = sanitized;
        EditCount++;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result SoftDelete(ReviewDeletionOrigin origin, string? reason, DateTimeOffset utcNow)
    {
        if (Status == ReviewStatus.RemovedLegal)
        {
            return Result.Failure(Error.Conflict("review.removedLegal", "Legally removed reviews cannot be modified."));
        }

        if (Status == ReviewStatus.SoftDeleted)
        {
            return Result.Success();
        }

        Status = ReviewStatus.SoftDeleted;
        DeletedAtUtc = utcNow;
        RemovedAtUtc = utcNow;
        DeletionOrigin = origin;
        DeletionReason = TextSanitizer.Sanitize(reason);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Restore(DateTimeOffset utcNow)
    {
        if (Status != ReviewStatus.SoftDeleted)
        {
            return Result.Failure(Error.Conflict("review.notSoftDeleted", "Only soft deleted reviews can be restored."));
        }

        Status = ReviewStatus.Published;
        DeletedAtUtc = null;
        RemovedAtUtc = null;
        DeletionOrigin = null;
        DeletionReason = null;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Fast-track only: temporarily hides content while an obviously-illegal report is decided.</summary>
    public Result PlaceUnderLegalReview(DateTimeOffset utcNow)
    {
        if (Status == ReviewStatus.UnderReview)
        {
            return Result.Success();
        }

        // Only a published review may be hidden. Anything else (soft deleted, legally removed)
        // must keep its state: releasing it later would otherwise resurrect deleted content.
        if (Status != ReviewStatus.Published)
        {
            return Result.Failure(Error.Conflict("review.notPublished", "Only published reviews can be placed under review."));
        }

        Status = ReviewStatus.UnderReview;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result ReleaseFromLegalReview(DateTimeOffset utcNow)
    {
        if (Status != ReviewStatus.UnderReview)
        {
            return Result.Failure(Error.Conflict("review.notUnderReview", "Review is not under review."));
        }

        Status = ReviewStatus.Published;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result RemoveLegal(DateTimeOffset utcNow)
    {
        if (Status == ReviewStatus.RemovedLegal)
        {
            return Result.Success();
        }

        Status = ReviewStatus.RemovedLegal;
        RemovedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Reinstates a legally removed review after a successful appeal.</summary>
    public Result ReinstateFromLegalRemoval(DateTimeOffset utcNow)
    {
        if (Status != ReviewStatus.RemovedLegal)
        {
            return Result.Failure(Error.Conflict("review.notRemoved", "Only legally removed reviews can be reinstated."));
        }

        Status = ReviewStatus.Published;
        RemovedAtUtc = null;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    private static Error? ValidateRatings(ReviewRatings ratings)
    {
        if (!ratings.HasAnyRating)
        {
            return Error.Validation("review.ratings", "At least one category rating (1-5) is required.");
        }

        if (!ratings.AllWithinRange)
        {
            return Error.Validation("review.ratings.range", "All ratings must be between 1 and 5.");
        }

        return null;
    }
}

/// <summary>Immutable snapshot of a review state before an edit.</summary>
public sealed class ReviewRevision : Entity
{
    private ReviewRevision()
    {
        RatingsJson = null!;
    }

    public Guid ReviewId { get; private set; }

    public int Version { get; private set; }

    public string? TextSnapshot { get; private set; }

    public string RatingsJson { get; private set; }

    public Guid EditedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ReviewRevision Create(Guid reviewId, int version, string? textSnapshot, string ratingsJson, Guid editedByUserId, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        ReviewId = reviewId,
        Version = version,
        TextSnapshot = textSnapshot,
        RatingsJson = ratingsJson,
        EditedByUserId = editedByUserId,
        CreatedAtUtc = utcNow,
    };
}
