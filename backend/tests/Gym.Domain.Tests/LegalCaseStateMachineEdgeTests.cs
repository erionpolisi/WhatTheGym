using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class LegalCaseStateMachineEdgeTests
{
    public static TheoryData<string?, string, string, string> MissingCreateFields => new()
    {
        { null, "reporter@example.com", "Beschreibung", "legalCase.fields" },
        { "   ", "reporter@example.com", "Beschreibung", "legalCase.fields" },
        { "Reporter", "   ", "Beschreibung", "legalCase.fields" },
        { "Reporter", "reporter@example.com", "   ", "legalCase.fields" },
    };

    public static TheoryData<LegalCaseClassification> ValidClassifications => new()
    {
        LegalCaseClassification.Normal,
        LegalCaseClassification.FastTrackObviouslyIllegal,
    };

    public static TheoryData<LegalDecision> Decisions => new()
    {
        LegalDecision.KeepOnline,
        LegalDecision.FullyRemoved,
    };

    public static TheoryData<AppealOutcome> AppealOutcomes => new()
    {
        AppealOutcome.DecisionUpheld,
        AppealOutcome.DecisionReversed,
    };

    public static TheoryData<LegalCaseEventType, LegalActorType> EventCases
    {
        get
        {
            var data = new TheoryData<LegalCaseEventType, LegalActorType>();
            foreach (var eventType in Enum.GetValues<LegalCaseEventType>())
            {
                data.Add(eventType, LegalActorType.System);
            }

            foreach (var actorType in Enum.GetValues<LegalActorType>())
            {
                if (actorType != LegalActorType.System)
                {
                    data.Add(LegalCaseEventType.NoteAdded, actorType);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MissingCreateFields))]
    public void Create_rejects_missing_reporter_email_or_description(string? name, string email, string description, string expectedCode)
    {
        var result = LegalCase.Create("WTG-2026-000001", Guid.NewGuid(), LegalCaseCategory.Other, name!, email, description, "status", DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Create_accepts_description_exactly_at_max_length_after_sanitization()
    {
        var description = "  " + new string('x', LegalCase.MaxDescriptionLength) + "  ";

        var result = LegalCase.Create("WTG-2026-000001", Guid.NewGuid(), LegalCaseCategory.Other, "Name", "name@example.com", description, "status", DomainTestHelpers.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().HaveLength(LegalCase.MaxDescriptionLength);
    }

    [Fact]
    public void Create_rejects_description_over_max_length_after_sanitization()
    {
        var result = LegalCase.Create("WTG-2026-000001", Guid.NewGuid(), LegalCaseCategory.Other, "Name", "name@example.com", new string('x', LegalCase.MaxDescriptionLength + 1), "status", DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("legalCase.description");
    }

    [Theory]
    [MemberData(nameof(ValidClassifications))]
    public void Received_case_can_be_classified_with_non_unclassified_values(LegalCaseClassification classification)
    {
        var legalCase = DomainTestHelpers.CreateLegalCase();

        var result = legalCase.Classify(classification, DomainTestHelpers.Now.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        legalCase.Classification.Should().Be(classification);
    }

    [Fact]
    public void Full_happy_path_received_under_review_decided_closed_sets_timestamps()
    {
        var legalCase = DomainTestHelpers.CreateLegalCase();
        var reviewStarted = DomainTestHelpers.Now.AddMinutes(1);
        var decided = DomainTestHelpers.Now.AddMinutes(2);
        var closed = DomainTestHelpers.Now.AddMinutes(3);

        legalCase.StartReview(reviewStarted).IsSuccess.Should().BeTrue();
        legalCase.Decide(LegalDecision.KeepOnline, "Zulaessig.", decided).IsSuccess.Should().BeTrue();
        legalCase.Close(closed).IsSuccess.Should().BeTrue();

        legalCase.Status.Should().Be(LegalCaseStatus.Closed);
        legalCase.DecidedAtUtc.Should().Be(decided);
        legalCase.ClosedAtUtc.Should().Be(closed);
        legalCase.AppealDeadlineUtc.Should().Be(decided.Add(LegalCase.AppealWindow));
    }

    [Theory]
    [MemberData(nameof(Decisions))]
    public void Decide_from_received_or_under_review_records_decision_and_rationale(LegalDecision decision)
    {
        var legalCase = DomainTestHelpers.CreateLegalCase();

        var result = legalCase.Decide(decision, "  Begruendung\u0000 ", DomainTestHelpers.Now.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        legalCase.Status.Should().Be(LegalCaseStatus.Decided);
        legalCase.Decision.Should().Be(decision);
        legalCase.DecisionRationale.Should().Be("Begruendung");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_rejects_missing_rationale(string? rationale)
    {
        var legalCase = DomainTestHelpers.CreateLegalCase();

        var result = legalCase.Decide(LegalDecision.KeepOnline, rationale!, DomainTestHelpers.Now.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("legalCase.rationale");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(182, true)]
    [InlineData(183, true)]
    [InlineData(184, false)]
    public void Appeal_window_boundary_is_inclusive_until_deadline(int daysAfterDecision, bool expectedOpen)
    {
        var decidedAt = DomainTestHelpers.Now.AddDays(1);
        var legalCase = DomainTestHelpers.CreateLegalCase();
        legalCase.Decide(LegalDecision.FullyRemoved, "Begruendung", decidedAt);

        legalCase.IsAppealOpen(decidedAt.AddDays(daysAfterDecision)).Should().Be(expectedOpen);
    }

    [Fact]
    public void Appeal_window_closes_one_second_after_exact_deadline()
    {
        var decidedAt = DomainTestHelpers.Now.AddDays(1);
        var legalCase = DomainTestHelpers.CreateLegalCase();
        legalCase.Decide(LegalDecision.FullyRemoved, "Begruendung", decidedAt);

        legalCase.IsAppealOpen(legalCase.AppealDeadlineUtc!.Value).Should().BeTrue();
        legalCase.IsAppealOpen(legalCase.AppealDeadlineUtc.Value.AddSeconds(1)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(AppealOutcomes))]
    public void Appeal_decision_records_outcome_rationale_and_timestamp(AppealOutcome outcome)
    {
        var appeal = LegalCaseAppeal.Create(Guid.NewGuid(), "Bitte nochmal pruefen.", DomainTestHelpers.Now).Value;
        var decidedAt = DomainTestHelpers.Now.AddDays(2);

        var result = appeal.Decide(outcome, "  Ergebnis begruendet. ", decidedAt);

        result.IsSuccess.Should().BeTrue();
        appeal.Status.Should().Be(AppealStatus.Decided);
        appeal.Outcome.Should().Be(outcome);
        appeal.OutcomeRationale.Should().Be("Ergebnis begruendet.");
        appeal.DecidedAtUtc.Should().Be(decidedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Appeal_create_rejects_missing_text(string? text)
    {
        LegalCaseAppeal.Create(Guid.NewGuid(), text!, DomainTestHelpers.Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Appeal_text_exactly_at_limit_is_allowed_and_one_over_is_rejected()
    {
        LegalCaseAppeal.Create(Guid.NewGuid(), new string('a', LegalCaseAppeal.MaxTextLength), DomainTestHelpers.Now).IsSuccess.Should().BeTrue();
        LegalCaseAppeal.Create(Guid.NewGuid(), new string('a', LegalCaseAppeal.MaxTextLength + 1), DomainTestHelpers.Now).IsFailure.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EventCases))]
    public void Legal_case_event_factory_records_sequence_actor_and_payload(LegalCaseEventType eventType, LegalActorType actorType)
    {
        var caseId = Guid.NewGuid();
        Guid? actorId = actorType is LegalActorType.System ? null : Guid.NewGuid();

        var legalEvent = LegalCaseEvent.Create(caseId, 7, eventType, actorType, actorId, "{\"ok\":true}", DomainTestHelpers.Now);

        legalEvent.LegalCaseId.Should().Be(caseId);
        legalEvent.Sequence.Should().Be(7);
        legalEvent.EventType.Should().Be(eventType);
        legalEvent.ActorType.Should().Be(actorType);
        legalEvent.ActorId.Should().Be(actorId);
        legalEvent.DataJson.Should().Be("{\"ok\":true}");
    }

    [Fact]
    public void Appeal_token_hash_can_be_set_and_replaced_without_exposing_token()
    {
        var legalCase = DomainTestHelpers.CreateLegalCase();
        legalCase.SetAppealTokenHash("hash-1", DomainTestHelpers.Now.AddMinutes(1));
        legalCase.SetAppealTokenHash("hash-2", DomainTestHelpers.Now.AddMinutes(2));

        legalCase.StatusTokenHash.Should().Be("status-token-hash");
        legalCase.AppealTokenHash.Should().Be("hash-2");
    }

    // Layering decision: email FORMAT validation is owned by the API-level FluentValidation
    // validators; the entity only guards domain invariants (non-empty, length). Documented here.
    [Fact]
    public void Create_accepts_unvalidated_email_format_by_design()
    {
        LegalCase.Create("WTG-2026-000001", Guid.NewGuid(), LegalCaseCategory.Other, "Name", "not-an-email", "Beschreibung", "status", DomainTestHelpers.Now)
            .IsSuccess.Should().BeTrue();
    }
}
