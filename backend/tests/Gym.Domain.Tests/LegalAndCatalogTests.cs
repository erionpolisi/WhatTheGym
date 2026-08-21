using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Domain.Tests;

public class LegalCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private static LegalCase CreateCase() =>
        LegalCase.Create(
            "WTG-2026-000042", Guid.NewGuid(), LegalCaseCategory.Defamation,
            "Melderin", "melderin@example.com", new string('a', 30), "hash", Now).Value;

    [Fact]
    public void New_case_is_received_and_unclassified()
    {
        var legalCase = CreateCase();

        legalCase.Status.Should().Be(LegalCaseStatus.Received);
        legalCase.Classification.Should().Be(LegalCaseClassification.Unclassified);
        legalCase.Decision.Should().BeNull();
    }

    [Fact]
    public void Decide_requires_documented_rationale()
    {
        var legalCase = CreateCase();

        legalCase.Decide(LegalDecision.KeepOnline, "   ", Now).IsFailure.Should().BeTrue();
        legalCase.Decide(LegalDecision.KeepOnline, "Nach Pruefung zulaessige Meinungsaeusserung.", Now).IsSuccess.Should().BeTrue();
        legalCase.Status.Should().Be(LegalCaseStatus.Decided);
    }

    [Fact]
    public void Decision_opens_appeal_window_of_at_least_six_months()
    {
        var legalCase = CreateCase();
        legalCase.Decide(LegalDecision.FullyRemoved, "Rechtswidriger Inhalt.", Now);

        legalCase.AppealDeadlineUtc.Should().NotBeNull();
        (legalCase.AppealDeadlineUtc!.Value - Now).Should().BeGreaterThanOrEqualTo(TimeSpan.FromDays(180));
        legalCase.IsAppealOpen(Now.AddDays(180)).Should().BeTrue();
        legalCase.IsAppealOpen(Now.AddDays(200)).Should().BeFalse();
    }

    [Fact]
    public void Decided_case_cannot_be_decided_or_reclassified_again()
    {
        var legalCase = CreateCase();
        legalCase.Decide(LegalDecision.KeepOnline, "Begruendung.", Now);

        legalCase.Decide(LegalDecision.FullyRemoved, "Anders.", Now).IsFailure.Should().BeTrue();
        legalCase.Classify(LegalCaseClassification.Normal, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Close_is_only_possible_after_decision()
    {
        var legalCase = CreateCase();

        legalCase.Close(Now).IsFailure.Should().BeTrue();

        legalCase.Decide(LegalDecision.KeepOnline, "Begruendung.", Now);
        legalCase.Close(Now).IsSuccess.Should().BeTrue();
        legalCase.Status.Should().Be(LegalCaseStatus.Closed);
    }

    [Fact]
    public void Status_flow_received_underReview_decided()
    {
        var legalCase = CreateCase();

        legalCase.StartReview(Now).IsSuccess.Should().BeTrue();
        legalCase.Status.Should().Be(LegalCaseStatus.UnderReview);
        legalCase.StartReview(Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Classification_cannot_be_reset_to_unclassified()
    {
        var legalCase = CreateCase();

        legalCase.Classify(LegalCaseClassification.FastTrackObviouslyIllegal, Now).IsSuccess.Should().BeTrue();
        legalCase.Classify(LegalCaseClassification.Unclassified, Now).IsFailure.Should().BeTrue();
    }
}

public class AppealTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Appeal_requires_text_and_single_decision()
    {
        LegalCaseAppeal.Create(Guid.NewGuid(), "  ", Now).IsFailure.Should().BeTrue();

        var appeal = LegalCaseAppeal.Create(Guid.NewGuid(), "Die Entscheidung ist falsch.", Now).Value;
        appeal.Decide(AppealOutcome.DecisionUpheld, "Bleibt bestehen.", Now).IsSuccess.Should().BeTrue();
        appeal.Decide(AppealOutcome.DecisionReversed, "Nochmal.", Now).IsFailure.Should().BeTrue();
    }
}

public class OutboxEmailTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Failed_attempts_backoff_and_eventually_give_up()
    {
        var email = OutboxEmail.Enqueue("a@b.c", "Betreff", "Text", "test", null, Now);

        for (var attempt = 1; attempt < OutboxEmail.MaxAttempts; attempt++)
        {
            email.MarkAttemptFailed("boom", Now);
            email.Status.Should().Be(OutboxEmailStatus.Pending);
            email.NextAttemptAtUtc.Should().BeAfter(Now);
        }

        email.MarkAttemptFailed("boom", Now);
        email.Status.Should().Be(OutboxEmailStatus.Failed);
        email.AttemptCount.Should().Be(OutboxEmail.MaxAttempts);
    }

    [Fact]
    public void Sent_mail_records_timestamp_and_clears_error()
    {
        var email = OutboxEmail.Enqueue("a@b.c", "Betreff", "Text", "test", null, Now);
        email.MarkAttemptFailed("boom", Now);

        email.MarkSent(Now.AddMinutes(5));

        email.Status.Should().Be(OutboxEmailStatus.Sent);
        email.SentAtUtc.Should().Be(Now.AddMinutes(5));
        email.LastError.Should().BeNull();
    }
}

public class GymEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    public void District_must_be_between_1_and_23(int district)
    {
        var result = GymEntry.Create("Test Gym", "test-gym", null, district, "Strasse 1", "1100", null, null, null, GymStatus.Active, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Draft_gyms_are_not_public_and_closed_gyms_accept_no_reviews()
    {
        var draft = GymEntry.Create("A", "a", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.Draft, Now).Value;
        var closed = GymEntry.Create("B", "b", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.PermanentlyClosed, Now).Value;
        var active = GymEntry.Create("C", "c", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.Active, Now).Value;

        draft.IsPubliclyVisible.Should().BeFalse();
        closed.IsPubliclyVisible.Should().BeTrue();
        closed.AcceptsReviews.Should().BeFalse();
        active.AcceptsReviews.Should().BeTrue();
    }

    [Fact]
    public void Opening_hours_validate_day_and_range()
    {
        GymOpeningHour.Create(0, new TimeOnly(8, 0), new TimeOnly(20, 0)).IsFailure.Should().BeTrue();
        GymOpeningHour.Create(8, new TimeOnly(8, 0), new TimeOnly(20, 0)).IsFailure.Should().BeTrue();
        GymOpeningHour.Create(1, new TimeOnly(20, 0), new TimeOnly(8, 0)).IsFailure.Should().BeTrue();
        GymOpeningHour.Create(1, new TimeOnly(8, 0), new TimeOnly(20, 0)).IsSuccess.Should().BeTrue();
    }
}
