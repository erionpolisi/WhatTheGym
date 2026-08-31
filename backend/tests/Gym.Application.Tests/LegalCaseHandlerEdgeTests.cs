using FluentAssertions;
using Gym.Application.Common;
using Gym.Application.Features.Legal;
using Gym.Application.Features.Reviews;
using Gym.Application.Features.Users;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gym.Application.Tests;

public sealed class LegalCaseHandlerEdgeTests
{
    public static TheoryData<string, bool> CategoryCases() => new()
    {
        { "Defamation", true }, { "defamation", true }, { "FalseFactualClaim", true }, { "Insult", true }, { "PrivacyViolation", true }, { "IllegalContent", true }, { "Other", true },
        { "", false }, { " ", false }, { "Spam", false }, { "1", true },
    };

    public static TheoryData<string, bool> ClassificationCases() => new()
    {
        { "Normal", true }, { "normal", true }, { "FastTrackObviouslyIllegal", true }, { "Unclassified", false }, { "", false }, { "Fast", false }, { "1", true },
    };

    public static TheoryData<string, bool> DecisionCases() => new()
    {
        { "KeepOnline", true }, { "keeponline", true }, { "FullyRemoved", true }, { "", false }, { "Remove", false }, { "1", true },
    };

    public static TheoryData<string, bool> AppealOutcomeCases() => new()
    {
        { "DecisionUpheld", true }, { "decisionupheld", true }, { "DecisionReversed", true }, { "", false }, { "Reversed", false }, { "1", true },
    };

    [Theory]
    [MemberData(nameof(CategoryCases))]
    public async Task Report_review_parses_legal_category_and_stores_hashed_status_token(string category, bool expectedSuccess)
    {
        var context = LegalContext.Create();

        var result = await context.ReportHandler.Handle(new ReportReviewCommand(context.Review.Id, category, "Melderin", "melderin@example.at", new string('b', 40)), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            var legalCase = context.Cases.Cases.Single();
            legalCase.StatusTokenHash.Should().NotBe(result.Value.StatusToken);
            legalCase.StatusTokenHash.Should().StartWith("hash-");
            context.Outbox.Sent.Single().Subject.Should().StartWith("[WhatTheGym] Meldung eingegangen");
        }
        else
        {
            result.Error.Code.Should().Be("legalCase.category");
        }
    }

