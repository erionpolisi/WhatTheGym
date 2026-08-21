using System.Text.Json;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence;

public sealed class GymChainRepository(AppDbContext context) : IGymChainRepository
{
    public void Add(GymChain chain) => context.GymChains.Add(chain);

    public void Remove(GymChain chain) => context.GymChains.Remove(chain);

    public Task<GymChain?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.GymChains.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<GymChain?> GetBySlugAsync(string slug, CancellationToken ct) =>
        context.GymChains.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<IReadOnlyList<GymChain>> ListAllAsync(CancellationToken ct) =>
        await context.GymChains.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        context.GymChains.AnyAsync(c => c.Slug == slug, ct);

    public Task<int> CountGymsAsync(Guid chainId, CancellationToken ct) =>
        context.Gyms.CountAsync(g => g.ChainId == chainId, ct);
}

public sealed class AmenityRepository(AppDbContext context) : IAmenityRepository
{
    public void Add(Amenity amenity) => context.Amenities.Add(amenity);

    public void Remove(Amenity amenity) => context.Amenities.Remove(amenity);

    public Task<Amenity?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Amenities.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Amenity>> ListAllAsync(CancellationToken ct) =>
        await context.Amenities.AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Amenity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? []
            : await context.Amenities.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.Name).ToListAsync(ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        context.Amenities.AnyAsync(a => a.Slug == slug, ct);

    public Task<int> CountGymsUsingAsync(Guid amenityId, CancellationToken ct) =>
        context.Gyms.CountAsync(g => g.AmenityIds.Contains(amenityId), ct);
}

public sealed class GymRepository(AppDbContext context) : IGymRepository
{
    public void Add(GymEntry gym) => context.Gyms.Add(gym);

    public Task<GymEntry?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Gyms.Include(g => g.Chain).Include(g => g.OpeningHours).FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<GymEntry?> GetBySlugAsync(string slug, CancellationToken ct) =>
        context.Gyms.Include(g => g.Chain).Include(g => g.OpeningHours).FirstOrDefaultAsync(g => g.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        context.Gyms.AnyAsync(g => g.Slug == slug, ct);

    public async Task<IReadOnlyList<Guid>> ListAllIdsAsync(CancellationToken ct) =>
        await context.Gyms.Select(g => g.Id).ToListAsync(ct);
}

public sealed class GymRatingSummaryStore(AppDbContext context, IClock clock) : IGymRatingSummaryStore
{
    public Task<GymRatingSummary?> GetAsync(Guid gymId, CancellationToken ct) =>
        context.GymRatingSummaries.FirstOrDefaultAsync(s => s.GymId == gymId, ct);

    public async Task UpsertAsync(Guid gymId, GymScoreResult score, CancellationToken ct)
    {
        var categoriesJson = JsonSerializer.Serialize(score.Categories, AppDbContext.JsonOptions);
        var existing = await context.GymRatingSummaries.FirstOrDefaultAsync(s => s.GymId == gymId, ct);
        if (existing is null)
        {
            context.GymRatingSummaries.Add(GymRatingSummary.Create(gymId, score, categoriesJson, clock.UtcNow));
        }
        else
        {
            existing.Apply(score, categoriesJson, clock.UtcNow);
        }
    }
}

public sealed class ReviewRepository(AppDbContext context) : IReviewRepository
{
    public void Add(Review review) => context.Reviews.Add(review);

    public void AddRevision(ReviewRevision revision) => context.ReviewRevisions.Add(revision);

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> HasActiveReviewAsync(Guid gymId, Guid userId, CancellationToken ct) =>
        context.Reviews.AnyAsync(
            r => r.GymId == gymId && r.UserId == userId
                 && (r.Status == ReviewStatus.Published || r.Status == ReviewStatus.UnderReview), ct);

    /// <summary>
    /// Ratings of published reviews including pending (tracked but unsaved) changes, so score
    /// recalculation inside a unit of work sees the final state before SaveChanges.
    /// </summary>
    public async Task<IReadOnlyList<ReviewRatings>> GetPublishedRatingsAsync(Guid gymId, CancellationToken ct)
    {
        var localReviews = context.Reviews.Local.Where(r => r.GymId == gymId).ToList();
        var localIds = localReviews.Select(r => r.Id).ToList();

        var dbRatings = await context.Reviews.AsNoTracking()
            .Where(r => r.GymId == gymId && r.Status == ReviewStatus.Published && !localIds.Contains(r.Id))
            .Select(r => r.Ratings)
            .ToListAsync(ct);

        var localRatings = localReviews
            .Where(r => r.Status == ReviewStatus.Published)
            .Select(r => r.Ratings);

        return dbRatings.Concat(localRatings).ToList();
    }

