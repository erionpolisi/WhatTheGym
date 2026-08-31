using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Entities;
using Gym.Domain.Enums;

namespace Gym.Application.Tests;

internal sealed class AppFakeChainRepository : IGymChainRepository
{
    public List<GymChain> Chains { get; } = [];

    public HashSet<string> ExistingSlugs { get; } = new(StringComparer.Ordinal);

    public Dictionary<Guid, int> GymCounts { get; } = [];

    public void Add(GymChain chain)
    {
        Chains.Add(chain);
        ExistingSlugs.Add(chain.Slug);
    }

    public void Remove(GymChain chain) => Chains.Remove(chain);

    public Task<GymChain?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Chains.FirstOrDefault(c => c.Id == id));

    public Task<GymChain?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Chains.FirstOrDefault(c => c.Slug == slug));

    public Task<IReadOnlyList<GymChain>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GymChain>>(Chains.ToList());

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(ExistingSlugs.Contains(slug) || Chains.Any(c => c.Slug == slug));

    public Task<int> CountGymsAsync(Guid chainId, CancellationToken cancellationToken) =>
        Task.FromResult(GymCounts.GetValueOrDefault(chainId));
}

internal sealed class AppFakeAmenityRepository : IAmenityRepository
{
    public List<Amenity> Amenities { get; } = [];

    public HashSet<string> ExistingSlugs { get; } = new(StringComparer.Ordinal);

    public Dictionary<Guid, int> GymCounts { get; } = [];

    public void Add(Amenity amenity)
    {
        Amenities.Add(amenity);
        ExistingSlugs.Add(amenity.Slug);
    }

    public void Remove(Amenity amenity) => Amenities.Remove(amenity);

    public Task<Amenity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Amenities.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Amenity>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Amenity>>(Amenities.ToList());

    public Task<IReadOnlyList<Amenity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Amenity>>(Amenities.Where(a => ids.Contains(a.Id)).ToList());

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(ExistingSlugs.Contains(slug) || Amenities.Any(a => a.Slug == slug));

    public Task<int> CountGymsUsingAsync(Guid amenityId, CancellationToken cancellationToken) =>
        Task.FromResult(GymCounts.GetValueOrDefault(amenityId));
}

internal sealed class AppFakeGymSearchQuery : IGymSearchQuery
{
    public GymSearchCriteria? LastCriteria { get; private set; }

    public List<GymSearchRow> Rows { get; } = [];

    public Task<PagedResult<GymSearchRow>> SearchAsync(GymSearchCriteria criteria, CancellationToken cancellationToken)
    {
        LastCriteria = criteria;
        return Task.FromResult(new PagedResult<GymSearchRow>(Rows, criteria.Page, criteria.PageSize, Rows.Count));
    }
}

internal sealed class AppFakeContactRepository : IContactRequestRepository
{
    public List<ContactRequest> Requests { get; } = [];

    public void Add(ContactRequest request) => Requests.Add(request);

    public Task<ContactRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

    public Task<PagedResult<ContactRequest>> ListAsync(ContactRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = Requests.Where(r => status is null || r.Status == status).ToList();
        return Task.FromResult(new PagedResult<ContactRequest>(items, page, pageSize, items.Count));
    }
}

internal sealed class AppFakeAnalyticsStore : IAnalyticsEventStore
{
    public List<AnalyticsEvent> Events { get; } = [];

    public void Add(AnalyticsEvent analyticsEvent) => Events.Add(analyticsEvent);
}

internal sealed class AppFakeSessionBucketHasher : ISessionBucketHasher
{
    public string Hash(string sessionId) => $"hashed:{sessionId}";
}

internal sealed class AppFakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<(Guid UserId, DateTimeOffset RevokedAtUtc)> Revocations { get; } = [];

    public List<RefreshToken> Tokens { get; } = [];

    public void Add(RefreshToken token) => Tokens.Add(token);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        Revocations.Add((userId, utcNow));
        return Task.CompletedTask;
    }
}

internal sealed class AppFakeLegalHoldRepository : ILegalHoldRepository
{
    public List<LegalHold> Holds { get; } = [];

    public HashSet<Guid> HeldReviews { get; } = [];

    public HashSet<Guid> HeldUsers { get; } = [];

    public void Add(LegalHold hold) => Holds.Add(hold);

    public Task<LegalHold?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Holds.FirstOrDefault(h => h.Id == id));

    public Task<IReadOnlyList<LegalHold>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LegalHold>>(Holds.Where(h => h.IsActive).ToList());

    public Task<bool> HasActiveHoldForReviewAsync(Guid reviewId, CancellationToken cancellationToken) =>
        Task.FromResult(HeldReviews.Contains(reviewId));

    public Task<bool> HasActiveHoldForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(HeldUsers.Contains(userId));
}

internal sealed class AppFakeLegalDocumentRepository : ILegalDocumentRepository
{
    public List<LegalDocument> Documents { get; } = [];

    public void Add(LegalDocument document) => Documents.Add(document);

    public Task<LegalDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.Id == id));

    public Task<LegalDocument?> GetActiveAsync(LegalDocumentType type, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.LastOrDefault(d => d.Type == type && d.IsPublished));

    public Task<IReadOnlyList<LegalDocument>> ListVersionsAsync(LegalDocumentType type, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LegalDocument>>(Documents.Where(d => d.Type == type).OrderBy(d => d.Version).ToList());

    public Task<int> GetMaxVersionAsync(LegalDocumentType type, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.Where(d => d.Type == type).Select(d => d.Version).DefaultIfEmpty(0).Max());
}

internal sealed class AppFakePersonalDataQuery : IPersonalDataQuery
{
    public List<LegalCase> Cases { get; } = [];

    public List<ContactRequest> Contacts { get; } = [];

    public Task<IReadOnlyList<LegalCase>> ListCasesByReporterEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LegalCase>>(Cases.Where(c => c.ReporterEmail == email).ToList());

    public Task<IReadOnlyList<ContactRequest>> ListContactRequestsByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ContactRequest>>(Contacts.Where(c => c.Email == email).ToList());
}

internal static class AppTestData
{
    public static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    public static RatingsDto Ratings(int value = 4) => new(null, null, null, null, value, null, null, null, null, null, null);

    public static GymEntry Gym(string name = "Fit Wien", GymStatus status = GymStatus.Active)
    {
        return GymEntry.Create(name, global::Gym.Domain.Common.Slug.Generate(name), null, 7, "Hauptstrasse 1", "1070", null, null, null, status, Now).Value;
    }

    public static User User(string email = "user@example.at", bool verified = true)
    {
        return global::Gym.Domain.Entities.User.CreateFromGoogle($"sub-{Guid.NewGuid():N}", email, verified, "Testerin", Now);
    }

    public static Review Review(Guid? gymId = null, Guid? userId = null, ReviewStatus status = ReviewStatus.Published)
    {
        var review = global::Gym.Domain.Entities.Review.Create(gymId ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), Ratings().ToDomain(), "Solide.", Now).Value;
        if (status == ReviewStatus.SoftDeleted)
        {
            review.SoftDelete(ReviewDeletionOrigin.Author, null, Now);
        }
        else if (status == ReviewStatus.UnderReview)
        {
            review.PlaceUnderLegalReview(Now);
        }
        else if (status == ReviewStatus.RemovedLegal)
        {
            review.RemoveLegal(Now);
        }

        return review;
    }
}