    [Theory]
    [MemberData(nameof(ClassificationCases))]
    public async Task Classify_case_parses_classification_and_fasttrack_hides_content(string classification, bool expectedSuccess)
    {
        var context = await LegalContext.ReportedAsync();

        var result = await context.ClassifyHandler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), context.LegalCase.Id, classification), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess && classification.Equals("FastTrackObviouslyIllegal", StringComparison.OrdinalIgnoreCase))
        {
            context.Review.Status.Should().Be(ReviewStatus.UnderReview);
            context.Summaries.Scores.Should().ContainKey(context.Review.GymId);
            context.Outbox.Sent.Should().Contain(m => m.Subject.StartsWith("[WhatTheGym] Ihre Bewertung wurde", StringComparison.Ordinal));
        }
        else if (!expectedSuccess)
        {
            result.Error.Type.Should().BeOneOf(ErrorType.Validation, ErrorType.Conflict);
        }
    }

    [Fact]
    public async Task Normal_classification_keeps_reported_content_online()
    {
        var context = await LegalContext.ReportedAsync();

        var result = await context.ClassifyHandler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), context.LegalCase.Id, "Normal"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Review.Status.Should().Be(ReviewStatus.Published);
        context.Cases.Events.Select(e => e.EventType).Should().Contain(LegalCaseEventType.Classified);
    }

    [Theory]
    [MemberData(nameof(DecisionCases))]
    public async Task Decide_case_parses_decision_and_queues_party_notifications(string decision, bool expectedSuccess)
    {
        var context = await LegalContext.StartedAsync();

        var result = await context.DecideHandler.Handle(new DecideCaseCommand(Guid.NewGuid(), context.LegalCase.Id, decision, "Nachvollziehbare rechtliche Begruendung."), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            context.LegalCase.AppealTokenHash.Should().NotBe("token-2");
            context.LegalCase.AppealTokenHash.Should().Be("hash-token-2");
            context.Outbox.Sent.Should().Contain(m => m.Subject.StartsWith("[WhatTheGym] Entscheidung zu Ihrer Meldung", StringComparison.Ordinal));
            context.Outbox.Sent.Should().Contain(m => m.Subject.StartsWith("[WhatTheGym] Entscheidung zu Ihrer Bewertung", StringComparison.Ordinal));
        }
        else
        {
            result.Error.Code.Should().Be("legalCase.decision");
        }
    }

    [Theory]
    [MemberData(nameof(AppealOutcomeCases))]
    public async Task Decide_appeal_parses_outcome_and_notifies_parties(string outcome, bool expectedSuccess)
    {
        var context = await LegalContext.DecidedWithAppealAsync("FullyRemoved");

        var result = await context.DecideAppealHandler.Handle(new DecideAppealCommand(Guid.NewGuid(), context.Appeal.Id, outcome, "Pruefung des Einspruchs."), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            context.Outbox.Sent.Should().Contain(m => m.Subject.StartsWith("[WhatTheGym] Entscheidung zu Ihrem Einspruch", StringComparison.Ordinal));
        }
        else
        {
            result.Error.Code.Should().Be("appeal.outcome");
        }
    }

    [Fact]
    public async Task Full_legal_flow_records_monotonic_events_and_closes_case()
    {
        var context = await LegalContext.ReportedAsync();

        (await context.ClassifyHandler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), context.LegalCase.Id, "Normal"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await context.StartHandler.Handle(new StartCaseReviewCommand(Guid.NewGuid(), context.LegalCase.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await context.DecideHandler.Handle(new DecideCaseCommand(Guid.NewGuid(), context.LegalCase.Id, "KeepOnline", "Bewertung bleibt online."), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await context.SubmitAppealHandler.Handle(new SubmitAppealCommand(context.LegalCase.CaseNumber, "token-2", "Ich erhebe begruendet Einspruch."), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await context.DecideAppealHandler.Handle(new DecideAppealCommand(Guid.NewGuid(), context.Cases.Appeals.Single().Id, "DecisionUpheld", "Entscheidung bleibt aufrecht."), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await context.CloseHandler.Handle(new CloseCaseCommand(Guid.NewGuid(), context.LegalCase.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();

        context.LegalCase.Status.Should().Be(LegalCaseStatus.Closed);
        context.Cases.Events.Select(e => e.Sequence).Should().Equal(Enumerable.Range(1, context.Cases.Events.Count));
        context.Cases.Events.Select(e => e.EventType).Should().ContainInOrder(
            LegalCaseEventType.CaseCreated,
            LegalCaseEventType.Classified,
            LegalCaseEventType.ReviewStarted,
            LegalCaseEventType.Decided,
            LegalCaseEventType.AppealSubmitted,
            LegalCaseEventType.AppealDecided,
            LegalCaseEventType.Closed);
    }

    [Fact]
    public async Task Wrong_state_start_after_decision_returns_conflict()
    {
        var context = await LegalContext.DecidedAsync("KeepOnline");

        var result = await context.StartHandler.Handle(new StartCaseReviewCommand(Guid.NewGuid(), context.LegalCase.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Appeal_is_accepted_at_exact_deadline_and_rejected_afterwards()
    {
        var context = await LegalContext.DecidedAsync("FullyRemoved");
        context.Clock.UtcNow = context.LegalCase.AppealDeadlineUtc!.Value;

        var atDeadline = await context.SubmitAppealHandler.Handle(new SubmitAppealCommand(context.LegalCase.CaseNumber, "token-2", "Einspruch exakt an der Frist."), CancellationToken.None);
        context.Clock.UtcNow = context.LegalCase.AppealDeadlineUtc.Value.AddTicks(1);
        var afterDeadline = await context.SubmitAppealHandler.Handle(new SubmitAppealCommand(context.LegalCase.CaseNumber, "token-2", "Einspruch zu spaet."), CancellationToken.None);

        atDeadline.IsSuccess.Should().BeTrue();
        afterDeadline.IsFailure.Should().BeTrue();
        afterDeadline.Error.Code.Should().Be("appeal.closed");
    }

    [Fact]
    public async Task Tokenized_status_requires_hashed_match()
    {
        var context = await LegalContext.ReportedAsync();
        var sut = new GetCaseStatusByTokenQueryHandler(context.Cases, context.Tokens);

        var success = await sut.Handle(new GetCaseStatusByTokenQuery(context.LegalCase.CaseNumber, "token-1"), CancellationToken.None);
        var failure = await sut.Handle(new GetCaseStatusByTokenQuery(context.LegalCase.CaseNumber, "raw-token"), CancellationToken.None);

        success.IsSuccess.Should().BeTrue();
        failure.IsFailure.Should().BeTrue();
        failure.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Fully_removed_decision_removes_review_and_recalculates_score()
    {
        var context = await LegalContext.StartedAsync();

        var result = await context.DecideHandler.Handle(new DecideCaseCommand(Guid.NewGuid(), context.LegalCase.Id, "FullyRemoved", "Rechtsverletzung liegt vor."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Review.Status.Should().Be(ReviewStatus.RemovedLegal);
        context.Summaries.Scores[context.Review.GymId].ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task Reversed_author_appeal_reinstates_removed_review()
    {
        var context = await LegalContext.DecidedWithAppealAsync("FullyRemoved");

        var result = await context.DecideAppealHandler.Handle(new DecideAppealCommand(Guid.NewGuid(), context.Appeal.Id, "DecisionReversed", "Bewertung wird wiederhergestellt."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Review.Status.Should().Be(ReviewStatus.Published);
        context.Summaries.Scores.Should().ContainKey(context.Review.GymId);
    }

    private sealed class LegalContext
    {
        private LegalContext()
        {
            Users.Add(Author);
            Reviews.Add(Review);
        }

        public InMemoryReviewRepository Reviews { get; } = new();

        public InMemoryLegalCaseRepository Cases { get; } = new();

        public InMemoryUserRepository Users { get; } = new();

        public InMemorySummaryStore Summaries { get; } = new();

        public FakeOutbox Outbox { get; } = new();

        public FakeTokenService Tokens { get; } = new();

        public FakeClock Clock { get; } = new(AppTestData.Now);

        public User Author { get; } = AppTestData.User("author@example.at");

        public Review Review { get; private set; } = null!;

        public LegalCase LegalCase => Cases.Cases.Single();

        public LegalCaseAppeal Appeal => Cases.Appeals.Single();

        public ReportReviewCommandHandler ReportHandler => new(Reviews, Cases, Tokens, Outbox, new FakeUnitOfWork(), Clock, MailOptions, new ReportReviewCommandValidator());

        public ClassifyCaseCommandHandler ClassifyHandler => new(Cases, Reviews, Users, new GymScoreUpdater(Reviews, Summaries), Outbox, new FakeUnitOfWork(), Clock);

        public StartCaseReviewCommandHandler StartHandler => new(Cases, new FakeUnitOfWork(), Clock);

        public DecideCaseCommandHandler DecideHandler => new(Cases, Reviews, Users, Tokens, new GymScoreUpdater(Reviews, Summaries), Outbox, new FakeUnitOfWork(), Clock, MailOptions);

        public SubmitAppealCommandHandler SubmitAppealHandler => new(Cases, Reviews, Users, Tokens, Outbox, new FakeUnitOfWork(), Clock);

        public DecideAppealCommandHandler DecideAppealHandler => new(Cases, Reviews, Users, new GymScoreUpdater(Reviews, Summaries), Outbox, new FakeUnitOfWork(), Clock);

        public CloseCaseCommandHandler CloseHandler => new(Cases, new FakeUnitOfWork(), Clock);

        private static IOptions<MailOptions> MailOptions => Options.Create(new MailOptions { PublicBaseUrl = "https://whatthegym.at" });

        public static LegalContext Create()
        {
            var context = new LegalContext();
            context.Review = AppTestData.Review(userId: context.Author.Id);
            context.Reviews.Reviews.Clear();
            context.Reviews.Add(context.Review);
            return context;
        }

        public static async Task<LegalContext> ReportedAsync()
        {
            var context = Create();
            var result = await context.ReportHandler.Handle(new ReportReviewCommand(context.Review.Id, "Defamation", "Melderin", "melderin@example.at", new string('b', 40)), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            return context;
        }

        public static async Task<LegalContext> StartedAsync()
        {
            var context = await ReportedAsync();
            (await context.ClassifyHandler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), context.LegalCase.Id, "Normal"), CancellationToken.None)).IsSuccess.Should().BeTrue();
            (await context.StartHandler.Handle(new StartCaseReviewCommand(Guid.NewGuid(), context.LegalCase.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
            return context;
        }

        public static async Task<LegalContext> DecidedAsync(string decision)
        {
            var context = await StartedAsync();
            (await context.DecideHandler.Handle(new DecideCaseCommand(Guid.NewGuid(), context.LegalCase.Id, decision, "Ausreichende Begruendung."), CancellationToken.None)).IsSuccess.Should().BeTrue();
            return context;
        }

        public static async Task<LegalContext> DecidedWithAppealAsync(string decision)
        {
            var context = await DecidedAsync(decision);
            (await context.SubmitAppealHandler.Handle(new SubmitAppealCommand(context.LegalCase.CaseNumber, "token-2", "Ich erhebe begruendet Einspruch."), CancellationToken.None)).IsSuccess.Should().BeTrue();
            return context;
        }
    }
}

public sealed class RetentionAndPrivacyHandlerTests
{
    [Fact]
    public async Task Export_my_data_contains_account_reviews_revisions_legal_cases_and_contact_requests()
    {
        var user = AppTestData.User("export@example.at");
        var users = new InMemoryUserRepository();
        users.Add(user);
        var reviews = new InMemoryReviewRepository();
        var review = AppTestData.Review(userId: user.Id);
        reviews.Add(review);
        reviews.AddRevision(ReviewRevision.Create(review.Id, 1, "Alt", "{\"equipment\":4}", user.Id, AppTestData.Now));
        var personal = new AppFakePersonalDataQuery();
        personal.Cases.Add(LegalCase.Create("WTG-2026-000001", review.Id, LegalCaseCategory.Other, "Anna", user.Email, "Beschreibung fuer Export", "hash-status", AppTestData.Now).Value);
        personal.Contacts.Add(ContactRequest.Create(ContactRequestType.General, "Anna", user.Email, "Bitte melden.", null, AppTestData.Now).Value);

        var result = await new ExportMyDataQueryHandler(users, reviews, personal, new FakeClock(AppTestData.Now))
            .Handle(new ExportMyDataQuery(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Account.Email.Should().Be(user.Email);
        result.Value.Reviews.Should().ContainSingle(r => r.Id == review.Id);
        result.Value.ReviewRevisions.Should().ContainSingle();
        result.Value.LegalCasesAsReporter.Should().ContainSingle();
        result.Value.ContactRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_account_anonymizes_user_revokes_tokens_and_soft_deletes_public_reviews_without_hold()
    {
        var user = AppTestData.User("delete@example.at");
        var users = new InMemoryUserRepository();
        users.Add(user);
        var reviews = new InMemoryReviewRepository();
        var review = AppTestData.Review(userId: user.Id);
        reviews.Add(review);
        var refreshTokens = new AppFakeRefreshTokenRepository();
        var summaries = new InMemorySummaryStore();

        var result = await new DeleteMyAccountCommandHandler(users, reviews, refreshTokens, new GymScoreUpdater(reviews, summaries), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new DeleteMyAccountCommand(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Deleted);
        user.Email.Should().EndWith("@anonymized.invalid");
        review.Status.Should().Be(ReviewStatus.SoftDeleted);
        refreshTokens.Revocations.Should().ContainSingle(r => r.UserId == user.Id);
        summaries.Scores.Should().ContainKey(review.GymId);
    }

    [Fact]
    public async Task Delete_account_soft_deletes_held_review_too_because_holds_only_pause_purging()
    {
        var user = AppTestData.User("held@example.at");
        var users = new InMemoryUserRepository();
        users.Add(user);
        var reviews = new InMemoryReviewRepository();
        var heldReview = AppTestData.Review(userId: user.Id);
        reviews.Add(heldReview);
        var summaries = new InMemorySummaryStore();

        var result = await new DeleteMyAccountCommandHandler(users, reviews, new AppFakeRefreshTokenRepository(), new GymScoreUpdater(reviews, summaries), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new DeleteMyAccountCommand(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Deleted);
        heldReview.Status.Should().Be(ReviewStatus.SoftDeleted);
        summaries.Scores.Should().ContainKey(heldReview.GymId);
    }

    [Fact]
    public async Task Delete_account_should_make_held_review_non_public()
    {
        var user = AppTestData.User("held@example.at");
        var users = new InMemoryUserRepository();
        users.Add(user);
        var reviews = new InMemoryReviewRepository();
        var heldReview = AppTestData.Review(userId: user.Id);
        reviews.Add(heldReview);

        await new DeleteMyAccountCommandHandler(users, reviews, new AppFakeRefreshTokenRepository(), new GymScoreUpdater(reviews, new InMemorySummaryStore()), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new DeleteMyAccountCommand(user.Id), CancellationToken.None);

        heldReview.IsPublic.Should().BeFalse();
    }
}

