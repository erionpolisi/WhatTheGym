using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public class LegalFlowTests(WtgApiFactory factory)
{
    private async Task<(Guid ReviewId, HttpClient AuthorClient)> CreateReviewAsync(string gymSlug, string authorEmail)
    {
        var author = await factory.LoginAsync(authorEmail, "Autorin");
        var create = await author.PostAsJsonAsync($"/api/v1/gyms/{gymSlug}/reviews", new
        {
            ratings = new { equipment = 1, staff = 1 },
            text = "Angeblich unhaltbare Zustaende hier.",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var node = (await create.Content.ReadFromJsonAsync<JsonNode>())!;
        return (node["id"]!.GetValue<Guid>(), author);
    }

    private async Task<(string CaseNumber, string StatusToken, Guid CaseId)> ReportAsync(HttpClient client, Guid reviewId)
    {
        var report = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Defamation",
            reporterName = "Studio Betreiber",
            reporterEmail = "betreiber@example.invalid",
            description = "Diese Bewertung enthaelt falsche Tatsachenbehauptungen ueber unser Studio.",
        });
        report.StatusCode.Should().Be(HttpStatusCode.Created);
        var node = (await report.Content.ReadFromJsonAsync<JsonNode>())!;
        var caseNumber = node["caseNumber"]!.GetValue<string>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseId = (await db.LegalCases.SingleAsync(c => c.CaseNumber == caseNumber)).Id;
        return (caseNumber, node["statusToken"]!.GetValue<string>(), caseId);
    }

    [Fact]
    public async Task Normal_report_keeps_content_online_until_decision_then_keep_online()
    {
        const string slug = "mcfit-wien-favoriten";
        var (reviewId, _) = await CreateReviewAsync(slug, "author-keep@example.invalid");
        var anonymous = factory.CreateClient();

        var (caseNumber, statusToken, caseId) = await ReportAsync(anonymous, reviewId);
        caseNumber.Should().MatchRegex(@"^WTG-\d{4}-\d{6}$");

        // Content stays online while the normal report is reviewed.
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().HaveCount(1);

        // Public case status works only with the correct token.
        var status = await anonymous.GetJsonAsync($"/api/v1/legal/cases/{caseNumber}/status?token={statusToken}");
        status["status"]!.GetValue<string>().Should().Be("Received");
        (await anonymous.GetAsync(new Uri($"/api/v1/legal/cases/{caseNumber}/status?token=wrongtoken", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Admin decides KeepOnline with a documented rationale.
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/start-review", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "KeepOnline",
            rationale = "Zulaessige Meinungsaeusserung ohne rechtswidrigen Inhalt.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Still online, decision visible via status token.
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().HaveCount(1);
        var decided = await anonymous.GetJsonAsync($"/api/v1/legal/cases/{caseNumber}/status?token={statusToken}");
        decided["decision"]!.GetValue<string>().Should().Be("KeepOnline");
        decided["appealDeadlineUtc"].Should().NotBeNull();

        // The audit trail is complete and append-only.
        var detail = await admin.GetJsonAsync($"/api/v1/admin/legal-cases/{caseId}");
        var eventTypes = detail["events"]!.AsArray().Select(e => e!["eventType"]!.GetValue<string>()).ToList();
        eventTypes.Should().Contain(["CaseCreated", "ReviewStarted", "Decided", "NotificationQueued"]);
        detail["events"]!.AsArray().Select(e => e!["sequence"]!.GetValue<int>()).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Fast_track_hides_content_and_full_removal_supports_tokenized_appeal()
    {
        const string slug = "mcfit-wien-landstrasse";
        var (reviewId, _) = await CreateReviewAsync(slug, "author-removed@example.invalid");
        var anonymous = factory.CreateClient();
        var (caseNumber, _, caseId) = await ReportAsync(anonymous, reviewId);

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        // Explicit fast-track classification hides the review before the decision.
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/classify", new
        {
            classification = "FastTrackObviouslyIllegal",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().BeEmpty();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/summary"))["reviewCount"]!.GetValue<int>().Should().Be(0);

        // Full removal.
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "FullyRemoved",
            rationale = "Offensichtlich rechtswidriger Inhalt (Schmaehkritik).",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().BeEmpty();

        // The author notification contains the appeal link; extract the token like the mail recipient would.
        string appealToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var authorMail = await db.OutboxEmails.SingleAsync(o => o.LegalCaseId == caseId && o.Kind == "legal.decision.author");
            var match = Regex.Match(authorMail.BodyText, @"token=([0-9a-f]{64})");
            match.Success.Should().BeTrue("the author mail must contain the appeal link");
            appealToken = match.Groups[1].Value;
        }

        // Appeal with the token from the notification.
        var appeal = await anonymous.PostAsJsonAsync($"/api/v1/legal/cases/{caseNumber}/appeal", new
        {
            token = appealToken,
            text = "Die Bewertung beruht auf wahren Tatsachen; ich lege Einspruch ein.",
        });
        appeal.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wrong token is rejected.
        (await anonymous.PostAsJsonAsync($"/api/v1/legal/cases/{caseNumber}/appeal", new
        {
            token = new string('a', 64),
            text = "Ungueltiger Versuch.",
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Admin reverses the decision -> review is reinstated.
        var detail = await admin.GetJsonAsync($"/api/v1/admin/legal-cases/{caseId}");
        var appealId = detail["appeals"]!.AsArray().Single()!["id"]!.GetValue<Guid>();
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/appeals/{appealId}/decide", new
        {
            outcome = "DecisionReversed",
            rationale = "Wahrheitsbeweis erbracht; Entscheidung aufgehoben.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task Case_can_be_closed_and_exported_and_transparency_counts()
    {
        const string slug = "clever-fit-wien-donaustadt";
        var (reviewId, _) = await CreateReviewAsync(slug, "author-closed@example.invalid");
        var anonymous = factory.CreateClient();
        var (_, _, caseId) = await ReportAsync(anonymous, reviewId);

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "KeepOnline",
            rationale = "Kein Rechtsverstoss erkennbar.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/close", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var export = await admin.GetAsync(new Uri($"/api/v1/admin/legal-cases/{caseId}/export", UriKind.Relative));
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var report = await anonymous.GetJsonAsync("/api/v1/legal/transparency-report?year=2026");
        report["totalReports"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Moderators_may_view_but_not_decide_cases()
    {
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var moderator = await factory.LoginAsync("moderator@example.invalid", "Moderatorin");

        // Promote to moderator via admin role management.
        var users = await admin.GetJsonAsync("/api/v1/admin/users?pageSize=100");
        var moderatorId = users["items"]!.AsArray()
            .Single(u => u!["email"]!.GetValue<string>() == "moderator@example.invalid")!["id"]!.GetValue<Guid>();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{moderatorId}/role", new { role = "Moderator" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Re-login to refresh role claims.
        moderator = await factory.LoginAsync("moderator@example.invalid", "Moderatorin");

        (await moderator.GetAsync(new Uri("/api/v1/admin/legal-cases", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await moderator.PostAsJsonAsync($"/api/v1/admin/legal-cases/{Guid.NewGuid()}/decide", new
        {
            decision = "KeepOnline",
            rationale = "x",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
