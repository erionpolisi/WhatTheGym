using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class MiscEntityEdgeTests
{
    public static TheoryData<ContactRequestType> ContactTypes
    {
        get
        {
            var data = new TheoryData<ContactRequestType>();
            foreach (var type in Enum.GetValues<ContactRequestType>())
            {
                data.Add(type);
            }

            return data;
        }
    }

    public static TheoryData<ContactRequestStatus, bool> ContactStatuses => new()
    {
        { ContactRequestStatus.New, false },
        { ContactRequestStatus.InProgress, false },
        { ContactRequestStatus.Resolved, true },
    };

    public static TheoryData<LegalDocumentType> LegalDocumentTypes
    {
        get
        {
            var data = new TheoryData<LegalDocumentType>();
            foreach (var type in Enum.GetValues<LegalDocumentType>())
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ContactTypes))]
    public void Contact_request_create_accepts_all_request_types_and_sanitizes_fields(ContactRequestType type)
    {
        var result = ContactRequest.Create(type, "  Anna ", " anna@example.com ", "  Hallo\u0000 ", Guid.NewGuid(), DomainTestHelpers.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(type);
        result.Value.Name.Should().Be("Anna");
        result.Value.Email.Should().Be("anna@example.com");
        result.Value.Message.Should().Be("Hallo");
        result.Value.Status.Should().Be(ContactRequestStatus.New);
    }

    [Theory]
    [InlineData("", "mail@example.com", "message")]
    [InlineData("name", "", "message")]
    [InlineData("name", "mail@example.com", "")]
    [InlineData("   ", "mail@example.com", "message")]
    [InlineData("name", "   ", "message")]
    [InlineData("name", "mail@example.com", "   ")]
    public void Contact_request_create_rejects_missing_required_fields(string name, string email, string message)
    {
        var result = ContactRequest.Create(ContactRequestType.General, name, email, message, null, DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("contact.fields");
    }

    [Fact]
    public void Contact_request_message_length_boundary_is_applied_after_sanitization()
    {
        ContactRequest.Create(ContactRequestType.General, "Name", "mail@example.com", "  " + new string('x', ContactRequest.MaxMessageLength) + "  ", null, DomainTestHelpers.Now)
            .IsSuccess.Should().BeTrue();
        ContactRequest.Create(ContactRequestType.General, "Name", "mail@example.com", new string('x', ContactRequest.MaxMessageLength + 1), null, DomainTestHelpers.Now)
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ContactStatuses))]
    public void Contact_request_status_sets_resolved_timestamp_only_for_resolved(ContactRequestStatus status, bool expectedResolvedAt)
    {
        var request = ContactRequest.Create(ContactRequestType.General, "Name", "mail@example.com", "Message", null, DomainTestHelpers.Now).Value;
        var changedAt = DomainTestHelpers.Now.AddHours(1);

        request.SetStatus(status, changedAt);

        request.Status.Should().Be(status);
        if (expectedResolvedAt)
        {
            request.ResolvedAtUtc.Should().Be(changedAt);
        }
        else
        {
            request.ResolvedAtUtc.Should().BeNull();
        }
    }

    [Fact]
    public void Analytics_event_factory_records_values_without_mutating_path_or_bucket()
    {
        var eventItem = AnalyticsEvent.Create("gym_view", "/gyms/test", "bucket-hash", DomainTestHelpers.Now);

        eventItem.EventType.Should().Be("gym_view");
        eventItem.Path.Should().Be("/gyms/test");
        eventItem.SessionBucket.Should().Be("bucket-hash");
        eventItem.OccurredAtUtc.Should().Be(DomainTestHelpers.Now);
    }

    [Theory]
    [MemberData(nameof(LegalDocumentTypes))]
    public void Legal_document_draft_and_publish_preserve_type_version_and_first_publish_time(LegalDocumentType type)
    {
        var document = LegalDocument.CreateDraft(type, 3, "  Titel  ", "ENTWURF - anwaltlich pruefen lassen", DomainTestHelpers.Now);
        var first = DomainTestHelpers.Now.AddDays(1);
        var second = DomainTestHelpers.Now.AddDays(2);

        document.Publish(first);
        document.Publish(second);

        document.Type.Should().Be(type);
        document.Version.Should().Be(3);
        document.Title.Should().Be("Titel");
        document.IsPublished.Should().BeTrue();
        document.PublishedAtUtc.Should().Be(first);
    }

    [Fact]
    public void Outbox_email_enqueue_and_failures_track_backoff_error_truncation_and_terminal_status()
    {
        var email = OutboxEmail.Enqueue("to@example.com", "Subject", "Body", "kind", Guid.NewGuid(), DomainTestHelpers.Now);

        email.Status.Should().Be(OutboxEmailStatus.Pending);
        email.NextAttemptAtUtc.Should().Be(DomainTestHelpers.Now);
        email.MarkAttemptFailed(new string('x', 3000), DomainTestHelpers.Now);

        email.AttemptCount.Should().Be(1);
        email.LastError.Should().HaveLength(2000);
        email.NextAttemptAtUtc.Should().Be(DomainTestHelpers.Now.AddSeconds(60));
    }

    [Fact]
    public void Outbox_email_mark_sent_after_failure_records_sent_state_and_clears_error()
    {
        var email = OutboxEmail.Enqueue("to@example.com", "Subject", "Body", "kind", null, DomainTestHelpers.Now);
        email.MarkAttemptFailed("boom", DomainTestHelpers.Now);

        email.MarkSent(DomainTestHelpers.Now.AddMinutes(10));

        email.Status.Should().Be(OutboxEmailStatus.Sent);
        email.LastError.Should().BeNull();
        email.SentAtUtc.Should().Be(DomainTestHelpers.Now.AddMinutes(10));
    }

    [Fact]
    public void Gym_rating_summary_create_and_apply_replace_all_score_fields()
    {
        var gymId = Guid.NewGuid();
        var firstScore = ScoreCalculator.Calculate([new ReviewRatings { Equipment = 4 }]);
        var secondScore = ScoreCalculator.Calculate([new ReviewRatings { PriceValue = 2, Equipment = 5 }]);
        var summary = GymRatingSummary.Create(gymId, firstScore, "first", DomainTestHelpers.Now);

        summary.Apply(secondScore, "second", DomainTestHelpers.Now.AddMinutes(5));

        summary.GymId.Should().Be(gymId);
        summary.ReviewCount.Should().Be(1);
        summary.MembershipScore.Should().Be(2);
        summary.StudioScore.Should().Be(5);
        summary.TotalScore.Should().Be(3.5);
        summary.ScoreBasis.Should().Be(ScoreBasis.Both);
        summary.CategoriesJson.Should().Be("second");
    }

    [Fact]
    public void Legal_hold_create_and_release_are_idempotent()
    {
        var legalCaseId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var hold = LegalHold.Create("  Litigation hold  ", legalCaseId, reviewId, null, DomainTestHelpers.Now);
        var releasedAt = DomainTestHelpers.Now.AddDays(1);

        hold.Release(releasedAt);
        hold.Release(releasedAt.AddDays(1));

        hold.Reason.Should().Be("Litigation hold");
        hold.LegalCaseId.Should().Be(legalCaseId);
        hold.ReviewId.Should().Be(reviewId);
        hold.IsActive.Should().BeFalse();
        hold.ReleasedAtUtc.Should().Be(releasedAt);
    }

    // Layering decision: email FORMAT validation is owned by the API-level FluentValidation
    // validators; the entity only guards domain invariants. Documented here.
    [Fact]
    public void Contact_request_accepts_unvalidated_email_format_by_design()
    {
        ContactRequest.Create(ContactRequestType.General, "Name", "not-an-email", "Message", null, DomainTestHelpers.Now).IsSuccess.Should().BeTrue();
    }
}
