using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public class FormsAndPrivacyTests(WtgApiFactory factory)
{
    [Fact]
    public async Task Contact_request_honeypot_is_silently_dropped()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/contact-requests", new
        {
            type = "General",
            name = "Bot",
            email = "bot@example.invalid",
            message = "Spam Nachricht mit Honeypot-Fuellung.",
            website = "http://spam.example",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var list = await admin.GetJsonAsync("/api/v1/admin/contact-requests?pageSize=100");
        list["items"]!.AsArray().Should().NotContain(i => i!["email"]!.GetValue<string>() == "bot@example.invalid");
    }

    [Fact]
    public async Task Contact_request_is_created_and_manageable_by_admin()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/contact-requests", new
        {
            type = "GymSuggestion",
            name = "Vorschlagende",
            email = "vorschlag@example.invalid",
            message = "Bitte nehmt das neue Studio in der Donaustadt auf.",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var list = await admin.GetJsonAsync("/api/v1/admin/contact-requests?pageSize=100");
        var item = list["items"]!.AsArray().Single(i => i!["email"]!.GetValue<string>() == "vorschlag@example.invalid")!;
        item["type"]!.GetValue<string>().Should().Be("GymSuggestion");

        var requestId = item["id"]!.GetValue<Guid>();
        (await admin.PutAsJsonAsync($"/api/v1/admin/contact-requests/{requestId}/status", new { status = "Resolved" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Analytics_accepts_allowlisted_events_only()
    {
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/analytics/events", new
        {
            eventType = "page_view",
            path = "/studios/fitinn-wien-favoritenstrasse?utm_source=x",
            sessionId = "client-session-abc",
        })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        (await client.PostAsJsonAsync("/api/v1/analytics/events", new
        {
            eventType = "totally_custom_event",
            path = "/",
            sessionId = "client-session-abc",
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Legal_documents_are_versioned_and_marked_as_draft()
    {
        var client = factory.CreateClient();

        var imprint = await client.GetJsonAsync("/api/v1/legal/documents/imprint");
        imprint["contentMarkdown"]!.GetValue<string>().Should().Contain("ENTWURF - anwaltlich pruefen lassen");
        imprint["version"]!.GetValue<int>().Should().Be(1);

        var versions = await client.GetJsonAsync("/api/v1/legal/documents/privacyPolicy/versions");
        versions.AsArray().Should().NotBeEmpty();

        // Admin publishes a new version which becomes the active one.
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var created = await admin.PostAsJsonAsync("/api/v1/admin/legal-documents", new
        {
            type = "Imprint",
            title = "Impressum",
            contentMarkdown = "# Impressum v2\n\nENTWURF - anwaltlich pruefen lassen",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = (await created.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<Guid>();

        // Draft is not active yet.
        (await client.GetJsonAsync("/api/v1/legal/documents/imprint"))["version"]!.GetValue<int>().Should().Be(1);

        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-documents/{documentId}/publish", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetJsonAsync("/api/v1/legal/documents/imprint"))["version"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task Processing_activities_record_is_public_and_versioned()
    {
        var client = factory.CreateClient();

        var record = await client.GetJsonAsync("/api/v1/legal/processing-activities");

        record["version"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        record["activities"]!.AsArray().Should().NotBeEmpty();
        record["notice"]!.GetValue<string>().Should().Contain("ENTWURF");
    }

    [Fact]
    public async Task Rate_limit_returns_429_when_exceeded()
    {
        // Isolated host with a very low analytics limit; shares the database container.
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimits:AnalyticsPerMinute", "2");
            builder.UseSetting("Database:MigrateOnStartup", "false");
        });
        var client = limitedFactory.CreateClient();

        async Task<HttpStatusCode> FireAsync() =>
            (await client.PostAsJsonAsync("/api/v1/analytics/events", new
            {
                eventType = "page_view",
                path = "/",
                sessionId = "ratelimit-test",
            })).StatusCode;

        (await FireAsync()).Should().Be(HttpStatusCode.Accepted);
        (await FireAsync()).Should().Be(HttpStatusCode.Accepted);
        (await FireAsync()).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Google_login_returns_503_when_not_configured()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/auth/google/start", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Swagger_document_is_exposed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("WhatTheGym API");
    }
}
