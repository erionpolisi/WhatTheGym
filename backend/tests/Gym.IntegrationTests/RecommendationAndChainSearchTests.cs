using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public class RecommendationAndChainSearchTests(WtgApiFactory factory)
{
    [Fact]
    public async Task Chain_search_groups_the_catalog_by_chain()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/chains/search");
        var items = json.AsArray();

        items.Should().NotBeEmpty();
        items.Should().Contain(c => c!["slug"]!.GetValue<string>() == "fitinn");
        items.Should().OnlyContain(c => c!["gymCount"]!.GetValue<int>() >= 1);

        // The grouped counts must cover exactly the publicly listed gyms that belong to a chain
        // (other tests in this collection may add chainless gyms to the shared database).
        var chainedGyms = 0;
        var page = 1;
        int totalPages;
        do
        {
            var flat = await client.GetJsonAsync($"/api/v1/gyms?page={page}&pageSize=100");
            totalPages = flat["totalPages"]!.GetValue<int>();
            chainedGyms += flat["items"]!.AsArray().Count(g => g!["chainSlug"] is not null);
            page++;
        }
        while (page <= totalPages);

        var totalGyms = items.Sum(c => c!["gymCount"]!.GetValue<int>());
        totalGyms.Should().Be(chainedGyms);
        chainedGyms.Should().BeGreaterThanOrEqualTo(113, "the Vienna catalog seeds 113 chained gyms");
    }

    [Fact]
    public async Task Chain_search_term_matches_chain_names_and_counts_matching_gyms()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/chains/search?term=fitinn");
        var items = json.AsArray();

        items.Should().Contain(c => c!["slug"]!.GetValue<string>() == "fitinn");
        var fitinn = items.First(c => c!["slug"]!.GetValue<string>() == "fitinn")!;
        fitinn["gymCount"]!.GetValue<int>().Should().BeGreaterThan(1);
        fitinn["trainingTypes"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Chain_search_district_filter_narrows_gym_counts()
    {
        var client = factory.CreateClient();

        var all = await client.GetJsonAsync("/api/v1/chains/search");
        var district = await client.GetJsonAsync("/api/v1/chains/search?district=1");

        district.AsArray().Count.Should().BeLessThan(all.AsArray().Count);
        district.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Chain_search_rejects_invalid_district()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/chains/search?district=42", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Women_only_chains_expose_their_target_audience()
    {
        var client = factory.CreateClient();

        var json = await client.GetJsonAsync("/api/v1/chains/search?term=sporty");

        json.AsArray().Should().Contain(c =>
            c!["name"]!.GetValue<string>().Contains("Sporty")
            && c["targetAudience"]!.GetValue<string>() == "WomenOnly");
    }

    [Fact]
    public async Task Recommendation_returns_top_matches_with_reasons()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/gyms/recommendations", new
        {
            gender = "male",
            districts = new[] { 10, 11 },
            trainingTypes = new[] { "Strength", "Cardio" },
            scoreImportance = "medium",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = (await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>())!;
        var items = json.AsArray();

        items.Count.Should().BeInRange(1, 3);
        foreach (var item in items)
        {
            item!["gym"]!["district"]!.GetValue<int>().Should().BeOneOf(10, 11);
            item["gym"]!["targetAudience"]!.GetValue<string>().Should().Be("General");
            item["matchPercent"]!.GetValue<int>().Should().BeInRange(0, 100);
            item["reasons"]!.AsArray().Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Recommendation_honors_women_only_requirement()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/gyms/recommendations", new
        {
            gender = "female",
            womenOnly = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = (await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>())!;
        var items = json.AsArray();

        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i!["gym"]!["targetAudience"]!.GetValue<string>() == "WomenOnly");
    }

    [Fact]
    public async Task Recommendation_rejects_invalid_preferences_with_problem_details()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/gyms/recommendations", new
        {
            districts = new[] { 42 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
