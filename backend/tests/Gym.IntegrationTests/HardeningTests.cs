using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public sealed class HardeningTests(WtgApiFactory factory)
{
    [Theory]
    [InlineData("/api/v1/gyms/does-not-exist", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/gyms?district=0", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/legal/documents/not-a-document", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/legal/cases/WTG-2026-999999/status?token=wrong", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/gyms/does-not-exist/summary", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/gyms/does-not-exist/reviews", HttpStatusCode.NotFound)]
    public async Task Error_responses_use_problem_details_contract(string path, HttpStatusCode expectedStatus)
    {
        var response = await factory.CreateClient().GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), (int)expectedStatus);
    }

    [Theory]
    [InlineData("/api/v1/gyms/kieser-training-wien-alsergrund")]
    [InlineData("/api/v1/legal/documents/imprint")]
    [InlineData("/api/v1/legal/documents/privacyPolicy")]
    [InlineData("/api/v1/legal/documents/termsOfUse")]
    public async Task Cacheable_public_details_support_etags(string path)
    {
        var client = factory.CreateClient();
        var first = await client.GetAsync(new Uri(path, UriKind.Relative));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = first.Headers.ETag!.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await client.SendAsync(request);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData("page_view")]
    [InlineData("search_performed")]
    [InlineData("gym_detail_view")]
    [InlineData("review_created")]
    [InlineData("report_submitted")]
    [InlineData("contact_submitted")]
    public async Task Analytics_accepts_every_allowlisted_event_type(string eventType)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/analytics/events", new
        {
            eventType,
            path = "/studios/fitinn-favoritenstrasse?secret=must-be-stripped",
            sessionId = IntegrationTestSupport.UniqueEmail("analytics-session"),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Theory]
    [InlineData("unknown_event", "session-1")]
    [InlineData("", "session-2")]
    [InlineData("page_view", "")]
    [InlineData("page_view", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Analytics_rejects_unknown_events_or_invalid_sessions(string eventType, string sessionId)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/analytics/events", new
        {
            eventType,
            path = "/",
            sessionId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), 400);
    }

    [Theory]
    [InlineData("General")]
    [InlineData("GymSuggestion")]
    [InlineData("DataCorrection")]
    public async Task Contact_request_types_are_created_and_visible_to_admin(string type)
    {
        var email = IntegrationTestSupport.UniqueEmail("contact");
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/contact-requests", new
        {
            type,
            name = "Kontakt Person",
            email,
            message = "Diese Nachricht ist lang genug fuer die Validierung.",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var queue = await admin.GetJsonAsync("/api/v1/admin/contact-requests?pageSize=100");
        queue["items"]!.AsArray().Should().Contain(i => i!["email"]!.GetValue<string>() == email && i!["type"]!.GetValue<string>() == type);
    }

    [Theory]
    [InlineData("bad-email")]
    [InlineData("short-message")]
    [InlineData("empty-name")]
    [InlineData("unknown-type")]
    [InlineData("too-many-links")]
    [InlineData("missing-gym")]
    [InlineData("too-long-message")]
    [InlineData("too-long-email")]
    public async Task Contact_request_validation_returns_bad_request_or_not_found(string scenario)
    {
        object payload = scenario switch
        {
            "bad-email" => new { type = "General", name = "Kontakt", email = "keine-mail", message = "Eine ausreichend lange Nachricht." },
            "short-message" => new { type = "General", name = "Kontakt", email = IntegrationTestSupport.UniqueEmail("contact-bad"), message = "kurz" },
            "empty-name" => new { type = "General", name = "", email = IntegrationTestSupport.UniqueEmail("contact-bad"), message = "Eine ausreichend lange Nachricht." },
            "unknown-type" => new { type = "Unknown", name = "Kontakt", email = IntegrationTestSupport.UniqueEmail("contact-bad"), message = "Eine ausreichend lange Nachricht." },
            "too-many-links" => new { type = "General", name = "Kontakt", email = IntegrationTestSupport.UniqueEmail("contact-bad"), message = "http://a.test http://b.test http://c.test http://d.test" },
            "missing-gym" => new { type = "DataCorrection", name = "Kontakt", email = IntegrationTestSupport.UniqueEmail("contact-bad"), message = "Eine ausreichend lange Nachricht.", gymSlug = "does-not-exist" },
            "too-long-message" => new { type = "General", name = "Kontakt", email = IntegrationTestSupport.UniqueEmail("contact-bad"), message = new string('a', 4001) },
            _ => new { type = "General", name = "Kontakt", email = $"{new string('a', 250)}@example.invalid", message = "Eine ausreichend lange Nachricht." },
        };
        var expected = scenario == "missing-gym" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest;

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/contact-requests", payload);

        response.StatusCode.Should().Be(expected);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), (int)expected);
    }

    [Fact]
    public async Task Contact_honeypot_is_accepted_and_not_added_to_admin_queue()
    {
        var email = IntegrationTestSupport.UniqueEmail("contact-honeypot");
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/contact-requests", new
        {
            type = "General",
            name = "Bot",
            email,
            message = "Diese Nachricht sieht echt aus, enthaelt aber Honeypot.",
            website = "https://bot.example",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var queue = await admin.GetJsonAsync("/api/v1/admin/contact-requests?pageSize=100");
        queue["items"]!.AsArray().Should().NotContain(i => i!["email"]!.GetValue<string>() == email);
    }

    [Theory]
    [InlineData("imprint")]
    [InlineData("privacyPolicy")]
    [InlineData("termsOfUse")]
    public async Task Legal_documents_are_public_and_version_lists_are_available(string type)
    {
        var client = factory.CreateClient();

        var document = await client.GetJsonAsync($"/api/v1/legal/documents/{type}");
        var versions = await client.GetJsonAsync($"/api/v1/legal/documents/{type}/versions");

        document["contentMarkdown"]!.GetValue<string>().Should().Contain("ENTWURF - anwaltlich pruefen lassen");
        document["isPublished"]!.GetValue<bool>().Should().BeTrue();
        versions.AsArray().Should().Contain(v => v!["version"]!.GetValue<int>() == document["version"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("not-a-type")]
    [InlineData("privacy")]
    public async Task Unknown_legal_document_types_return_bad_request(string type)
    {
        var response = await factory.CreateClient().GetAsync(new Uri($"/api/v1/legal/documents/{type}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), 400);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Review")]
    [InlineData("LegalCase")]
    [InlineData("ContactRequest")]
    [InlineData("AnalyticsEvent")]
    public async Task Processing_activities_include_required_personal_data_entities(string entityName)
    {
        var record = await factory.CreateClient().GetJsonAsync("/api/v1/legal/processing-activities");

        record["activities"]!.AsArray()
            .SelectMany(a => a!["entities"]!.AsArray().Select(e => e!.GetValue<string>()))
            .Should().Contain(entityName);
        record["notice"]!.GetValue<string>().Should().Contain("ENTWURF");
    }

    [Theory]
    [InlineData("", 1, "Adresse", "1010")]
    [InlineData("Name", 0, "Adresse", "1010")]
    [InlineData("Name", 24, "Adresse", "1010")]
    [InlineData("Name", 1, "", "1010")]
    [InlineData("Name", 1, "Adresse", "9999")]
    [InlineData("Name", 1, "Adresse", "abc")]
    public async Task Admin_gym_validation_returns_problem_details(string name, int district, string addressLine, string postalCode)
    {
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/gyms", new
        {
            name,
            district,
            addressLine,
            postalCode,
            status = "Active",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), 400);
    }

    [Fact]
    public async Task Legal_case_events_are_append_only_at_database_level()
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Append Only", district: 17);
        var reviewId = await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 2 }, "append-only");
        var report = await factory.CreateClient().PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Other",
            reporterName = "Append Reporter",
            reporterEmail = IntegrationTestSupport.UniqueEmail("append-reporter"),
            description = "Meldung fuer den Append-only Trigger Test mit ausreichender Begruendung.",
        });
        report.StatusCode.Should().Be(HttpStatusCode.Created);
        var caseNumber = (await IntegrationTestSupport.ReadJsonAsync(report))["caseNumber"]!.GetValue<string>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseId = await db.LegalCases
            .Where(c => c.CaseNumber == caseNumber)
            .Select(c => c.Id)
            .SingleAsync();
        var eventId = await db.LegalCaseEvents
            .Where(e => e.LegalCaseId == caseId)
            .Select(e => e.Id)
            .FirstAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync("""UPDATE "LegalCaseEvents" SET "DataJson" = '{{}}'::jsonb WHERE "Id" = {0}""", eventId));
        exception.MessageText.Should().Contain("append-only");
    }
}