    public async Task<PagedResult<(Review Review, string AuthorName, bool AuthorVerified)>> ListPublishedForGymAsync(
        Guid gymId, int page, int pageSize, CancellationToken ct)
    {
        var query = context.Reviews.AsNoTracking()
            .Where(r => r.GymId == gymId && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Join(context.Users, r => r.UserId, u => u.Id, (r, u) => new { Review = r, u.DisplayName, u.EmailVerified, u.Status });

        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows
            .Select(x => (x.Review, x.DisplayName, x.EmailVerified && x.Status == UserStatus.Active))
            .ToList();
        return new PagedResult<(Review, string, bool)>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<Review>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        await context.Reviews.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);

    public async Task<PagedResult<(Review Review, string GymName, string GymSlug)>> ListByStatusAsync(
        ReviewStatus status, int page, int pageSize, CancellationToken ct)
    {
        var query = context.Reviews.AsNoTracking()
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Join(context.Gyms, r => r.GymId, g => g.Id, (r, g) => new { Review = r, g.Name, g.Slug });

        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(x => (x.Review, x.Name, x.Slug)).ToList();
        return new PagedResult<(Review, string, string)>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ReviewRevision>> ListRevisionsAsync(Guid reviewId, CancellationToken ct) =>
        await context.ReviewRevisions.AsNoTracking()
            .Where(r => r.ReviewId == reviewId)
            .OrderBy(r => r.Version)
            .ToListAsync(ct);
}

public sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public void Add(User user) => context.Users.Add(user);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByGoogleSubjectAsync(string googleSubject, CancellationToken ct) =>
        context.Users.FirstOrDefaultAsync(u => u.GoogleSubject == googleSubject, ct);

    public async Task<PagedResult<User>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = context.Users.AsNoTracking().OrderBy(u => u.CreatedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<User>(items, page, pageSize, total);
    }

    public Task<int> CountAdminsAsync(CancellationToken ct) =>
        context.Users.CountAsync(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active, ct);
}

public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset utcNow, CancellationToken ct)
    {
        var active = await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in active)
        {
            token.Revoke(utcNow);
        }
    }
}

public sealed class LegalCaseRepository(AppDbContext context) : ILegalCaseRepository
{
    public void Add(LegalCase legalCase) => context.LegalCases.Add(legalCase);

    public void AddEvent(LegalCaseEvent caseEvent) => context.LegalCaseEvents.Add(caseEvent);

    public void AddAppeal(LegalCaseAppeal appeal) => context.LegalCaseAppeals.Add(appeal);

