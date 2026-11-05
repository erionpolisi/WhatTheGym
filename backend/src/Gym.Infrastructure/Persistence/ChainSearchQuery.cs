using Gym.Application.Abstractions;
using Gym.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Gym.Infrastructure.Persistence;

/// <summary>Row shape returned by the raw chain search SQL.</summary>
public sealed class ChainSearchRowFlat
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Website { get; init; }

    public int TrainingTypes { get; init; }

    public string TargetAudience { get; init; } = "General";

    public int GymCount { get; init; }

    public int ReviewCount { get; init; }

    public double? AverageScore { get; init; }
}

/// <summary>
/// Chain-first directory search: groups the public gym catalogue by chain and reports how many
/// gyms of each chain match the current term/district/score filters. Term matching reuses the
/// gym full-text/trigram semantics plus the chain name, so a chain, gym or address hit keeps
/// the same results as the flat gym search.
/// </summary>
public sealed class ChainSearchQuery(AppDbContext context) : IChainSearchQuery
{
    public async Task<IReadOnlyList<ChainSearchRow>> SearchAsync(ChainSearchCriteria criteria, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c."Id", c."Name", c."Slug", c."Website",
                   c."TrainingTypes",
                   c."TargetAudience",
                   COUNT(g."Id")::int AS "GymCount",
                   COALESCE(SUM(s."ReviewCount"), 0)::int AS "ReviewCount",
                   AVG(s."TotalScore") AS "AverageScore"
            FROM "GymChains" c
            JOIN "Gyms" g ON g."ChainId" = c."Id"
            LEFT JOIN "GymRatingSummaries" s ON s."GymId" = g."Id"
            WHERE g."Status" <> 'Draft'
              AND (@district IS NULL OR g."District" = @district)
              AND (@minTotal IS NULL OR s."TotalScore" >= @minTotal)
              AND (@term IS NULL
                   OR g."SearchVector" @@ plainto_tsquery('german', @term)
                   OR similarity(g."Name", @term) > 0.25
                   OR c."Name" ILIKE '%' || @term || '%')
            GROUP BY c."Id", c."Name", c."Slug", c."Website", c."TrainingTypes", c."TargetAudience"
            ORDER BY AVG(s."TotalScore") DESC NULLS LAST, COUNT(g."Id") DESC, c."Name" ASC
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("term", NpgsqlDbType.Text) { Value = (object?)criteria.Term ?? DBNull.Value },
            new NpgsqlParameter("district", NpgsqlDbType.Integer) { Value = (object?)criteria.District ?? DBNull.Value },
            new NpgsqlParameter("minTotal", NpgsqlDbType.Double) { Value = (object?)criteria.MinTotalScore ?? DBNull.Value },
        };

        var rows = await context.Database
            .SqlQueryRaw<ChainSearchRowFlat>(sql, parameters)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ChainSearchRow(
                r.Id,
                r.Name,
                r.Slug,
                r.Website,
                (TrainingType)r.TrainingTypes,
                Enum.Parse<TargetAudience>(r.TargetAudience),
                r.GymCount,
                r.ReviewCount,
                r.AverageScore))
            .ToList();
    }
}
