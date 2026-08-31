using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public sealed class SearchFilterMatrixTests(WtgApiFactory factory)
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    public async Task District_filter_returns_only_requested_district(int district)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "District Filter", district);
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?district={district}&pageSize=100");

        result["page"]!.GetValue<int>().Should().Be(1);
        result["items"]!.AsArray().Should().Contain(i => i!["slug"]!.GetValue<string>() == slug);
        result["items"]!.AsArray().Should().OnlyContain(i => i!["district"]!.GetValue<int>() == district);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(99)]
    public async Task Invalid_district_returns_problem_details(int district)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/api/v1/gyms?district={district}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), 400);
    }

    [Theory]
    [InlineData("fitinn")]
    [InlineData("mcfit")]
    [InlineData("clever-fit")]
    [InlineData("club-danube")]
    [InlineData("kieser-training")]
    public async Task Chain_filter_returns_only_requested_chain(string chainSlug)
    {
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?chain={chainSlug}&pageSize=100");

        result["items"]!.AsArray().Should().NotBeEmpty();
        result["items"]!.AsArray().Should().OnlyContain(i => i!["chainSlug"]!.GetValue<string>() == chainSlug);
    }

    [Theory]
    [InlineData("score")]
    [InlineData("name")]
    [InlineData("newest")]
    public async Task Sort_options_return_stable_order(string sort)
    {
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?sort={sort}&pageSize=20");
        var items = result["items"]!.AsArray();

        items.Should().NotBeEmpty();
        if (sort == "name")
        {
            items.Select(i => i!["name"]!.GetValue<string>()).Should().BeInAscendingOrder();
        }
        else if (sort == "score")
        {
            items.Select(i => i!["totalScore"]?.GetValue<double>()).Should().BeInDescendingOrder();
        }
        else
        {
            items.Select(i => i!["slug"]!.GetValue<string>()).Should().OnlyHaveUniqueItems();
        }
    }

    [Theory]
    [InlineData(0, 10, 1, 10)]
    [InlineData(-1, 10, 1, 10)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, -5, 1, 1)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(2, 7, 2, 7)]
    [InlineData(1, 100, 1, 100)]
    [InlineData(1, 500, 1, 100)]
    public async Task Pagination_inputs_are_normalized(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?page={page}&pageSize={pageSize}");

        result["page"]!.GetValue<int>().Should().Be(expectedPage);
        result["pageSize"]!.GetValue<int>().Should().Be(expectedPageSize);
        result["items"]!.AsArray().Should().HaveCountLessThanOrEqualTo(expectedPageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(25)]
    public async Task Total_pages_are_consistent_with_total_count(int pageSize)
    {
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?pageSize={pageSize}");

        var totalCount = result["totalCount"]!.GetValue<int>();
        result["totalPages"]!.GetValue<int>().Should().Be((int)Math.Ceiling(totalCount / (double)pageSize));
    }

    [Theory]
    [InlineData("fitinn", "FitInn")]
    [InlineData("FitInn", "FitInn")]
    [InlineData("fitin", "FitInn")]
    [InlineData("favoriten", "Favoriten")]
    [InlineData("Favoritenstrasse", "Favoriten")]
    [InlineData("donaustadt", "Donaustadt")]
    [InlineData("bruenner", "Bruenner")]
    [InlineData("mcfit", "McFIT")]
    [InlineData("kieser", "Kieser")]
    [InlineData("clever", "Clever")]
    [InlineData("club", "Club")]
    [InlineData("", "")]
    public async Task Term_search_handles_exact_prefix_typo_and_empty_terms(string term, string expectedFragment)
    {
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?term={Uri.EscapeDataString(term)}&pageSize=100");

        var items = result["items"]!.AsArray();
        items.Should().NotBeEmpty();
        if (expectedFragment.Length > 0)
        {
            items.Should().Contain(i => i!["name"]!.GetValue<string>().Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData(5, 1, true)]
    [InlineData(5, 5, true)]
    [InlineData(5, 5.1, false)]
    [InlineData(3, 2.9, true)]
    [InlineData(3, 3.1, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 1.1, false)]
    [InlineData(4, 4, true)]
    [InlineData(4, 4.5, false)]
    public async Task Minimum_total_score_filter_includes_only_matching_scored_gyms(int score, double minimum, bool shouldContain)
    {
        var (gymId, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Score Filter", district: 9);
        _ = gymId;
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { priceValue = score, equipment = score }, "score-filter");
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?district=9&minTotalScore={minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}&pageSize=100");

        result["items"]!.AsArray().Any(i => i!["slug"]!.GetValue<string>() == slug).Should().Be(shouldContain);
        var scoredItems = result["items"]!.AsArray().Where(i => i!["totalScore"] is not null).ToList();
        if (scoredItems.Count > 0)
        {
            scoredItems.Should().OnlyContain(i => i!["totalScore"]!.GetValue<double>() >= minimum);
        }
    }

    [Theory]
    [InlineData("minMembershipScore", 5, 1, true)]
    [InlineData("minMembershipScore", 5, 5, true)]
    [InlineData("minMembershipScore", 5, 5.1, false)]
    [InlineData("minStudioScore", 4, 1, true)]
    [InlineData("minStudioScore", 4, 4, true)]
    [InlineData("minStudioScore", 4, 4.1, false)]
    public async Task Minimum_area_score_filters_include_only_matching_scored_gyms(string filter, int score, double minimum, bool shouldContain)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Area Score Filter", district: 8);
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { priceValue = score, equipment = score }, "area-score-filter");
        var client = factory.CreateClient();

        var result = await client.GetJsonAsync($"/api/v1/gyms?district=8&{filter}={minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}&pageSize=100");

        result["items"]!.AsArray().Any(i => i!["slug"]!.GetValue<string>() == slug).Should().Be(shouldContain);
    }
}