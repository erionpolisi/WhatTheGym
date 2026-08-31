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
