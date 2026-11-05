using Gym.Application.Abstractions;
using Gym.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence;

/// <summary>
/// Loads the whole public gym catalogue with inherited facets, amenity ids and rating summaries.
/// The catalogue is small (hundreds of rows), so the rule-based recommendation scores it in memory.
/// </summary>
public sealed class RecommendationCatalogQuery(AppDbContext context) : IRecommendationCatalogQuery
{
    public async Task<IReadOnlyList<RecommendationCatalogRow>> ListPublicGymsAsync(CancellationToken cancellationToken)
    {
        var gyms = await context.Gyms
            .AsNoTracking()
            .Include(g => g.Chain)
            .Where(g => g.Status == GymStatus.Active || g.Status == GymStatus.TemporarilyClosed)
            .ToListAsync(cancellationToken);

        var summaries = await context.GymRatingSummaries
            .AsNoTracking()
            .ToDictionaryAsync(s => s.GymId, cancellationToken);

        return gyms
            .Select(g =>
            {
                summaries.TryGetValue(g.Id, out var summary);
                return new RecommendationCatalogRow(
                    g.Id,
                    g.Name,
                    g.Slug,
                    g.District,
                    g.AddressLine,
                    g.PostalCode,
                    g.Status,
                    g.Chain?.Name,
                    g.Chain?.Slug,
                    g.EffectiveTrainingTypes,
                    g.EffectiveTargetAudience,
                    g.AmenityIds,
                    summary?.ReviewCount ?? 0,
                    summary?.TotalScore,
                    summary?.MembershipScore,
                    summary?.StudioScore,
                    summary?.ScoreBasis ?? ScoreBasis.None);
            })
            .ToList();
    }
}
