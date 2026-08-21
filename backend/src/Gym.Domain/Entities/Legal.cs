using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Domain.Entities;

public sealed class LegalCase : Entity
{
    public const int MaxDescriptionLength = 4000;

    /// <summary>Appeals stay accessible at least six months after the original decision.</summary>
    public static readonly TimeSpan AppealWindow = TimeSpan.FromDays(183);

    private LegalCase()
    {
        CaseNumber = null!;
        ReporterName = null!;
        ReporterEmail = null!;
        Description = null!;
        StatusTokenHash = null!;
    }

    public string CaseNumber { get; private set; }

    public Guid ReviewId { get; private set; }

    public LegalCaseStatus Status { get; private set; }

    public LegalCaseClassification Classification { get; private set; }

    public LegalCaseCategory Category { get; private set; }

    public string ReporterName { get; private set; }

    public string ReporterEmail { get; private set; }

    public string Description { get; private set; }

    public LegalDecision? Decision { get; private set; }

    public string? DecisionRationale { get; private set; }

    public string StatusTokenHash { get; private set; }

    public string? AppealTokenHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public DateTimeOffset? AppealDeadlineUtc { get; private set; }

    public static Result<LegalCase> Create(
        string caseNumber,
        Guid reviewId,
        LegalCaseCategory category,
        string reporterName,
        string reporterEmail,
        string description,
        string statusTokenHash,
        DateTimeOffset utcNow)
    {
        var name = TextSanitizer.Sanitize(reporterName);
        var email = TextSanitizer.Sanitize(reporterEmail);
        var text = TextSanitizer.Sanitize(description);

        if (name is null || email is null || text is null)
        {
            return Result.Failure<LegalCase>(Error.Validation("legalCase.fields", "Reporter name, email and description are required."));
        }

        if (text.Length > MaxDescriptionLength)
        {
            return Result.Failure<LegalCase>(Error.Validation("legalCase.description", $"Description must not exceed {MaxDescriptionLength} characters."));
        }

        return new LegalCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = caseNumber,
            ReviewId = reviewId,
            Status = LegalCaseStatus.Received,
            Classification = LegalCaseClassification.Unclassified,
            Category = category,
            ReporterName = name,
            ReporterEmail = email,
            Description = text,
            StatusTokenHash = statusTokenHash,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public Result Classify(LegalCaseClassification classification, DateTimeOffset utcNow)
    {
        if (classification == LegalCaseClassification.Unclassified)
        {
            return Result.Failure(Error.Validation("legalCase.classification", "A case cannot be reset to unclassified."));
        }

        if (Status is LegalCaseStatus.Decided or LegalCaseStatus.Closed)
        {
            return Result.Failure(Error.Conflict("legalCase.finalized", "A decided case cannot be reclassified."));
        }

        Classification = classification;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result StartReview(DateTimeOffset utcNow)
    {
        if (Status != LegalCaseStatus.Received)
        {
            return Result.Failure(Error.Conflict("legalCase.status", "Only received cases can move to review."));
        }

        Status = LegalCaseStatus.UnderReview;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Decide(LegalDecision decision, string rationale, DateTimeOffset utcNow)
    {
        if (Status is LegalCaseStatus.Decided or LegalCaseStatus.Closed)
        {
            return Result.Failure(Error.Conflict("legalCase.finalized", "The case has already been decided."));
        }

        var text = TextSanitizer.Sanitize(rationale);
        if (text is null)
        {
            return Result.Failure(Error.Validation("legalCase.rationale", "A documented rationale is required for every decision."));
        }

        Status = LegalCaseStatus.Decided;
        Decision = decision;
        DecisionRationale = text;
        DecidedAtUtc = utcNow;
        AppealDeadlineUtc = utcNow.Add(AppealWindow);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Close(DateTimeOffset utcNow)
    {
        if (Status != LegalCaseStatus.Decided)
        {
            return Result.Failure(Error.Conflict("legalCase.status", "Only decided cases can be closed."));
        }

        Status = LegalCaseStatus.Closed;
        ClosedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public void SetAppealTokenHash(string appealTokenHash, DateTimeOffset utcNow)
    {
        AppealTokenHash = appealTokenHash;
        UpdatedAtUtc = utcNow;
    }

    public bool IsAppealOpen(DateTimeOffset utcNow) =>
        DecidedAtUtc is not null && AppealDeadlineUtc is not null && utcNow <= AppealDeadlineUtc;
}

/// <summary>Append-only audit event. Never updated or deleted; enforced additionally by a database trigger.</summary>
public sealed class LegalCaseEvent : Entity
{
    private LegalCaseEvent()
    {
        DataJson = null!;
    }

    public Guid LegalCaseId { get; private set; }

    public int Sequence { get; private set; }

    public LegalCaseEventType EventType { get; private set; }

    public LegalActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public string DataJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static LegalCaseEvent Create(
        Guid legalCaseId,
        int sequence,
        LegalCaseEventType eventType,
        LegalActorType actorType,
        Guid? actorId,
        string dataJson,
        DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        LegalCaseId = legalCaseId,
        Sequence = sequence,
        EventType = eventType,
        ActorType = actorType,
        ActorId = actorId,
        DataJson = dataJson,
        CreatedAtUtc = utcNow,
    };
}

public sealed class LegalCaseAppeal : Entity
{
    public const int MaxTextLength = 4000;

    private LegalCaseAppeal()
    {
        Text = null!;
    }

    public Guid LegalCaseId { get; private set; }

    public string Text { get; private set; }

    public AppealStatus Status { get; private set; }

    public AppealOutcome? Outcome { get; private set; }

    public string? OutcomeRationale { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public static Result<LegalCaseAppeal> Create(Guid legalCaseId, string text, DateTimeOffset utcNow)
    {
        var sanitized = TextSanitizer.Sanitize(text);
        if (sanitized is null)
        {
            return Result.Failure<LegalCaseAppeal>(Error.Validation("appeal.text", "Appeal text is required."));
        }

        if (sanitized.Length > MaxTextLength)
        {
            return Result.Failure<LegalCaseAppeal>(Error.Validation("appeal.text", $"Appeal text must not exceed {MaxTextLength} characters."));
        }

        return new LegalCaseAppeal
        {
            Id = Guid.NewGuid(),
            LegalCaseId = legalCaseId,
            Text = sanitized,
            Status = AppealStatus.Received,
            CreatedAtUtc = utcNow,
        };
    }

    public Result Decide(AppealOutcome outcome, string rationale, DateTimeOffset utcNow)
    {
        if (Status == AppealStatus.Decided)
        {
            return Result.Failure(Error.Conflict("appeal.decided", "The appeal has already been decided."));
        }

        var text = TextSanitizer.Sanitize(rationale);
        if (text is null)
        {
            return Result.Failure(Error.Validation("appeal.rationale", "A documented rationale is required."));
        }

        Status = AppealStatus.Decided;
        Outcome = outcome;
        OutcomeRationale = text;
        DecidedAtUtc = utcNow;
        return Result.Success();
    }
}

/// <summary>Pauses retention-based deletion for the linked case, review or user while active.</summary>
public sealed class LegalHold : Entity
{
    private LegalHold()
    {
        Reason = null!;
    }

    public string Reason { get; private set; }

    public Guid? LegalCaseId { get; private set; }

    public Guid? ReviewId { get; private set; }

    public Guid? UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public bool IsActive => ReleasedAtUtc is null;

    public static LegalHold Create(string reason, Guid? legalCaseId, Guid? reviewId, Guid? userId, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        Reason = reason.Trim(),
        LegalCaseId = legalCaseId,
        ReviewId = reviewId,
        UserId = userId,
        CreatedAtUtc = utcNow,
    };

    public void Release(DateTimeOffset utcNow) => ReleasedAtUtc ??= utcNow;
}

public sealed class LegalDocument : Entity
{
    private LegalDocument()
    {
        Title = null!;
        ContentMarkdown = null!;
    }

    public LegalDocumentType Type { get; private set; }

    public int Version { get; private set; }

    public string Title { get; private set; }

    public string ContentMarkdown { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public static LegalDocument CreateDraft(LegalDocumentType type, int version, string title, string contentMarkdown, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Version = version,
        Title = title.Trim(),
        ContentMarkdown = contentMarkdown,
        IsPublished = false,
        CreatedAtUtc = utcNow,
    };

    public void Publish(DateTimeOffset utcNow)
    {
        IsPublished = true;
        PublishedAtUtc ??= utcNow;
    }
}
