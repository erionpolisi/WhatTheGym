using Gym.Domain.Enums;
using Gym.Domain.Scoring;

namespace Gym.Domain.Entities;

/// <summary>Materialized rating aggregate per gym, rebuilt whenever score-relevant reviews change.</summary>
public sealed class GymRatingSummary
{
    private GymRatingSummary()
    {
        CategoriesJson = null!;
    }

    public Guid GymId { get; private set; }

    public int ReviewCount { get; private set; }

    public double? MembershipScore { get; private set; }

    public double? StudioScore { get; private set; }

    public double? TotalScore { get; private set; }

    public ScoreBasis ScoreBasis { get; private set; }

    /// <summary>Serialized per-category averages and counts (see <see cref="CategoryScore"/>).</summary>
    public string CategoriesJson { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static GymRatingSummary Create(Guid gymId, GymScoreResult score, string categoriesJson, DateTimeOffset utcNow) => new()
    {
        GymId = gymId,
        ReviewCount = score.ReviewCount,
        MembershipScore = score.MembershipScore,
        StudioScore = score.StudioScore,
        TotalScore = score.TotalScore,
        ScoreBasis = score.Basis,
        CategoriesJson = categoriesJson,
        UpdatedAtUtc = utcNow,
    };

    public void Apply(GymScoreResult score, string categoriesJson, DateTimeOffset utcNow)
    {
        ReviewCount = score.ReviewCount;
        MembershipScore = score.MembershipScore;
        StudioScore = score.StudioScore;
        TotalScore = score.TotalScore;
        ScoreBasis = score.Basis;
        CategoriesJson = categoriesJson;
        UpdatedAtUtc = utcNow;
    }
}
