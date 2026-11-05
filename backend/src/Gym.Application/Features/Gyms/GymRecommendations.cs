using System.Globalization;
using Gym.Application.Abstractions;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Application.Features.Gyms;

/// <summary>
/// Rule-based gym recommendation (ADR 0014). Preferences come from the public questionnaire;
/// hard constraints filter, soft criteria score, and the result explains every match in German.
/// No AI involved: the weighting is deterministic and unit-tested.
/// </summary>
public sealed record RecommendGymsQuery(
    string? Gender,
    bool WomenOnlyRequired,
    IReadOnlyList<int>? Districts,
    IReadOnlyList<string>? TrainingTypes,
    IReadOnlyList<string>? AmenitySlugs,
    string? ScoreImportance,
    int? Limit);

public sealed class RecommendGymsQueryHandler(
    IRecommendationCatalogQuery catalog,
    IAmenityRepository amenities) : IQueryHandler<RecommendGymsQuery, IReadOnlyList<GymRecommendationDto>>
{
    private const double DistrictWeight = 30;
    private const double TrainingWeight = 25;
    private const double AmenityWeight = 20;
    private const double TemporarilyClosedFactor = 0.6;
    private const int DefaultLimit = 3;
    private const int MaxLimit = 10;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static readonly IReadOnlyDictionary<TrainingType, string> TrainingTypeLabels =
        new Dictionary<TrainingType, string>
        {
            [TrainingType.Strength] = "Krafttraining",
            [TrainingType.Cardio] = "Cardio",
            [TrainingType.Functional] = "Functional Training",
            [TrainingType.GroupClasses] = "Kurse",
            [TrainingType.CrossFit] = "CrossFit",
            [TrainingType.CircuitTraining] = "Zirkeltraining",
            [TrainingType.PersonalTraining] = "Personal Training",
            [TrainingType.Wellness] = "Wellness",
        };

    public async Task<Result<IReadOnlyList<GymRecommendationDto>>> Handle(RecommendGymsQuery query, CancellationToken cancellationToken)
    {
        var validated = Validate(query);
        if (validated.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GymRecommendationDto>>(validated.Error);
        }

        var prefs = validated.Value;
        var rows = await catalog.ListPublicGymsAsync(cancellationToken);

        var requestedAmenityIds = new List<Guid>();
        var amenityNamesById = new Dictionary<Guid, string>();
        if (prefs.AmenitySlugs.Count > 0)
        {
            // Unknown slugs are ignored so stale clients degrade instead of failing.
            foreach (var amenity in await amenities.ListAllAsync(cancellationToken))
            {
                amenityNamesById[amenity.Id] = amenity.Name;
                if (prefs.AmenitySlugs.Contains(amenity.Slug))
                {
                    requestedAmenityIds.Add(amenity.Id);
                }
            }
        }

        var scored = rows
            .Where(row => PassesHardFilters(row, prefs))
            .Select(row => ScoreGym(row, prefs, requestedAmenityIds, amenityNamesById))
            .OrderByDescending(r => r.MatchPercent)
            .ThenByDescending(r => r.Gym.TotalScore ?? double.MinValue)
            .ThenByDescending(r => r.Gym.ReviewCount)
            .ThenBy(r => r.Gym.Name, StringComparer.Create(German, ignoreCase: true))
            .Take(prefs.Limit)
            .ToList();

        return Result.Success<IReadOnlyList<GymRecommendationDto>>(scored);
    }

    private static Result<ValidatedPreferences> Validate(RecommendGymsQuery query)
    {
        var gender = query.Gender?.Trim().ToLowerInvariant() switch
        {
            null or "" => (string?)null,
            "male" => "male",
            "female" => "female",
            _ => "invalid",
        };
        if (gender == "invalid")
        {
            return Result.Failure<ValidatedPreferences>(
                Error.Validation("recommendation.gender", "Ungültige Angabe beim Geschlecht."));
        }

        if (query.WomenOnlyRequired && gender == "male")
        {
            return Result.Failure<ValidatedPreferences>(
                Error.Validation("recommendation.womenOnly", "Nur-Frauen-Studios stehen nur Frauen offen."));
        }

        var districts = new List<int>();
        foreach (var district in query.Districts ?? [])
        {
            if (district is < 1 or > 23)
            {
                return Result.Failure<ValidatedPreferences>(
                    Error.Validation("recommendation.districts", "Bezirke müssen zwischen 1 und 23 liegen."));
            }

            if (!districts.Contains(district))
            {
                districts.Add(district);
            }
        }

        var trainingTypes = TrainingType.None;
        var requestedTrainingCount = 0;
        foreach (var raw in (query.TrainingTypes ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<TrainingType>(raw, ignoreCase: true, out var parsed)
                || parsed == TrainingType.None
                || !Enum.IsDefined(parsed))
            {
                return Result.Failure<ValidatedPreferences>(
                    Error.Validation("recommendation.trainingTypes", "Unbekannte Trainingsart."));
            }

            trainingTypes |= parsed;
            requestedTrainingCount++;
        }

        var amenitySlugs = (query.AmenitySlugs ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 140)
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .Take(50)
            .ToList();

        var scoreWeight = query.ScoreImportance?.Trim().ToLowerInvariant() switch
        {
            "high" => 35d,
            null or "" or "medium" => 25d,
            "low" => 10d,
            _ => -1d,
        };
        if (scoreWeight < 0)
        {
            return Result.Failure<ValidatedPreferences>(
                Error.Validation("recommendation.scoreImportance", "Ungültige Gewichtung der Bewertungen."));
        }

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        return Result.Success(new ValidatedPreferences(
            gender,
            query.WomenOnlyRequired,
            districts,
            trainingTypes,
            requestedTrainingCount,
            amenitySlugs,
            scoreWeight,
            limit));
    }

    private static bool PassesHardFilters(RecommendationCatalogRow row, ValidatedPreferences prefs)
    {
        // Men cannot train in women-only studios; women can require them explicitly.
        if (prefs.Gender == "male" && row.TargetAudience == TargetAudience.WomenOnly)
        {
            return false;
        }

        if (prefs.WomenOnlyRequired && row.TargetAudience != TargetAudience.WomenOnly)
        {
            return false;
        }

        if (prefs.Districts.Count > 0 && !prefs.Districts.Contains(row.District))
        {
            return false;
        }

        return true;
    }

    private static GymRecommendationDto ScoreGym(
        RecommendationCatalogRow row,
        ValidatedPreferences prefs,
        IReadOnlyList<Guid> requestedAmenityIds,
        IReadOnlyDictionary<Guid, string> amenityNamesById)
    {
        double applicable = 0;
        double earned = 0;
        var reasons = new List<string>();
        var notes = new List<string>();

        if (row.TargetAudience == TargetAudience.WomenOnly)
        {
            if (prefs.Gender == "female" || prefs.WomenOnlyRequired)
            {
                reasons.Add("Reines Frauen-Studio.");
            }
            else
            {
                notes.Add("Hinweis: Dieses Studio ist nur für Frauen zugänglich.");
            }
        }

        if (prefs.Districts.Count > 0)
        {
            applicable += DistrictWeight;
            var rank = IndexOf(prefs.Districts, row.District);
            var n = prefs.Districts.Count;
            earned += DistrictWeight * (n - rank) / n;
            reasons.Add(n > 1
                ? $"Liegt im {row.District}. Bezirk (deine Priorität {rank + 1} von {n})."
                : $"Liegt in deinem Wunschbezirk ({row.District}. Bezirk).");
        }

        if (prefs.RequestedTrainingCount > 0)
        {
            applicable += TrainingWeight;
            var matched = Enum.GetValues<TrainingType>()
                .Where(t => t != TrainingType.None && prefs.TrainingTypes.HasFlag(t) && row.TrainingTypes.HasFlag(t))
                .ToList();
            earned += TrainingWeight * matched.Count / prefs.RequestedTrainingCount;
            if (matched.Count > 0)
            {
                reasons.Add($"Bietet {string.Join(", ", matched.Select(t => TrainingTypeLabels[t]))}.");
            }
            else
            {
                notes.Add("Deckt deine gewünschten Trainingsarten laut Katalog nicht ab.");
            }
        }

        if (requestedAmenityIds.Count > 0)
        {
            applicable += AmenityWeight;
            var matched = requestedAmenityIds.Where(row.AmenityIds.Contains).ToList();
            earned += AmenityWeight * matched.Count / requestedAmenityIds.Count;
            if (matched.Count > 0)
            {
                reasons.Add($"Gewünschte Ausstattung vorhanden: {string.Join(", ", matched.Select(id => amenityNamesById[id]))}.");
            }
        }

        applicable += prefs.ScoreWeight;
        if (row.TotalScore is double totalScore)
        {
            // Normalize the 1-5 community score to 0..1.
            earned += prefs.ScoreWeight * (totalScore - 1) / 4;
            reasons.Add(
                $"Gesamtnote {totalScore.ToString("0.0", German)}/5 aus {row.ReviewCount} " +
                (row.ReviewCount == 1 ? "Bewertung." : "Bewertungen."));
        }
        else
        {
            // Missing data counts as neutral midpoint, never as zero.
            earned += prefs.ScoreWeight * 0.5;
            notes.Add("Noch keine Community-Bewertungen vorhanden.");
        }

        if (row.Status == GymStatus.TemporarilyClosed)
        {
            earned *= TemporarilyClosedFactor;
            notes.Add("Derzeit vorübergehend geschlossen.");
        }

        var percent = (int)Math.Clamp(Math.Round(100 * earned / applicable), 0, 100);

        var gym = new GymListItemDto(
            row.Id,
            row.Name,
            row.Slug,
            row.District,
            row.AddressLine,
            row.PostalCode,
            row.Status.ToString(),
            row.ChainName,
            row.ChainSlug,
            row.ReviewCount,
            row.TotalScore,
            row.MembershipScore,
            row.StudioScore,
            ScoreSummaryDto.ToBasisString(row.ScoreBasis),
            CatalogFacetDtoMapper.TrainingTypes(row.TrainingTypes),
            row.TargetAudience.ToString());

        return new GymRecommendationDto(gym, percent, reasons, notes);
    }

    private static int IndexOf(IReadOnlyList<int> list, int value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed record ValidatedPreferences(
        string? Gender,
        bool WomenOnlyRequired,
        IReadOnlyList<int> Districts,
        TrainingType TrainingTypes,
        int RequestedTrainingCount,
        IReadOnlyList<string> AmenitySlugs,
        double ScoreWeight,
        int Limit);
}
