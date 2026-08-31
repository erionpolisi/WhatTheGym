using Gym.Domain.Entities;
using Gym.Domain.Enums;

namespace Gym.Domain.Tests;

internal static class DomainTestHelpers
{
    internal static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    internal static ReviewRatings Rating(RatingCategory category, int value) => category switch
    {
        RatingCategory.PriceValue => new ReviewRatings { PriceValue = value },
        RatingCategory.ContractTerms => new ReviewRatings { ContractTerms = value },
        RatingCategory.Billing => new ReviewRatings { Billing = value },
        RatingCategory.CancellationExperience => new ReviewRatings { CancellationExperience = value },
        RatingCategory.Equipment => new ReviewRatings { Equipment = value },
        RatingCategory.Cleanliness => new ReviewRatings { Cleanliness = value },
        RatingCategory.Staff => new ReviewRatings { Staff = value },
        RatingCategory.Crowding => new ReviewRatings { Crowding = value },
        RatingCategory.ChangingRoom => new ReviewRatings { ChangingRoom = value },
        RatingCategory.Showers => new ReviewRatings { Showers = value },
        RatingCategory.Atmosphere => new ReviewRatings { Atmosphere = value },
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    internal static int? Get(this ReviewRatings ratings, RatingCategory category) => ratings.Get(category);

    internal static Review CreateReview(ReviewRatings? ratings = null, string? text = "Gut.") =>
        Review.Create(Guid.NewGuid(), Guid.NewGuid(), ratings ?? new ReviewRatings { Equipment = 4 }, text, Now).Value;

    internal static LegalCase CreateLegalCase(DateTimeOffset? now = null) =>
        LegalCase.Create(
            "WTG-2026-000042",
            Guid.NewGuid(),
            LegalCaseCategory.Defamation,
            "Melderin",
            "melderin@example.com",
            new string('a', 30),
            "status-token-hash",
            now ?? Now).Value;
}
