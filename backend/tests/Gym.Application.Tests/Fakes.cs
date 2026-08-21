using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;

namespace Gym.Application.Tests;

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

public sealed class FakeTokenService : ISecureTokenService
{
    private int _counter;

    public (string Token, string Hash) CreateToken()
    {
        _counter++;
        return ($"token-{_counter}", $"hash-token-{_counter}");
    }

    public string Hash(string token) => $"hash-{token}";
}

public sealed class FakeOutbox : IEmailOutbox
{
    public List<OutboxEmail> Sent { get; } = [];

    public void Enqueue(OutboxEmail email) => Sent.Add(email);
}

public sealed class FakeSearchIndex : ISearchIndex
{
    public Task IndexGymAsync(Guid gymId, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public void Add(User user) => Users.Add(user);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByGoogleSubjectAsync(string googleSubject, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => u.GoogleSubject == googleSubject));

    public Task<PagedResult<User>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        Task.FromResult(new PagedResult<User>(Users, page, pageSize, Users.Count));

    public Task<int> CountAdminsAsync(CancellationToken ct) =>
        Task.FromResult(Users.Count(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active));
}

public sealed class InMemoryGymRepository : IGymRepository
{
    public List<GymEntry> Gyms { get; } = [];

    public void Add(GymEntry gym) => Gyms.Add(gym);

    public Task<GymEntry?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Gyms.FirstOrDefault(g => g.Id == id));

    public Task<GymEntry?> GetBySlugAsync(string slug, CancellationToken ct) =>
        Task.FromResult(Gyms.FirstOrDefault(g => g.Slug == slug));

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        Task.FromResult(Gyms.Any(g => g.Slug == slug));

    public Task<IReadOnlyList<Guid>> ListAllIdsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Gyms.Select(g => g.Id).ToList());
}

public sealed class InMemoryReviewRepository : IReviewRepository
{
    public List<Review> Reviews { get; } = [];

    public List<ReviewRevision> Revisions { get; } = [];

    public void Add(Review review) => Reviews.Add(review);

    public void AddRevision(ReviewRevision revision) => Revisions.Add(revision);

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Reviews.FirstOrDefault(r => r.Id == id));

    public Task<bool> HasActiveReviewAsync(Guid gymId, Guid userId, CancellationToken ct) =>
        Task.FromResult(Reviews.Any(r => r.GymId == gymId && r.UserId == userId
            && r.Status is ReviewStatus.Published or ReviewStatus.UnderReview));

    public Task<IReadOnlyList<ReviewRatings>> GetPublishedRatingsAsync(Guid gymId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReviewRatings>>(
            Reviews.Where(r => r.GymId == gymId && r.Status == ReviewStatus.Published).Select(r => r.Ratings).ToList());

    public Task<PagedResult<(Review Review, string AuthorName, bool AuthorVerified)>> ListPublishedForGymAsync(
        Guid gymId, int page, int pageSize, CancellationToken ct)
    {
        var items = Reviews.Where(r => r.GymId == gymId && r.Status == ReviewStatus.Published)
            .Select(r => (r, "Autor", true)).ToList();
        return Task.FromResult(new PagedResult<(Review, string, bool)>(items, page, pageSize, items.Count));
    }

    public Task<IReadOnlyList<Review>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Review>>(Reviews.Where(r => r.UserId == userId).ToList());

    public Task<PagedResult<(Review Review, string GymName, string GymSlug)>> ListByStatusAsync(
        ReviewStatus status, int page, int pageSize, CancellationToken ct)
    {
        var items = Reviews.Where(r => r.Status == status).Select(r => (r, "Gym", "gym")).ToList();
        return Task.FromResult(new PagedResult<(Review, string, string)>(items, page, pageSize, items.Count));
    }

    public Task<IReadOnlyList<ReviewRevision>> ListRevisionsAsync(Guid reviewId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReviewRevision>>(Revisions.Where(r => r.ReviewId == reviewId).ToList());
}

public sealed class InMemorySummaryStore : IGymRatingSummaryStore
{
    public Dictionary<Guid, GymScoreResult> Scores { get; } = [];

    public Task<GymRatingSummary?> GetAsync(Guid gymId, CancellationToken ct) =>
        Task.FromResult<GymRatingSummary?>(null);

    public Task UpsertAsync(Guid gymId, GymScoreResult score, CancellationToken ct)
    {
        Scores[gymId] = score;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryLegalCaseRepository : ILegalCaseRepository
{
    public List<LegalCase> Cases { get; } = [];

    public List<LegalCaseEvent> Events { get; } = [];

    public List<LegalCaseAppeal> Appeals { get; } = [];

    private int _sequence;

    public void Add(LegalCase legalCase) => Cases.Add(legalCase);

    public void AddEvent(LegalCaseEvent caseEvent) => Events.Add(caseEvent);

    public void AddAppeal(LegalCaseAppeal appeal) => Appeals.Add(appeal);

    public Task<LegalCase?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Cases.FirstOrDefault(c => c.Id == id));

    public Task<LegalCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken ct) =>
        Task.FromResult(Cases.FirstOrDefault(c => c.CaseNumber == caseNumber));

    public Task<string> NextCaseNumberAsync(int year, CancellationToken ct) =>
        Task.FromResult($"WTG-{year}-{++_sequence:D6}");

    public Task<int> NextEventSequenceAsync(Guid legalCaseId, CancellationToken ct) =>
        Task.FromResult(Events.Count(e => e.LegalCaseId == legalCaseId) + 1);

    public Task<PagedResult<LegalCase>> ListAsync(LegalCaseStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var items = Cases.Where(c => status is null || c.Status == status).ToList();
        return Task.FromResult(new PagedResult<LegalCase>(items, page, pageSize, items.Count));
    }

    public Task<IReadOnlyList<LegalCaseEvent>> ListEventsAsync(Guid legalCaseId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LegalCaseEvent>>(Events.Where(e => e.LegalCaseId == legalCaseId).OrderBy(e => e.Sequence).ToList());

    public Task<IReadOnlyList<LegalCaseAppeal>> ListAppealsAsync(Guid legalCaseId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LegalCaseAppeal>>(Appeals.Where(a => a.LegalCaseId == legalCaseId).ToList());

    public Task<LegalCaseAppeal?> GetAppealByIdAsync(Guid appealId, CancellationToken ct) =>
        Task.FromResult(Appeals.FirstOrDefault(a => a.Id == appealId));

    public Task<IReadOnlyList<LegalCase>> ListForReviewAsync(Guid reviewId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LegalCase>>(Cases.Where(c => c.ReviewId == reviewId).ToList());

    public Task<TransparencyCounts> GetTransparencyCountsAsync(int year, CancellationToken ct) =>
        Task.FromResult(new TransparencyCounts(year, Cases.Count, 0, 0, 0, 0, Appeals.Count, 0));
}
