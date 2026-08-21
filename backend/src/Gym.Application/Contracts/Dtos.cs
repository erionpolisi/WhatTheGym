using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;

namespace Gym.Application.Contracts;

public sealed record ChainDto(Guid Id, string Name, string Slug, string? Website);

public sealed record AmenityDto(Guid Id, string Name, string Slug);

public sealed record OpeningHourDto(int IsoDayOfWeek, string OpensAt, string ClosesAt);

public sealed record CategoryScoreDto(string Category, string Area, double? Average, int RatingCount);

public sealed record ScoreSummaryDto(
    double? TotalScore,
    double? MembershipScore,
    double? StudioScore,
    string ScoreBasis,
    int ReviewCount,
    IReadOnlyList<CategoryScoreDto> Categories)
{
    public static ScoreSummaryDto From(GymScoreResult score) => new(
        score.TotalScore,
        score.MembershipScore,
        score.StudioScore,
        ToBasisString(score.Basis),
        score.ReviewCount,
        score.Categories
            .Select(c => new CategoryScoreDto(
                CamelCase(c.Category.ToString()),
                RatingCategories.IsMembership(c.Category) ? "membership" : "studio",
                c.Average,
                c.RatingCount))
            .ToList());

    public static string ToBasisString(Domain.Enums.ScoreBasis basis) => basis switch
    {
        Domain.Enums.ScoreBasis.Both => "both",
        Domain.Enums.ScoreBasis.MembershipOnly => "membershipOnly",
        Domain.Enums.ScoreBasis.StudioOnly => "studioOnly",
        _ => "none",
    };

    private static string CamelCase(string value) =>
        string.Create(value.Length, value, (span, s) =>
        {
            s.AsSpan().CopyTo(span);
            span[0] = char.ToLowerInvariant(span[0]);
        });
}

public sealed record GymListItemDto(
    Guid Id,
    string Name,
    string Slug,
    int District,
    string AddressLine,
    string PostalCode,
    string Status,
    string? ChainName,
    string? ChainSlug,
    int ReviewCount,
    double? TotalScore,
    double? MembershipScore,
    double? StudioScore,
    string ScoreBasis);

public sealed record GymDetailDto(
    Guid Id,
    string Name,
    string Slug,
    int District,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? Website,
    string? Phone,
    string? Description,
    string Status,
    ChainDto? Chain,
    IReadOnlyList<AmenityDto> Amenities,
    IReadOnlyList<OpeningHourDto> OpeningHours,
    ScoreSummaryDto Score,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RatingsDto(
    int? PriceValue,
    int? ContractTerms,
    int? Billing,
    int? CancellationExperience,
    int? Equipment,
    int? Cleanliness,
    int? Staff,
    int? Crowding,
    int? ChangingRoom,
    int? Showers,
    int? Atmosphere)
{
    public ReviewRatings ToDomain() => new()
    {
        PriceValue = PriceValue,
        ContractTerms = ContractTerms,
        Billing = Billing,
        CancellationExperience = CancellationExperience,
        Equipment = Equipment,
        Cleanliness = Cleanliness,
        Staff = Staff,
        Crowding = Crowding,
        ChangingRoom = ChangingRoom,
        Showers = Showers,
        Atmosphere = Atmosphere,
    };

    public static RatingsDto From(ReviewRatings r) => new(
        r.PriceValue,
        r.ContractTerms,
        r.Billing,
        r.CancellationExperience,
        r.Equipment,
        r.Cleanliness,
        r.Staff,
        r.Crowding,
        r.ChangingRoom,
        r.Showers,
        r.Atmosphere);
}

public sealed record ReviewAuthorDto(string DisplayName, bool VerifiedViaGoogle);

public sealed record ReviewDto(
    Guid Id,
    Guid GymId,
    ReviewAuthorDto Author,
    RatingsDto Ratings,
    string? Text,
    int EditCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OwnReviewDto(
    Guid Id,
    Guid GymId,
    string Status,
    RatingsDto Ratings,
    string? Text,
    int EditCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ModerationReviewDto(
    Guid Id,
    Guid GymId,
    string GymName,
    string GymSlug,
    Guid UserId,
    string Status,
    RatingsDto Ratings,
    string? Text,
    string? DeletionOrigin,
    string? DeletionReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MeDto(
    Guid Id,
    string Email,
    bool EmailVerified,
    string DisplayName,
    string Role);

public sealed record UserAdminDto(
    Guid Id,
    string Email,
    bool EmailVerified,
    string DisplayName,
    string Role,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record ReportReviewResultDto(string CaseNumber, string StatusToken);

public sealed record LegalCaseStatusPublicDto(
    string CaseNumber,
    string Status,
    string? Decision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? AppealDeadlineUtc);

public sealed record LegalCaseListItemDto(
    Guid Id,
    string CaseNumber,
    Guid ReviewId,
    string Status,
    string Classification,
    string Category,
    string? Decision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc);

public sealed record LegalCaseEventDto(
    int Sequence,
    string EventType,
    string ActorType,
    Guid? ActorId,
    string DataJson,
    DateTimeOffset CreatedAtUtc);

public sealed record LegalCaseAppealDto(
    Guid Id,
    string Status,
    string? Outcome,
    string Text,
    string? OutcomeRationale,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc);

public sealed record LegalCaseDetailDto(
    Guid Id,
    string CaseNumber,
    Guid ReviewId,
    string Status,
    string Classification,
    string Category,
    string ReporterName,
    string ReporterEmail,
    string Description,
    string? Decision,
    string? DecisionRationale,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset? AppealDeadlineUtc,
    IReadOnlyList<LegalCaseEventDto> Events,
    IReadOnlyList<LegalCaseAppealDto> Appeals);

public sealed record TransparencyReportDto(
    int Year,
    int TotalReports,
    int KeptOnline,
    int FullyRemoved,
    int PendingCases,
    int FastTrackCases,
    int AppealsSubmitted,
    int AppealsReversed,
    string Notes);

public sealed record LegalDocumentDto(
    Guid Id,
    string Type,
    int Version,
    string Title,
    string ContentMarkdown,
    bool IsPublished,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

public sealed record ContactRequestDto(
    Guid Id,
    string Type,
    string Name,
    string Email,
    string Message,
    Guid? GymId,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record PersonalDataExportDto(
    DateTimeOffset ExportedAtUtc,
    MeDto Account,
    IReadOnlyList<OwnReviewDto> Reviews,
    IReadOnlyList<object> ReviewRevisions,
    IReadOnlyList<object> LegalCasesAsReporter,
    IReadOnlyList<object> ContactRequests);
