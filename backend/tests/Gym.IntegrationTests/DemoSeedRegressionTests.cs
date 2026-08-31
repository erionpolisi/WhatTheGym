using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Gym.IntegrationTests;

/// <summary>
/// Boots the API exactly like local development: catalogue AND demo data seeded.
/// Regression coverage for defects that only exist with development seed data present.
/// </summary>
public sealed class DemoSeedApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("whatthegym_demoseed_test")
        .WithUsername("wtg_test")
        .WithPassword("wtg_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:SeedCatalog", "true");
        builder.UseSetting("Seed:SeedDemoData", "true");
        builder.UseSetting("Auth:EnableDevLogin", "true");
        builder.UseSetting("Auth:BootstrapAdminEmail", "admin@example.invalid");
        builder.UseSetting("RateLimits:PublicWritePerMinute", "1000");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("RateLimits:AnalyticsPerMinute", "1000");
    }
}

public sealed class DemoSeedRegressionTests(DemoSeedApiFactory factory) : IClassFixture<DemoSeedApiFactory>
{
    /// <summary>
    /// Regression: the demo seeder created case WTG-<year>-000001 WITHOUT consuming the
    /// legal_case_seq sequence, so the first real report on a development database drew
    /// duplicate number 1 and failed with a unique-index violation (HTTP 500).
    /// </summary>
    [Fact]
    public async Task Report_on_demo_seeded_database_creates_case_with_unique_number()
    {
        var client = factory.CreateClient();

        // Find a demo-seeded published review.
        var gyms = (await client.GetFromJsonAsync<JsonNode>("/api/v1/gyms?sort=name&pageSize=50"))!;
        var reviewedGym = gyms["items"]!.AsArray()
            .First(g => g!["reviewCount"]!.GetValue<int>() > 0);
        var reviews = (await client.GetFromJsonAsync<JsonNode>(
            $"/api/v1/gyms/{reviewedGym!["slug"]!.GetValue<string>()}/reviews"))!;
        var reviewId = reviews["items"]!.AsArray().First()!["id"]!.GetValue<string>();

        var response = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Defamation",
            reporterName = "Regression Reporter",
            reporterEmail = "regression@example.invalid",
            description = "Regressionstest: erste echte Meldung auf einer Entwicklungs-Datenbank mit Demo-Daten.",
            website = string.Empty,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        var caseNumber = body["caseNumber"]!.GetValue<string>();
        Assert.NotEqual("WTG-2026-000001", caseNumber);

        // A second report by another reporter must also get a fresh number.
        var second = await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Other",
            reporterName = "Zweiter Melder",
            reporterEmail = "zweiter@example.invalid",
            description = "Zweite Meldung im Regressionstest, muss eine neue Fallnummer erhalten.",
            website = string.Empty,
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondNumber = (await second.Content.ReadFromJsonAsync<JsonNode>())!["caseNumber"]!.GetValue<string>();
        Assert.NotEqual(caseNumber, secondNumber);
    }
}
