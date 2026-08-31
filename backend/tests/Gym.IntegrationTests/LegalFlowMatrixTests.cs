using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public sealed class LegalFlowMatrixTests(WtgApiFactory factory)
{
    private async Task<(string Slug, Guid ReviewId, HttpClient Author)> CreateReviewForCaseAsync(string prefix)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, prefix, district: 11);
        var author = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail(prefix.ToLowerInvariant()), "Fall Autorin");
        var create = await author.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new
        {
            ratings = new { equipment = 1, cleanliness = 2 },
            text = "Diese Bewertung enthaelt eine konkrete Beschwerde fuer den Rechtsprozess.",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return (slug, (await IntegrationTestSupport.ReadJsonAsync(create))["id"]!.GetValue<Guid>(), author);
    }

    private async Task<(string CaseNumber, string StatusToken, Guid CaseId)> ReportAsync(Guid reviewId, string category = "Defamation")
    {
        var client = factory.CreateClient();
        var report = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category,
            reporterName = "Studio Betreiber",
            reporterEmail = IntegrationTestSupport.UniqueEmail("reporter"),
            description = "Diese Meldung beschreibt einen konkret beanstandeten Inhalt ausreichend genau.",
        });
        report.StatusCode.Should().Be(HttpStatusCode.Created);
        var node = await IntegrationTestSupport.ReadJsonAsync(report);
        var caseNumber = node["caseNumber"]!.GetValue<string>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseId = (await db.LegalCases.SingleAsync(c => c.CaseNumber == caseNumber)).Id;
        return (caseNumber, node["statusToken"]!.GetValue<string>(), caseId);
    }

    [Theory]
    [InlineData("Defamation")]
    [InlineData("FalseFactualClaim")]
    [InlineData("Insult")]
    [InlineData("PrivacyViolation")]
    [InlineData("IllegalContent")]
    [InlineData("Other")]
    public async Task Report_categories_create_status_token_and_admin_detail(string category)
    {
        var (_, reviewId, _) = await CreateReviewForCaseAsync("Legal Category");

        var (caseNumber, statusToken, caseId) = await ReportAsync(reviewId, category);

        caseNumber.Should().MatchRegex(@"^WTG-\d{4}-\d{6}$");
        var status = await factory.CreateClient().GetJsonAsync($"/api/v1/legal/cases/{caseNumber}/status?token={statusToken}");
        status["status"]!.GetValue<string>().Should().Be("Received");
        var detail = await (await factory.LoginAsync("admin@example.invalid", "Admin")).GetJsonAsync($"/api/v1/admin/legal-cases/{caseId}");
        detail["category"]!.GetValue<string>().Should().Be(category);
        detail["events"]!.AsArray().Should().Contain(e => e!["eventType"]!.GetValue<string>() == "CaseCreated");
    }

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("FastTrackObviouslyIllegal", false)]
    public async Task Classification_controls_public_visibility(string classification, bool remainsPublic)
    {
        var (slug, reviewId, _) = await CreateReviewForCaseAsync("Legal Classify");
        var (_, _, caseId) = await ReportAsync(reviewId);
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/classify", new { classification }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reviews = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews");
        reviews["items"]!.AsArray().Any(i => i!["id"]!.GetValue<Guid>() == reviewId).Should().Be(remainsPublic);
    }

    [Theory]
    [InlineData("KeepOnline", true, "Published")]
    [InlineData("FullyRemoved", false, "RemovedLegal")]
    public async Task Decisions_update_public_and_author_visible_review_state(string decision, bool remainsPublic, string expectedOwnStatus)
    {
        var (slug, reviewId, author) = await CreateReviewForCaseAsync("Legal Decision");
        var (_, _, caseId) = await ReportAsync(reviewId);
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision,
            rationale = "Dokumentierte und nachvollziehbare Entscheidung im Integrationstest.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reviews = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews");
        reviews["items"]!.AsArray().Any(i => i!["id"]!.GetValue<Guid>() == reviewId).Should().Be(remainsPublic);
        var own = await author.GetJsonAsync("/api/v1/me/reviews");
        own.AsArray().Single(i => i!["id"]!.GetValue<Guid>() == reviewId)!["status"]!.GetValue<string>().Should().Be(expectedOwnStatus);
    }

    [Theory]
    [InlineData("classify-unclassified", HttpStatusCode.BadRequest)]
    [InlineData("classify-unknown", HttpStatusCode.BadRequest)]
    [InlineData("decide-unknown", HttpStatusCode.BadRequest)]
    [InlineData("decide-empty-rationale", HttpStatusCode.BadRequest)]
    [InlineData("close-before-decision", HttpStatusCode.Conflict)]
    [InlineData("start-twice", HttpStatusCode.Conflict)]
    [InlineData("decide-twice", HttpStatusCode.Conflict)]
    [InlineData("appeal-before-decision", HttpStatusCode.NotFound)]
    public async Task Invalid_legal_transitions_return_4xx(string scenario, HttpStatusCode expectedStatus)
    {
        var (_, reviewId, _) = await CreateReviewForCaseAsync("Legal Invalid");
        var (caseNumber, _, caseId) = await ReportAsync(reviewId);
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        var response = scenario switch
        {
            "classify-unclassified" => await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/classify", new { classification = "Unclassified" }),
            "classify-unknown" => await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/classify", new { classification = "Nope" }),
            "decide-unknown" => await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new { decision = "Nope", rationale = "ungueltig" }),
            "decide-empty-rationale" => await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new { decision = "KeepOnline", rationale = "" }),
            "close-before-decision" => await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/close", new { }),
            "start-twice" => await StartTwiceAsync(admin, caseId),
            "decide-twice" => await DecideTwiceAsync(admin, caseId),
            _ => await factory.CreateClient().PostAsJsonAsync($"/api/v1/legal/cases/{caseNumber}/appeal", new { token = new string('a', 64), text = "Einspruch vor Entscheidung." }),
        };

        response.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Normal_flow_detail_events_grow_monotonically_and_transparency_updates_after_close()
    {
        var (slug, reviewId, _) = await CreateReviewForCaseAsync("Legal Normal Flow");
        var (caseNumber, statusToken, caseId) = await ReportAsync(reviewId);
        var anonymous = factory.CreateClient();
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        (await anonymous.GetAsync(new Uri($"/api/v1/legal/cases/{caseNumber}/status?token=wrong", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/start-review", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "KeepOnline",
            rationale = "Kein Rechtsverstoss erkennbar.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/close", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await admin.GetJsonAsync($"/api/v1/admin/legal-cases/{caseId}");
        detail["events"]!.AsArray().Select(e => e!["sequence"]!.GetValue<int>()).Should().BeInAscendingOrder();
        detail["events"]!.AsArray().Should().OnlyHaveUniqueItems(e => e!["sequence"]!.GetValue<int>());
        (await anonymous.GetJsonAsync($"/api/v1/legal/cases/{caseNumber}/status?token={statusToken}"))["status"]!.GetValue<string>().Should().Be("Closed");
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().Contain(i => i!["id"]!.GetValue<Guid>() == reviewId);
        (await anonymous.GetJsonAsync("/api/v1/legal/transparency-report?year=2026"))["totalReports"]!.GetValue<int>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Honeypot_report_is_accepted_but_no_case_is_created()
    {
        var (_, reviewId, _) = await CreateReviewForCaseAsync("Legal Honeypot");
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var before = (await admin.GetJsonAsync("/api/v1/admin/legal-cases?pageSize=100"))["totalCount"]!.GetValue<int>();

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Other",
            reporterName = "Bot",
            reporterEmail = "bot@example.invalid",
            description = "Bot fuellt das versteckte Feld und darf keinen Fall erzeugen.",
            website = "https://spam.example",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var after = (await admin.GetJsonAsync("/api/v1/admin/legal-cases?pageSize=100"))["totalCount"]!.GetValue<int>();
        after.Should().Be(before);
    }

    [Fact]
    public async Task Report_on_nonexistent_review_returns_not_found_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"/api/v1/reviews/{Guid.NewGuid()}/report", new
        {
            category = "Other",
            reporterName = "Melderin",
            reporterEmail = IntegrationTestSupport.UniqueEmail("missing-report"),
            description = "Dieser Bericht verweist auf eine nicht existierende Bewertung.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), 404);
    }

    private static async Task<HttpResponseMessage> StartTwiceAsync(HttpClient admin, Guid caseId)
    {
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/start-review", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/start-review", new { });
    }

    private static async Task<HttpResponseMessage> DecideTwiceAsync(HttpClient admin, Guid caseId)
    {
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "KeepOnline",
            rationale = "Erste Entscheidung im Test.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "KeepOnline",
            rationale = "Zweite Entscheidung im Test.",
        });
    }
}