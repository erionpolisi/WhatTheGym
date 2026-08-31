using FluentAssertions;
using Gym.Application.Common;
using Gym.Application.Features.Legal;
using Gym.Application.Features.Reviews;
using Gym.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gym.Application.Tests;

/// <summary>Regression tests for the 2026-08 security hardening fixes.</summary>
public sealed class SecurityHardeningTests
{
    private static IOptions<MailOptions> Mail() => Options.Create(new MailOptions { PublicBaseUrl = "https://example.invalid" });

    [Fact]
    public async Task Report_audit_event_masks_status_token_while_mail_contains_it()
    {
        var reviews = new InMemoryReviewRepository();
        var cases = new InMemoryLegalCaseRepository();
        var outbox = new FakeOutbox();
        var review = AppTestData.Review();
        reviews.Add(review);

        var handler = new ReportReviewCommandHandler(
            reviews, cases, new FakeTokenService(), outbox, new FakeUnitOfWork(), new FakeClock(AppTestData.Now),
            Mail(), new ReportReviewCommandValidator());
        var result = await handler.Handle(
            new ReportReviewCommand(review.Id, "Defamation", "Melderin", "melderin@example.at",
                "Diese Bewertung enthaelt eine falsche Tatsachenbehauptung."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var statusToken = result.Value.StatusToken;
        outbox.Sent.Should().ContainSingle().Which.BodyText.Should().Contain(statusToken);

        var notification = cases.Events.Single(e => e.EventType == LegalCaseEventType.NotificationQueued);
        notification.DataJson.Should().NotContain(statusToken);
        notification.DataJson.Should().Contain(Uri.EscapeDataString(LegalLinks.MaskedToken));
    }

    [Fact]
    public async Task Decision_audit_events_mask_appeal_token_while_mails_contain_it()
    {
        var reviews = new InMemoryReviewRepository();
        var cases = new InMemoryLegalCaseRepository();
        var users = new InMemoryUserRepository();
        var outbox = new FakeOutbox();
        var tokens = new FakeTokenService();
        var author = AppTestData.User("autorin@example.at");
        users.Add(author);
        var review = AppTestData.Review(userId: author.Id);
        reviews.Add(review);

        var (_, statusHash) = tokens.CreateToken();
        var legalCase = Gym.Domain.Entities.LegalCase.Create(
            "WTG-2026-000001", review.Id, LegalCaseCategory.Defamation, "Melderin", "melderin@example.at",
            "Begruendung mit ausreichender Laenge fuer den Fall.", statusHash, AppTestData.Now).Value;
        cases.Add(legalCase);

        var handler = new DecideCaseCommandHandler(
            cases, reviews, users, tokens, new GymScoreUpdater(reviews, new InMemorySummaryStore()),
            outbox, new FakeUnitOfWork(), new FakeClock(AppTestData.Now), Mail());
        var result = await handler.Handle(
            new DecideCaseCommand(Guid.NewGuid(), legalCase.Id, "FullyRemoved", "Erwiesen falsche Tatsachenbehauptung."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // The appeal token was created after the status token by the fake ("token-2").
        var appealMail = outbox.Sent.Single(m => m.Kind == "legal.decision.author");
        appealMail.BodyText.Should().Contain("token-2");

        foreach (var notification in cases.Events.Where(e => e.EventType == LegalCaseEventType.NotificationQueued))
        {
            notification.DataJson.Should().NotContain("token-2");
        }
    }

    [Fact]
    public async Task Reclassifying_fast_track_to_normal_releases_hidden_review()
    {
        var reviews = new InMemoryReviewRepository();
        var cases = new InMemoryLegalCaseRepository();
        var users = new InMemoryUserRepository();
        var outbox = new FakeOutbox();
        var summaries = new InMemorySummaryStore();
        var author = AppTestData.User();
        users.Add(author);
        var review = AppTestData.Review(userId: author.Id);
        reviews.Add(review);

        var legalCase = Gym.Domain.Entities.LegalCase.Create(
            "WTG-2026-000002", review.Id, LegalCaseCategory.IllegalContent, "Melder", "melder@example.at",
            "Begruendung mit ausreichender Laenge fuer den Fall.", "hash", AppTestData.Now).Value;
        cases.Add(legalCase);

        var handler = new ClassifyCaseCommandHandler(
            cases, reviews, users, new GymScoreUpdater(reviews, summaries), outbox, new FakeUnitOfWork(), new FakeClock(AppTestData.Now));

        (await handler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), legalCase.Id, "FastTrackObviouslyIllegal"), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.UnderReview);

        (await handler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), legalCase.Id, "Normal"), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        cases.Events.Should().Contain(e => e.EventType == LegalCaseEventType.ContentRestored);
        summaries.Scores[review.GymId].ReviewCount.Should().Be(1);
    }

    [Fact]
    public async Task Reclassifying_to_normal_keeps_review_hidden_while_another_fast_track_case_is_open()
    {
        var reviews = new InMemoryReviewRepository();
        var cases = new InMemoryLegalCaseRepository();
        var users = new InMemoryUserRepository();
        var review = AppTestData.Review();
        reviews.Add(review);

        var first = Gym.Domain.Entities.LegalCase.Create(
            "WTG-2026-000003", review.Id, LegalCaseCategory.IllegalContent, "A", "a@example.at",
            "Begruendung mit ausreichender Laenge fuer den Fall.", "hash-a", AppTestData.Now).Value;
        var second = Gym.Domain.Entities.LegalCase.Create(
            "WTG-2026-000004", review.Id, LegalCaseCategory.IllegalContent, "B", "b@example.at",
            "Begruendung mit ausreichender Laenge fuer den Fall.", "hash-b", AppTestData.Now).Value;
        cases.Add(first);
        cases.Add(second);

        var handler = new ClassifyCaseCommandHandler(
            cases, reviews, users, new GymScoreUpdater(reviews, new InMemorySummaryStore()), new FakeOutbox(),
            new FakeUnitOfWork(), new FakeClock(AppTestData.Now));

        (await handler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), first.Id, "FastTrackObviouslyIllegal"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), second.Id, "FastTrackObviouslyIllegal"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        (await handler.Handle(new ClassifyCaseCommand(Guid.NewGuid(), first.Id, "Normal"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.UnderReview);
    }

    [Theory]
    [InlineData("http://a.at http://b.at http://c.at", true)]
    [InlineData("http://a.at http://b.at http://c.at http://d.at", false)]
    public async Task Update_own_review_applies_link_spam_check(string text, bool expectedSuccess)
    {
        var reviews = new InMemoryReviewRepository();
        var review = AppTestData.Review();
        reviews.Add(review);

        var handler = new UpdateOwnReviewCommandHandler(
            reviews, new GymScoreUpdater(reviews, new InMemorySummaryStore()), new FakeUnitOfWork(),
            new FakeClock(AppTestData.Now), new UpdateOwnReviewCommandValidator());
        var result = await handler.Handle(
            new UpdateOwnReviewCommand(review.UserId, review.Id, AppTestData.Ratings(4), text), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
    }

    [Fact]
    public async Task Soft_deleted_review_cannot_be_edited_by_its_author()
    {
        var reviews = new InMemoryReviewRepository();
        var review = AppTestData.Review(status: ReviewStatus.SoftDeleted);
        reviews.Add(review);

        var handler = new UpdateOwnReviewCommandHandler(
            reviews, new GymScoreUpdater(reviews, new InMemorySummaryStore()), new FakeUnitOfWork(),
            new FakeClock(AppTestData.Now), new UpdateOwnReviewCommandValidator());
        var result = await handler.Handle(
            new UpdateOwnReviewCommand(review.UserId, review.Id, AppTestData.Ratings(4), "Nachtraeglich geaendert"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("review.locked");
    }
}
