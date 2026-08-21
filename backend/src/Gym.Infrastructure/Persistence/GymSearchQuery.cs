using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Gym.Infrastructure.Persistence;

/// <summary>Row shape returned by the raw search SQL.</summary>
public sealed class GymSearchRowFlat
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public int District { get; init; }

    public string AddressLine { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? ChainName { get; init; }

    public string? ChainSlug { get; init; }

    public int ReviewCount { get; init; }

    public double? TotalScore { get; init; }

    public double? MembershipScore { get; init; }

    public double? StudioScore { get; init; }

    public string ScoreBasis { get; init; } = "None";

    public long TotalCount { get; init; }
}

/// <summary>
/// PostgreSQL full-text (german) + trigram search over the gym catalogue,
/// joined with the materialized rating summaries for score filtering/sorting.
/// </summary>
public sealed class GymSearchQuery(AppDbContext context) : IGymSearchQuery
{
    public async Task<PagedResult<GymSearchRow>> SearchAsync(GymSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var orderBy = criteria.Sort?.ToLowerInvariant() switch
        {
            "name" => "g.\"Name\" ASC",
            "newest" => "g.\"CreatedAtUtc\" DESC",
            "score" => "s.\"TotalScore\" DESC NULLS LAST, g.\"Name\" ASC",
            _ => criteria.Term is null
                ? "s.\"TotalScore\" DESC NULLS LAST, g.\"Name\" ASC"
                : "rank DESC, g.\"Name\" ASC",
        };

        var sql = $"""
            SELECT g."Id", g."Name", g."Slug", g."District", g."AddressLine", g."PostalCode",
                   g."Status",
                   c."Name" AS "ChainName", c."Slug" AS "ChainSlug",
                   COALESCE(s."ReviewCount", 0) AS "ReviewCount",
                   s."TotalScore", s."MembershipScore", s."StudioScore",
                   COALESCE(s."ScoreBasis", 'None') AS "ScoreBasis",
                   COUNT(*) OVER() AS "TotalCount",
                   CASE WHEN @term IS NULL THEN 0
                        ELSE ts_rank(g."SearchVector", plainto_tsquery('german', @term))
                             + similarity(g."Name", @term)
                   END AS rank
            FROM "Gyms" g
            LEFT JOIN "GymChains" c ON c."Id" = g."ChainId"
            LEFT JOIN "GymRatingSummaries" s ON s."GymId" = g."Id"
            WHERE (@includeNonPublic OR g."Status" <> 'Draft')
              AND (@district IS NULL OR g."District" = @district)
              AND (@chainSlug IS NULL OR c."Slug" = @chainSlug)
              AND (@minTotal IS NULL OR s."TotalScore" >= @minTotal)
              AND (@minMembership IS NULL OR s."MembershipScore" >= @minMembership)
              AND (@minStudio IS NULL OR s."StudioScore" >= @minStudio)
              AND (@term IS NULL
                   OR g."SearchVector" @@ plainto_tsquery('german', @term)
                   OR similarity(g."Name", @term) > 0.25
                   OR (c."Name" IS NOT NULL AND c."Name" ILIKE '%' || @term || '%'))
            ORDER BY {orderBy}
            LIMIT @limit OFFSET @offset
            """;

        // Parameters carry explicit types so NULL values do not break type inference.
        var parameters = new[]
        {
            new NpgsqlParameter("term", NpgsqlDbType.Text) { Value = (object?)criteria.Term ?? DBNull.Value },
            new NpgsqlParameter("includeNonPublic", NpgsqlDbType.Boolean) { Value = criteria.IncludeNonPublic },
            new NpgsqlParameter("district", NpgsqlDbType.Integer) { Value = (object?)criteria.District ?? DBNull.Value },
            new NpgsqlParameter("chainSlug", NpgsqlDbType.Text) { Value = (object?)criteria.ChainSlug ?? DBNull.Value },
            new NpgsqlParameter("minTotal", NpgsqlDbType.Double) { Value = (object?)criteria.MinTotalScore ?? DBNull.Value },
            new NpgsqlParameter("minMembership", NpgsqlDbType.Double) { Value = (object?)criteria.MinMembershipScore ?? DBNull.Value },
            new NpgsqlParameter("minStudio", NpgsqlDbType.Double) { Value = (object?)criteria.MinStudioScore ?? DBNull.Value },
            new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = criteria.PageSize },
            new NpgsqlParameter("offset", NpgsqlDbType.Integer) { Value = (criteria.Page - 1) * criteria.PageSize },
        };

        var rows = await context.Database
            .SqlQueryRaw<GymSearchRowFlat>(sql, parameters)
            .ToListAsync(cancellationToken);

        var total = rows.Count > 0 ? (int)rows[0].TotalCount : 0;
        var items = rows
            .Select(r => new GymSearchRow(
                r.Id, r.Name, r.Slug, r.District, r.AddressLine, r.PostalCode,
                Enum.Parse<GymStatus>(r.Status),
                r.ChainName, r.ChainSlug, r.ReviewCount,
                r.TotalScore, r.MembershipScore, r.StudioScore,
                Enum.Parse<ScoreBasis>(r.ScoreBasis)))
            .ToList();

        return new PagedResult<GymSearchRow>(items, criteria.Page, criteria.PageSize, total);
    }
}

/// <summary>The tsvector column and indexes are database-generated, so indexing is a no-op.</summary>
public sealed class PostgresSearchIndex : ISearchIndex
{
    public Task IndexGymAsync(Guid gymId, CancellationToken cancellationToken) => Task.CompletedTask;
}
