using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Gym.IntegrationTests;

public sealed class LowRateLimitApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("whatthegym_ratelimit_test")
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
        builder.UseSetting("Seed:SeedDemoData", "false");
        builder.UseSetting("Auth:EnableDevLogin", "true");
        builder.UseSetting("Auth:BootstrapAdminEmail", "admin@example.invalid");
        builder.UseSetting("RateLimits:PublicWritePerMinute", "3");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("RateLimits:AnalyticsPerMinute", "3");
    }
}

public sealed class RateLimitTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Public_write_rate_limit_returns_429_with_retry_after()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var accepted = await client.PostAsJsonAsync("/api/v1/contact-requests", new
            {
                type = "General",
                name = $"Rate Limit {i}",
                email = $"rate-limit-{i}@example.invalid",
                message = "Diese gueltige Nachricht zaehlt gegen das niedrige Limit.",
            });
            accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/contact-requests", new
        {
            type = "General",
            name = "Rate Limit 4",
            email = "rate-limit-4@example.invalid",
            message = "Diese gueltige Nachricht sollte das niedrige Limit ueberschreiten.",
        });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_remain_available_with_low_rate_limits(string path)
    {
        var response = await factory.CreateClient().GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}