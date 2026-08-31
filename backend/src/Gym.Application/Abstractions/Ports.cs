using Gym.Application.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;

namespace Gym.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Raised by the unit of work when a database uniqueness constraint is violated (mapped to HTTP 409).</summary>
public sealed class UniqueConstraintViolationException(string constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' violated.", innerException)
{
    public string ConstraintName { get; } = constraintName;
}

/// <summary>Creates a random secret token and its server-side hash (tokens are never stored in plain text).</summary>
public interface ISecureTokenService
{
    (string Token, string Hash) CreateToken();

    string Hash(string token);
}

public interface ISessionBucketHasher
{
    /// <summary>Hashes a client session id with a short-lived rotating key into an unlinkable bucket.</summary>
    string Hash(string sessionId);
}

public interface IEmailOutbox
{
    void Enqueue(OutboxEmail email);
}

/// <summary>Write-side search index port. The PostgreSQL implementation is maintained by the database.</summary>
public interface ISearchIndex
{
    Task IndexGymAsync(Guid gymId, CancellationToken cancellationToken);
}

public sealed record GymSearchCriteria(
    string? Term,
    int? District,
    string? ChainSlug,
    double? MinTotalScore,
    double? MinMembershipScore,
    double? MinStudioScore,
    string? Sort,
    int Page,
    int PageSize,
    bool IncludeNonPublic);

public sealed record GymSearchRow(
    Guid Id,
    string Name,
    string Slug,
    int District,
    string AddressLine,
    string PostalCode,
    GymStatus Status,
    string? ChainName,
    string? ChainSlug,
    int ReviewCount,
    double? TotalScore,
    double? MembershipScore,
    double? StudioScore,
    ScoreBasis ScoreBasis);

public interface IGymSearchQuery
{
    Task<PagedResult<GymSearchRow>> SearchAsync(GymSearchCriteria criteria, CancellationToken cancellationToken);
}

public interface IGymChainRepository
{
    void Add(GymChain chain);

    void Remove(GymChain chain);

    Task<GymChain?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<GymChain?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<GymChain>> ListAllAsync(CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<int> CountGymsAsync(Guid chainId, CancellationToken cancellationToken);
}

public interface IAmenityRepository
{
    void Add(Amenity amenity);

    void Remove(Amenity amenity);

    Task<Amenity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Amenity>> ListAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Amenity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<int> CountGymsUsingAsync(Guid amenityId, CancellationToken cancellationToken);
}

public interface IGymRepository
{
    void Add(GymEntry gym);

    Task<GymEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<GymEntry?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListAllIdsAsync(CancellationToken cancellationToken);
}

public interface IGymRatingSummaryStore
{
    Task<GymRatingSummary?> GetAsync(Guid gymId, CancellationToken cancellationToken);

    Task UpsertAsync(Guid gymId, GymScoreResult score, CancellationToken cancellationToken);
}

public interface IReviewRepository
{
    void Add(Review review);

    void AddRevision(ReviewRevision revision);

    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasActiveReviewAsync(Guid gymId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewRatings>> GetPublishedRatingsAsync(Guid gymId, CancellationToken cancellationToken);

    Task<PagedResult<(Review Review, string AuthorName, bool AuthorVerified)>> ListPublishedForGymAsync(
        Guid gymId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Review>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedResult<(Review Review, string GymName, string GymSlug)>> ListByStatusAsync(
        ReviewStatus status, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewRevision>> ListRevisionsAsync(Guid reviewId, CancellationToken cancellationToken);
}

public interface IUserRepository
{
    void Add(User user);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken);

    Task<PagedResult<User>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountAdminsAsync(CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken);
}

public interface ILegalCaseRepository
{
    void Add(LegalCase legalCase);

    void AddEvent(LegalCaseEvent caseEvent);

    void AddAppeal(LegalCaseAppeal appeal);

    Task<LegalCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<LegalCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken);

    Task<string> NextCaseNumberAsync(int year, CancellationToken cancellationToken);

    Task<int> NextEventSequenceAsync(Guid legalCaseId, CancellationToken cancellationToken);

    Task<PagedResult<LegalCase>> ListAsync(LegalCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalCaseEvent>> ListEventsAsync(Guid legalCaseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalCaseAppeal>> ListAppealsAsync(Guid legalCaseId, CancellationToken cancellationToken);

    Task<LegalCaseAppeal?> GetAppealByIdAsync(Guid appealId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalCase>> ListForReviewAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<TransparencyCounts> GetTransparencyCountsAsync(int year, CancellationToken cancellationToken);
}

public sealed record TransparencyCounts(
    int Year,
    int TotalReports,
    int KeptOnline,
    int FullyRemoved,
    int PendingCases,
    int FastTrackCases,
    int AppealsSubmitted,
    int AppealsReversed);

public interface ILegalHoldRepository
{
    void Add(LegalHold hold);

    Task<LegalHold?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalHold>> ListActiveAsync(CancellationToken cancellationToken);

    Task<bool> HasActiveHoldForReviewAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<bool> HasActiveHoldForUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface ILegalDocumentRepository
{
    void Add(LegalDocument document);

    Task<LegalDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<LegalDocument?> GetActiveAsync(LegalDocumentType type, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalDocument>> ListVersionsAsync(LegalDocumentType type, CancellationToken cancellationToken);

    Task<int> GetMaxVersionAsync(LegalDocumentType type, CancellationToken cancellationToken);
}

public interface IContactRequestRepository
{
    void Add(ContactRequest request);

    Task<ContactRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ContactRequest>> ListAsync(ContactRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IAnalyticsEventStore
{
    void Add(AnalyticsEvent analyticsEvent);
}

/// <summary>Read side for the personal data export (composed of everything linkable to the user).</summary>
public interface IPersonalDataQuery
{
    Task<IReadOnlyList<LegalCase>> ListCasesByReporterEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactRequest>> ListContactRequestsByEmailAsync(string email, CancellationToken cancellationToken);
}
