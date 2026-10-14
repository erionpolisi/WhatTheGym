namespace Gym.Domain.Enums;

public enum UserRole
{
    User,
    Moderator,
    Admin,
}

public enum UserStatus
{
    Active,
    Deleted,
}

public enum GymStatus
{
    Draft,
    Active,
    TemporarilyClosed,
    PermanentlyClosed,
}

[Flags]
public enum TrainingType
{
    None = 0,
    Strength = 1 << 0,
    Cardio = 1 << 1,
    Functional = 1 << 2,
    GroupClasses = 1 << 3,
    CrossFit = 1 << 4,
    CircuitTraining = 1 << 5,
    PersonalTraining = 1 << 6,
    Wellness = 1 << 7,
}

public enum TargetAudience
{
    General,
    WomenOnly,
}

public enum ReviewStatus
{
    Published,
    SoftDeleted,
    UnderReview,
    RemovedLegal,
}

public enum ReviewDeletionOrigin
{
    Author,
    Moderator,
    Admin,
    AccountDeletion,
}

public enum ScoreBasis
{
    None,
    MembershipOnly,
    StudioOnly,
    Both,
}

public enum RatingCategory
{
    // Membership area
    PriceValue,
    ContractTerms,
    Billing,
    CancellationExperience,

    // Studio area
    Equipment,
    Cleanliness,
    Staff,
    Crowding,
    ChangingRoom,
    Showers,
    Atmosphere,
}

public enum LegalCaseStatus
{
    Received,
    UnderReview,
    Decided,
    Closed,
}

public enum LegalCaseClassification
{
    Unclassified,
    Normal,
    FastTrackObviouslyIllegal,
}

public enum LegalCaseCategory
{
    Defamation,
    FalseFactualClaim,
    Insult,
    PrivacyViolation,
    IllegalContent,
    Other,
}

public enum LegalDecision
{
    KeepOnline,
    FullyRemoved,
}

public enum LegalCaseEventType
{
    CaseCreated,
    Classified,
    ReviewStarted,
    ContentHidden,
    ContentRestored,
    Decided,
    NotificationQueued,
    AppealSubmitted,
    AppealDecided,
    Closed,
    NoteAdded,
    LegalHoldApplied,
    LegalHoldReleased,
}

public enum LegalActorType
{
    System,
    Reporter,
    Author,
    Moderator,
    Admin,
}

public enum AppealStatus
{
    Received,
    UnderReview,
    Decided,
}

public enum AppealOutcome
{
    DecisionUpheld,
    DecisionReversed,
}

public enum ContactRequestType
{
    General,
    GymSuggestion,
    DataCorrection,
}

public enum ContactRequestStatus
{
    New,
    InProgress,
    Resolved,
}

public enum LegalDocumentType
{
    Imprint,
    PrivacyPolicy,
    TermsOfUse,
}

public enum OutboxEmailStatus
{
    Pending,
    Sent,
    Failed,
}
