using System.Text.Json;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;

namespace Gym.Application.Features.Gyms;

public static class SummaryMapper
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    public static ScoreSummaryDto ToDto(GymRatingSummary? summary)
    {
        if (summary is null)
        {
            return ScoreSummaryDto.From(ScoreCalculator.Calculate([]));
        }

        var categories = JsonSerializer.Deserialize<List<CategoryScore>>(summary.CategoriesJson, JsonOptions) ?? [];
        var result = new GymScoreResult(
            summary.ReviewCount,
            summary.MembershipScore,
            summary.StudioScore,
            summary.TotalScore,
            summary.ScoreBasis,
            categories);
        return ScoreSummaryDto.From(result);
    }
}

public sealed record SearchGymsQuery(
    string? Term,
    int? District,
    string? ChainSlug,
    double? MinTotalScore,
    double? MinMembershipScore,
    double? MinStudioScore,
    string? Sort,
    int? Page,
    int? PageSize,
    bool IncludeNonPublic = false);

public sealed class SearchGymsQueryHandler(IGymSearchQuery search) : IQueryHandler<SearchGymsQuery, PagedResult<GymListItemDto>>
{
    public async Task<Result<PagedResult<GymListItemDto>>> Handle(SearchGymsQuery query, CancellationToken cancellationToken)
    {
        if (query.District is int district && district is < 1 or > 23)
        {
            return Result.Failure<PagedResult<GymListItemDto>>(Error.Validation("search.district", "Der Bezirk muss zwischen 1 und 23 liegen."));
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var criteria = new GymSearchCriteria(
            string.IsNullOrWhiteSpace(query.Term) ? null : query.Term.Trim(),
            query.District,
            string.IsNullOrWhiteSpace(query.ChainSlug) ? null : query.ChainSlug.Trim(),
            query.MinTotalScore,
            query.MinMembershipScore,
            query.MinStudioScore,
            query.Sort,
            page,
            pageSize,
            query.IncludeNonPublic);

        var rows = await search.SearchAsync(criteria, cancellationToken);
        var items = rows.Items
            .Select(r => new GymListItemDto(
                r.Id, r.Name, r.Slug, r.District, r.AddressLine, r.PostalCode, r.Status.ToString(),
                r.ChainName, r.ChainSlug, r.ReviewCount, r.TotalScore, r.MembershipScore, r.StudioScore,
                ScoreSummaryDto.ToBasisString(r.ScoreBasis)))
            .ToList();

        return new PagedResult<GymListItemDto>(items, rows.Page, rows.PageSize, rows.TotalCount);
    }
}

public sealed record GetGymBySlugQuery(string Slug, bool IncludeNonPublic = false);

public sealed class GetGymBySlugQueryHandler(
    IGymRepository gyms,
    IAmenityRepository amenities,
    IGymRatingSummaryStore summaries) : IQueryHandler<GetGymBySlugQuery, GymDetailDto>
{
    public async Task<Result<GymDetailDto>> Handle(GetGymBySlugQuery query, CancellationToken cancellationToken)
    {
        var gym = await gyms.GetBySlugAsync(query.Slug, cancellationToken);
        if (gym is null || (!gym.IsPubliclyVisible && !query.IncludeNonPublic))
        {
            return Result.Failure<GymDetailDto>(Error.NotFound("gym.notFound", "Das Studio wurde nicht gefunden."));
        }

        var gymAmenities = await amenities.GetByIdsAsync(gym.AmenityIds, cancellationToken);
        var summary = await summaries.GetAsync(gym.Id, cancellationToken);

        var dto = new GymDetailDto(
            gym.Id,
            gym.Name,
            gym.Slug,
            gym.District,
            gym.AddressLine,
            gym.PostalCode,
            gym.City,
            gym.CountryCode,
            gym.Website,
            gym.Phone,
            gym.Description,
            gym.Status.ToString(),
            gym.Chain is null ? null : new ChainDto(gym.Chain.Id, gym.Chain.Name, gym.Chain.Slug, gym.Chain.Website),
            gymAmenities.Select(a => new AmenityDto(a.Id, a.Name, a.Slug)).ToList(),
            gym.OpeningHours
                .OrderBy(h => h.IsoDayOfWeek)
                .Select(h => new OpeningHourDto(h.IsoDayOfWeek, h.OpensAt.ToString("HH:mm"), h.ClosesAt.ToString("HH:mm")))
                .ToList(),
            SummaryMapper.ToDto(summary),
            gym.CreatedAtUtc,
            gym.UpdatedAtUtc);

        return dto;
    }
}

public sealed record GetGymSummaryQuery(string Slug);

public sealed class GetGymSummaryQueryHandler(
    IGymRepository gyms,
    IGymRatingSummaryStore summaries) : IQueryHandler<GetGymSummaryQuery, ScoreSummaryDto>
{
    public async Task<Result<ScoreSummaryDto>> Handle(GetGymSummaryQuery query, CancellationToken cancellationToken)
    {
        var gym = await gyms.GetBySlugAsync(query.Slug, cancellationToken);
        if (gym is null || !gym.IsPubliclyVisible)
        {
            return Result.Failure<ScoreSummaryDto>(Error.NotFound("gym.notFound", "Das Studio wurde nicht gefunden."));
        }

        var summary = await summaries.GetAsync(gym.Id, cancellationToken);
        return SummaryMapper.ToDto(summary);
    }
}
