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
public class CatalogEndpointsTests(WtgApiFactory factory)
{
    [Fact]
    public async Task Health_endpoints_are_available()
    {
        var client = factory.CreateClient();

        (await client.GetAsync(new Uri("/health/live", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task No_pending_migrations_after_startup()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Seeded_catalog_contains_about_fifty_vienna_gyms()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/gyms?pageSize=100");

        json["totalCount"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(45);
        json["items"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task District_filter_returns_only_that_district()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/gyms?district=22&pageSize=100");

        var items = json["items"]!.AsArray();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i!["district"]!.GetValue<int>() == 22);
    }

    [Fact]
    public async Task Fulltext_and_trigram_search_finds_fitinn()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/gyms?term=fitinn&pageSize=100");

        json["items"]!.AsArray()
            .Should().Contain(i => i!["name"]!.GetValue<string>().Contains("FitInn"));
    }

    [Fact]
    public async Task Chain_filter_works_by_slug()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/gyms?chain=mcfit&pageSize=100");

        var items = json["items"]!.AsArray();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i!["chainSlug"]!.GetValue<string>() == "mcfit");
    }

    [Fact]
    public async Task Gym_detail_returns_summary_with_basis_none_without_reviews()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/gyms/kieser-training-wien-innere-stadt");

        json["slug"]!.GetValue<string>().Should().Be("kieser-training-wien-innere-stadt");
        json["district"]!.GetValue<int>().Should().Be(1);
        json["score"]!["scoreBasis"]!.GetValue<string>().Should().Be("none");
        json["score"]!["totalScore"].Should().BeNull();
        json["score"]!["categories"]!.AsArray().Should().HaveCount(11);
        json["amenities"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Gym_detail_supports_etags()
    {
        var client = factory.CreateClient();
        var first = await client.GetAsync(new Uri("/api/v1/gyms/kieser-training-wien-alsergrund", UriKind.Relative));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = first.Headers.ETag!.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/gyms/kieser-training-wien-alsergrund", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await client.SendAsync(request);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Unknown_gym_returns_problem_details_404()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/gyms/does-not-exist", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Chains_and_amenities_are_public()
    {
        var client = factory.CreateClient();

        var chains = await client.GetJsonAsync("/api/v1/chains");
        var amenities = await client.GetJsonAsync("/api/v1/amenities");

        chains.AsArray().Should().Contain(c => c!["slug"]!.GetValue<string>() == "fitinn");
        amenities.AsArray().Should().Contain(a => a!["slug"]!.GetValue<string>() == "sauna");
    }

    [Fact]
    public async Task Admin_endpoints_require_authentication_and_role()
    {
        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/admin/chains", new { name = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await factory.LoginAsync("user-noadmin@example.invalid", "Normalo");
        (await user.PostAsJsonAsync("/api/v1/admin/chains", new { name = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_create_gym_with_stable_unique_slug()
    {
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        var create = await admin.PostAsJsonAsync("/api/v1/admin/gyms", new
        {
            name = "Teststudio Favoriten",
            district = 10,
            addressLine = "Teststrasse 1",
            postalCode = "1100",
            status = "Active",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        // Same name again -> suffix keeps the slug unique.
        var duplicate = await admin.PostAsJsonAsync("/api/v1/admin/gyms", new
        {
            name = "Teststudio Favoriten",
            district = 10,
            addressLine = "Teststrasse 2",
            postalCode = "1100",
            status = "Draft",
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await admin.GetAsync(new Uri("/api/v1/gyms/teststudio-favoriten-2", UriKind.Relative));
        // Draft gyms are hidden from the public detail endpoint.
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var publicOne = await admin.GetJsonAsync("/api/v1/gyms/teststudio-favoriten");
        publicOne["status"]!.GetValue<string>().Should().Be("Active");
    }
}
