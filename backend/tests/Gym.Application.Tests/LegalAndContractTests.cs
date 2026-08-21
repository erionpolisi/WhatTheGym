using FluentAssertions;
using FluentValidation;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Legal;
using Gym.Application.Features.Reviews;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gym.Application.Tests;

public class ReportReviewCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryReviewRepository _reviews = new();
    private readonly InMemoryLegalCaseRepository _cases = new();
    private readonly FakeOutbox _outbox = new();

    private ReportReviewCommandHandler CreateSut() => new(
        _reviews, _cases, new FakeTokenService(), _outbox, new FakeUnitOfWork(), new FakeClock(Now),
        Options.Create(new MailOptions { PublicBaseUrl = "http://localhost:3000" }),
        new ReportReviewCommandValidator());

    private Review AddPublishedReview()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), new ReviewRatings { Staff = 1 }, "Text", Now).Value;
        _reviews.Add(review);
        return review;
    }

    [Fact]
    public async Task Report_creates_case_with_events_token_and_reporter_mail()
    {
        var review = AddPublishedReview();

        var result = await CreateSut().Handle(
            new ReportReviewCommand(review.Id, "Defamation", "Melderin", "melderin@example.com",
                "Diese Bewertung enthaelt nachweislich falsche Tatsachenbehauptungen."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CaseNumber.Should().StartWith("WTG-2026-");
        result.Value.StatusToken.Should().NotBeNullOrEmpty();

        _cases.Cases.Should().HaveCount(1);
        _cases.Cases[0].Status.Should().Be(LegalCaseStatus.Received);
        _cases.Events.Select(e => e.EventType).Should().Contain(LegalCaseEventType.CaseCreated);
        _cases.Events.Select(e => e.EventType).Should().Contain(LegalCaseEventType.NotificationQueued);
        _outbox.Sent.Should().ContainSingle(m => m.ToEmail == "melderin@example.com");
        _outbox.Sent[0].BodyText.Should().Contain("ENTWURF");

        // Reported content stays online for normal reports.
        review.Status.Should().Be(ReviewStatus.Published);
    }

    [Fact]
    public async Task Invalid_category_is_rejected()
    {
        var review = AddPublishedReview();

        var result = await CreateSut().Handle(
            new ReportReviewCommand(review.Id, "NotACategory", "Melderin", "melderin@example.com",
                "Begruendung mit ausreichend Laenge fuer die Validierung."),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Too_short_description_is_rejected()
    {
        var review = AddPublishedReview();

        var result = await CreateSut().Handle(
            new ReportReviewCommand(review.Id, "Defamation", "M", "melderin@example.com", "kurz"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Nonexistent_review_yields_not_found()
    {
        var result = await CreateSut().Handle(
            new ReportReviewCommand(Guid.NewGuid(), "Defamation", "Melderin", "melderin@example.com",
                "Begruendung mit ausreichend Laenge fuer die Validierung."),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}

public class ProcessingActivitiesTests
{
    [Fact]
    public void Every_personal_data_entity_is_covered_by_an_activity()
    {
        var coveredEntities = ProcessingActivitiesRecord.Activities
            .SelectMany(a => a.Entities)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entity in ProcessingActivitiesRecord.PersonalDataEntities)
        {
            coveredEntities.Should().Contain(entity, $"entity {entity} stores personal data and must be documented");
        }
    }

    [Fact]
    public void Every_activity_documents_purpose_legal_basis_and_retention()
    {
        foreach (var activity in ProcessingActivitiesRecord.Activities)
        {
            activity.Purpose.Should().NotBeNullOrWhiteSpace();
            activity.LegalBasis.Should().Contain("DSGVO");
            activity.Retention.Should().NotBeNullOrWhiteSpace();
            activity.DataCategories.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Personal_data_entity_list_matches_domain_entities()
    {
        // Guards against new personal-data-bearing entities being added without documentation.
        var domainEntityNames = typeof(Review).Assembly.GetTypes()
            .Where(t => t.Namespace == "Gym.Domain.Entities" && t is { IsClass: true, IsAbstract: false })
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entity in ProcessingActivitiesRecord.PersonalDataEntities)
        {
            domainEntityNames.Should().Contain(entity);
        }
    }
}

public class ScoreSummaryDtoTests
{
    [Fact]
    public void Basis_strings_are_stable_api_contract()
    {
        ScoreSummaryDto.ToBasisString(ScoreBasis.Both).Should().Be("both");
        ScoreSummaryDto.ToBasisString(ScoreBasis.MembershipOnly).Should().Be("membershipOnly");
        ScoreSummaryDto.ToBasisString(ScoreBasis.StudioOnly).Should().Be("studioOnly");
        ScoreSummaryDto.ToBasisString(ScoreBasis.None).Should().Be("none");
    }

    [Fact]
    public void Categories_carry_area_and_camel_case_names()
    {
        var dto = ScoreSummaryDto.From(ScoreCalculator.Calculate([new ReviewRatings { PriceValue = 3, Equipment = 4 }]));

        dto.Categories.Should().HaveCount(11);
        dto.Categories.Single(c => c.Category == "priceValue").Area.Should().Be("membership");
        dto.Categories.Single(c => c.Category == "equipment").Area.Should().Be("studio");
        dto.Categories.Single(c => c.Category == "showers").Average.Should().BeNull();
    }
}

public class ValidatorTests
{
    [Fact]
    public void Review_validator_blocks_link_spam()
    {
        var validator = new CreateReviewCommandValidator();
        var ratings = new RatingsDto(null, null, null, null, 4, null, null, null, null, null, null);
        var spam = "http://a.com http://b.com http://c.com http://d.com";

        var result = validator.Validate(new CreateReviewCommand(Guid.NewGuid(), "gym", ratings, spam));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Report_validator_requires_valid_email()
    {
        var validator = new ReportReviewCommandValidator();

        var result = validator.Validate(new ReportReviewCommand(
            Guid.NewGuid(), "Defamation", "Name", "keine-email",
            "Ausreichend lange Begruendung fuer die Meldung."));

        result.IsValid.Should().BeFalse();
    }
}
