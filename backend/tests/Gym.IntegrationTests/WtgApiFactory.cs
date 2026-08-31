using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Gym.IntegrationTests;

/// <summary>
/// Boots the real API against a disposable PostgreSQL container. Startup applies the EF Core
/// migrations and the deterministic Vienna catalogue seed (demo data stays off for test isolation).
/// </summary>
public sealed class WtgApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("whatthegym_test")
        .WithUsername("wtg_test")
        .WithPassword("wtg_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Force host creation (runs migrations + seed) so the container connection is validated early.
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
        builder.UseSetting("Seed:SeedDemoData", "false");
        builder.UseSetting("Auth:EnableDevLogin", "true");
        builder.UseSetting("Auth:BootstrapAdminEmail", "admin@example.invalid");
        builder.UseSetting("RateLimits:PublicWritePerMinute", "1000");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("RateLimits:AnalyticsPerMinute", "1000");
    }

    // Session-authenticated writes require the CSRF header; set it as a client default.
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(Gym.Api.Middleware.CsrfHeaderMiddleware.HeaderName, "1");
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<WtgApiFactory>;

public static class TestClientExtensions
{
    /// <summary>Signs in through the development login and returns the authenticated client.</summary>
    public static async Task<HttpClient> LoginAsync(this WtgApiFactory factory, string email, string displayName, bool emailVerified = true)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/dev-login", new
        {
            email,
            displayName,
            emailVerified,
        });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<JsonNode> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }
}
