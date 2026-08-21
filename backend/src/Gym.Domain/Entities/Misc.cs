using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Domain.Entities;

public sealed class ContactRequest : Entity
{
    public const int MaxMessageLength = 4000;

    private ContactRequest()
    {
        Name = null!;
        Email = null!;
        Message = null!;
    }

    public ContactRequestType Type { get; private set; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public string Message { get; private set; }

    public Guid? GymId { get; private set; }

    public ContactRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public static Result<ContactRequest> Create(ContactRequestType type, string name, string email, string message, Guid? gymId, DateTimeOffset utcNow)
    {
        var sanitizedName = TextSanitizer.Sanitize(name);
        var sanitizedEmail = TextSanitizer.Sanitize(email);
        var sanitizedMessage = TextSanitizer.Sanitize(message);

        if (sanitizedName is null || sanitizedEmail is null || sanitizedMessage is null)
        {
            return Result.Failure<ContactRequest>(Error.Validation("contact.fields", "Name, email and message are required."));
        }

        if (sanitizedMessage.Length > MaxMessageLength)
        {
            return Result.Failure<ContactRequest>(Error.Validation("contact.message", $"Message must not exceed {MaxMessageLength} characters."));
        }

        return new ContactRequest
        {
            Id = Guid.NewGuid(),
            Type = type,
            Name = sanitizedName,
            Email = sanitizedEmail,
            Message = sanitizedMessage,
            GymId = gymId,
            Status = ContactRequestStatus.New,
            CreatedAtUtc = utcNow,
        };
    }

    public void SetStatus(ContactRequestStatus status, DateTimeOffset utcNow)
    {
        Status = status;
        ResolvedAtUtc = status == ContactRequestStatus.Resolved ? utcNow : null;
    }
}

/// <summary>PII-free analytics: allowlisted event type, no IP, hashed short-lived session bucket.</summary>
public sealed class AnalyticsEvent
{
    private AnalyticsEvent()
    {
        EventType = null!;
        SessionBucket = null!;
    }

    public long Id { get; private set; }

    public string EventType { get; private set; }

    public string? Path { get; private set; }

    public string SessionBucket { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AnalyticsEvent Create(string eventType, string? path, string sessionBucket, DateTimeOffset utcNow) => new()
    {
        EventType = eventType,
        Path = path,
        SessionBucket = sessionBucket,
        OccurredAtUtc = utcNow,
    };
}

public sealed class OutboxEmail : Entity
{
    public const int MaxAttempts = 8;

    private OutboxEmail()
    {
        ToEmail = null!;
        Subject = null!;
        BodyText = null!;
        Kind = null!;
    }

    public string ToEmail { get; private set; }

    public string Subject { get; private set; }

    public string BodyText { get; private set; }

    /// <summary>Logical mail kind, e.g. "legal.reportReceived".</summary>
    public string Kind { get; private set; }

    public OutboxEmailStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public Guid? LegalCaseId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public static OutboxEmail Enqueue(string toEmail, string subject, string bodyText, string kind, Guid? legalCaseId, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        ToEmail = toEmail,
        Subject = subject,
        BodyText = bodyText,
        Kind = kind,
        Status = OutboxEmailStatus.Pending,
        AttemptCount = 0,
        NextAttemptAtUtc = utcNow,
        LegalCaseId = legalCaseId,
        CreatedAtUtc = utcNow,
    };

    public void MarkSent(DateTimeOffset utcNow)
    {
        Status = OutboxEmailStatus.Sent;
        SentAtUtc = utcNow;
        LastError = null;
    }

    /// <summary>Registers a failed attempt with exponential backoff; gives up after <see cref="MaxAttempts"/>.</summary>
    public void MarkAttemptFailed(string error, DateTimeOffset utcNow)
    {
        AttemptCount++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        if (AttemptCount >= MaxAttempts)
        {
            Status = OutboxEmailStatus.Failed;
        }
        else
        {
            var backoff = TimeSpan.FromSeconds(Math.Pow(2, AttemptCount) * 30);
            NextAttemptAtUtc = utcNow.Add(backoff);
        }
    }
}