    public Task<LegalCase?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.LegalCases.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<LegalCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken ct) =>
        context.LegalCases.FirstOrDefaultAsync(c => c.CaseNumber == caseNumber, ct);

    public async Task<string> NextCaseNumberAsync(int year, CancellationToken ct)
    {
        var result = await context.Database
            .SqlQuery<long>($"SELECT nextval('legal_case_seq') AS \"Value\"")
            .ToListAsync(ct);
        return $"WTG-{year}-{result[0]:D6}";
    }

    public async Task<int> NextEventSequenceAsync(Guid legalCaseId, CancellationToken ct)
    {
        var dbMax = await context.LegalCaseEvents
            .Where(e => e.LegalCaseId == legalCaseId)
            .MaxAsync(e => (int?)e.Sequence, ct) ?? 0;
        var localMax = context.LegalCaseEvents.Local
            .Where(e => e.LegalCaseId == legalCaseId)
            .Select(e => (int?)e.Sequence)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        return Math.Max(dbMax, localMax) + 1;
    }

    public async Task<PagedResult<LegalCase>> ListAsync(LegalCaseStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = context.LegalCases.AsNoTracking().AsQueryable();
        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        query = query.OrderByDescending(c => c.CreatedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<LegalCase>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<LegalCaseEvent>> ListEventsAsync(Guid legalCaseId, CancellationToken ct) =>
        await context.LegalCaseEvents.AsNoTracking()
            .Where(e => e.LegalCaseId == legalCaseId)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LegalCaseAppeal>> ListAppealsAsync(Guid legalCaseId, CancellationToken ct) =>
        await context.LegalCaseAppeals.AsNoTracking()
            .Where(a => a.LegalCaseId == legalCaseId)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<LegalCaseAppeal?> GetAppealByIdAsync(Guid appealId, CancellationToken ct) =>
        context.LegalCaseAppeals.FirstOrDefaultAsync(a => a.Id == appealId, ct);

    public async Task<IReadOnlyList<LegalCase>> ListForReviewAsync(Guid reviewId, CancellationToken ct) =>
        await context.LegalCases.AsNoTracking().Where(c => c.ReviewId == reviewId).ToListAsync(ct);

    public async Task<TransparencyCounts> GetTransparencyCountsAsync(int year, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddYears(1);
        var cases = context.LegalCases.AsNoTracking().Where(c => c.CreatedAtUtc >= from && c.CreatedAtUtc < to);

        var total = await cases.CountAsync(ct);
        var kept = await cases.CountAsync(c => c.Decision == LegalDecision.KeepOnline, ct);
        var removed = await cases.CountAsync(c => c.Decision == LegalDecision.FullyRemoved, ct);
        var pending = await cases.CountAsync(c => c.Status == LegalCaseStatus.Received || c.Status == LegalCaseStatus.UnderReview, ct);
        var fastTrack = await cases.CountAsync(c => c.Classification == LegalCaseClassification.FastTrackObviouslyIllegal, ct);

        var appeals = context.LegalCaseAppeals.AsNoTracking()
            .Join(cases, a => a.LegalCaseId, c => c.Id, (a, c) => a);
        var appealsSubmitted = await appeals.CountAsync(ct);
        var appealsReversed = await appeals.CountAsync(a => a.Outcome == AppealOutcome.DecisionReversed, ct);

        return new TransparencyCounts(year, total, kept, removed, pending, fastTrack, appealsSubmitted, appealsReversed);
    }
}

public sealed class LegalHoldRepository(AppDbContext context) : ILegalHoldRepository
{
    public void Add(LegalHold hold) => context.LegalHolds.Add(hold);

    public Task<LegalHold?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.LegalHolds.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<IReadOnlyList<LegalHold>> ListActiveAsync(CancellationToken ct) =>
        await context.LegalHolds.AsNoTracking().Where(h => h.ReleasedAtUtc == null).ToListAsync(ct);

    public Task<bool> HasActiveHoldForReviewAsync(Guid reviewId, CancellationToken ct) =>
        context.LegalHolds.AnyAsync(h => h.ReviewId == reviewId && h.ReleasedAtUtc == null, ct);

    public Task<bool> HasActiveHoldForUserAsync(Guid userId, CancellationToken ct) =>
        context.LegalHolds.AnyAsync(h => h.UserId == userId && h.ReleasedAtUtc == null, ct);
}

public sealed class LegalDocumentRepository(AppDbContext context) : ILegalDocumentRepository
{
    public void Add(LegalDocument document) => context.LegalDocuments.Add(document);

    public Task<LegalDocument?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.LegalDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<LegalDocument?> GetActiveAsync(LegalDocumentType type, CancellationToken ct) =>
        context.LegalDocuments.AsNoTracking()
            .Where(d => d.Type == type && d.IsPublished)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LegalDocument>> ListVersionsAsync(LegalDocumentType type, CancellationToken ct) =>
        await context.LegalDocuments.AsNoTracking()
            .Where(d => d.Type == type)
            .OrderByDescending(d => d.Version)
            .ToListAsync(ct);

    public async Task<int> GetMaxVersionAsync(LegalDocumentType type, CancellationToken ct) =>
        await context.LegalDocuments.Where(d => d.Type == type).MaxAsync(d => (int?)d.Version, ct) ?? 0;
}

public sealed class ContactRequestRepository(AppDbContext context) : IContactRequestRepository
{
    public void Add(ContactRequest request) => context.ContactRequests.Add(request);

    public Task<ContactRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.ContactRequests.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<PagedResult<ContactRequest>> ListAsync(ContactRequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = context.ContactRequests.AsNoTracking().AsQueryable();
        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        query = query.OrderByDescending(c => c.CreatedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ContactRequest>(items, page, pageSize, total);
    }
}

public sealed class AnalyticsEventStore(AppDbContext context) : IAnalyticsEventStore
{
    public void Add(AnalyticsEvent analyticsEvent) => context.AnalyticsEvents.Add(analyticsEvent);
}

public sealed class PersonalDataQuery(AppDbContext context) : IPersonalDataQuery
{
    public async Task<IReadOnlyList<LegalCase>> ListCasesByReporterEmailAsync(string email, CancellationToken ct) =>
        await context.LegalCases.AsNoTracking().Where(c => c.ReporterEmail == email).ToListAsync(ct);

    public async Task<IReadOnlyList<ContactRequest>> ListContactRequestsByEmailAsync(string email, CancellationToken ct) =>
        await context.ContactRequests.AsNoTracking().Where(c => c.Email == email).ToListAsync(ct);
}

public sealed class EmailOutbox(AppDbContext context) : IEmailOutbox
{
    public void Enqueue(OutboxEmail email) => context.OutboxEmails.Add(email);
}
