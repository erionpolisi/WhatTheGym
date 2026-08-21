namespace Gym.Application.Common;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string? GoogleClientId { get; set; }

    public string? GoogleClientSecret { get; set; }

    /// <summary>Verified Google email that becomes the first Admin on login while no Admin exists.</summary>
    public string? BootstrapAdminEmail { get; set; }

    /// <summary>Development-only fake login endpoint. Must never be enabled outside Development.</summary>
    public bool EnableDevLogin { get; set; }

    public int RefreshTokenLifetimeDays { get; set; } = 30;

    public int SessionCookieMinutes { get; set; } = 60;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string FromAddress { get; set; } = "noreply@whatthegym.at";

    public string FromName { get; set; } = "WhatTheGym";

    /// <summary>Resend API key; when empty, mails are logged instead of sent (local development).</summary>
    public string? ResendApiKey { get; set; }

    /// <summary>Public frontend base URL used in mail links (case status, appeals).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:3000";
}

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Years to retain legal case audit events after case closure.</summary>
    public int CaseAuditYears { get; set; } = 7;

    /// <summary>Years to retain review revisions after the review was removed.</summary>
    public int ReviewRevisionYears { get; set; } = 3;

    /// <summary>Days to retain raw analytics events.</summary>
    public int AnalyticsDays { get; set; } = 400;

    /// <summary>Days to retain sent/failed outbox mails.</summary>
    public int OutboxDays { get; set; } = 90;
}

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public string[] AllowedEventTypes { get; set; } =
    [
        "page_view",
        "search_performed",
        "gym_detail_view",
        "review_created",
        "report_submitted",
        "contact_submitted",
    ];
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Apply official Vienna gym catalogue seed.</summary>
    public bool SeedCatalog { get; set; } = true;

    /// <summary>Demo users, reviews and cases. Local/Development only; never staging or production.</summary>
    public bool SeedDemoData { get; set; }
}
