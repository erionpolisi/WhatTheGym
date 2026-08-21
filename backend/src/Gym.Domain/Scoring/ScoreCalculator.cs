using Gym.Domain.Enums;

namespace Gym.Domain.Scoring;

public static class RatingCategories
{
    public static readonly IReadOnlyList<RatingCategory> Membership =
    [
        RatingCategory.PriceValue,
        RatingCategory.ContractTerms,
        RatingCategory.Billing,
        RatingCategory.CancellationExperience,
    ];

    public static readonly IReadOnlyList<RatingCategory> Studio =
    [
        RatingCategory.Equipment,
        RatingCategory.Cleanliness,
        RatingCategory.Staff,
        RatingCategory.Crowding,
        RatingCategory.ChangingRoom,
        RatingCategory.Showers,
        RatingCategory.Atmosphere,
    ];

    public static bool IsMembership(RatingCategory category) => Membership.Contains(category);
}

public sealed record CategoryScore(RatingCategory Category, double? Average, int RatingCount);

public sealed record GymScoreResult(
    int ReviewCount,
    double? MembershipScore,
    double? StudioScore,
    double? TotalScore,
    ScoreBasis Basis,
    IReadOnlyList<CategoryScore> Categories);

/// <summary>
/// Aggregates published review ratings. Only available data is aggregated: a category without
/// ratings is null, an area without any rated category is null, and the total is the 50/50 mean
/// of both areas only when both exist. Missing data is never treated as zero.
/// </summary>
public static class ScoreCalculator
{
    public static GymScoreResult Calculate(IReadOnlyCollection<Entities.ReviewRatings> publishedRatings)
    {
        var categories = new List<CategoryScore>(11);
        var rawAverages = new Dictionary<RatingCategory, double>();

        foreach (var category in Enum.GetValues<RatingCategory>())
        {
            var values = publishedRatings
                .Select(r => r.Get(category))
                .Where(v => v.HasValue)
                .Select(v => (double)v!.Value)
                .ToList();

            if (values.Count > 0)
            {
                var average = values.Average();
                rawAverages[category] = average;
                categories.Add(new CategoryScore(category, Round(average), values.Count));
            }
            else
            {
                categories.Add(new CategoryScore(category, null, 0));
            }
        }

        var membershipRaw = AreaAverage(rawAverages, RatingCategories.Membership);
        var studioRaw = AreaAverage(rawAverages, RatingCategories.Studio);

        double? totalRaw;
        ScoreBasis basis;
        if (membershipRaw.HasValue && studioRaw.HasValue)
        {
            totalRaw = (membershipRaw.Value + studioRaw.Value) / 2d;
            basis = ScoreBasis.Both;
        }
        else if (membershipRaw.HasValue)
        {
            totalRaw = membershipRaw;
            basis = ScoreBasis.MembershipOnly;
        }
        else if (studioRaw.HasValue)
        {
            totalRaw = studioRaw;
            basis = ScoreBasis.StudioOnly;
        }
        else
        {
            totalRaw = null;
            basis = ScoreBasis.None;
        }

        return new GymScoreResult(
            publishedRatings.Count,
            Round(membershipRaw),
            Round(studioRaw),
            Round(totalRaw),
            basis,
            categories);
    }

    private static double? AreaAverage(IReadOnlyDictionary<RatingCategory, double> rawAverages, IReadOnlyList<RatingCategory> areaCategories)
    {
        var available = areaCategories
            .Where(rawAverages.ContainsKey)
            .Select(c => rawAverages[c])
            .ToList();

        return available.Count > 0 ? available.Average() : null;
    }

    private static double? Round(double? value) =>
        value.HasValue ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;
}
