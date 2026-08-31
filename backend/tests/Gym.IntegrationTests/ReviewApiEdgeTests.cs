using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public sealed class ReviewApiEdgeTests(WtgApiFactory factory)
{
    public static TheoryData<string, string> RatingCategories => new()
    {
        { "priceValue", "membershipOnly" },
        { "contractTerms", "membershipOnly" },
        { "billing", "membershipOnly" },
        { "cancellationExperience", "membershipOnly" },
        { "equipment", "studioOnly" },
        { "cleanliness", "studioOnly" },
        { "staff", "studioOnly" },
        { "crowding", "studioOnly" },
        { "changingRoom", "studioOnly" },
        { "showers", "studioOnly" },
        { "atmosphere", "studioOnly" },
    };

    [Theory]
    [MemberData(nameof(RatingCategories))]
    public async Task Creating_review_with_single_category_updates_summary_category(string category, string expectedBasis)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Category Review", district: 6);
        var ratings = new Dictionary<string, int?> { [category] = 4 };

        await IntegrationTestSupport.CreateReviewAsync(factory, slug, ratings, "category-review");

        var summary = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/summary");
        summary["reviewCount"]!.GetValue<int>().Should().Be(1);
        summary["scoreBasis"]!.GetValue<string>().Should().Be(expectedBasis);
        summary["totalScore"]!.GetValue<double>().Should().Be(4);
        var categoryScore = summary["categories"]!.AsArray().Single(c => c!["category"]!.GetValue<string>() == category)!;
        categoryScore["average"]!.GetValue<double>().Should().Be(4);
        categoryScore["ratingCount"]!.GetValue<int>().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(RatingCategories))]
    public async Task Editing_review_replaces_category_and_preserves_single_active_review(string category, string expectedBasis)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Edit Category", district: 7);
        var client = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("edit-category"), "Editorin");
        var create = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { equipment = 2 } });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var reviewId = (await IntegrationTestSupport.ReadJsonAsync(create))["id"]!.GetValue<Guid>();

        var update = await client.PutAsJsonAsync($"/api/v1/reviews/{reviewId}", new
        {
            ratings = new Dictionary<string, int?> { [category] = 5 },
            text = "Aktualisierte Bewertung fuer eine einzelne Kategorie.",
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IntegrationTestSupport.ReadJsonAsync(update))["editCount"]!.GetValue<int>().Should().Be(1);
        var summary = await client.GetJsonAsync($"/api/v1/gyms/{slug}/summary");
        summary["scoreBasis"]!.GetValue<string>().Should().Be(expectedBasis);
        var categoryScore = summary["categories"]!.AsArray().Single(c => c!["category"]!.GetValue<string>() == category)!;
        categoryScore["average"]!.GetValue<double>().Should().Be(5);
    }

    public static TheoryData<string, int> InvalidRatingValues => new()
    {
        { "priceValue", 0 }, { "priceValue", 6 },
        { "contractTerms", 0 }, { "contractTerms", 6 },
        { "billing", 0 }, { "billing", 6 },
        { "cancellationExperience", 0 }, { "cancellationExperience", 6 },
        { "equipment", 0 }, { "equipment", 6 },
        { "cleanliness", 0 }, { "cleanliness", 6 },
        { "staff", 0 }, { "staff", 6 },
        { "crowding", 0 }, { "crowding", 6 },
        { "changingRoom", 0 }, { "changingRoom", 6 },
        { "showers", 0 }, { "showers", 6 },
        { "atmosphere", 0 }, { "atmosphere", 6 },
    };

    [Theory]
    [MemberData(nameof(InvalidRatingValues))]
    public async Task Out_of_range_ratings_return_validation_problem(string category, int value)
    {
        var client = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("invalid-rating"), "Invalid");

        var response = await client.PostAsJsonAsync("/api/v1/gyms/fitinn-favoritenstrasse/reviews", new
        {
            ratings = new Dictionary<string, int?> { [category] = value },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await IntegrationTestSupport.ReadJsonAsync(response);
        IntegrationTestSupport.AssertProblem(problem, 400);
        problem["detail"]!.GetValue<string>().Should().Contain("zwischen 1 und 5");
    }

    [Theory]
    [InlineData("empty-ratings")]
    [InlineData("too-many-links")]
    [InlineData("too-long-text")]
    [InlineData("missing-gym")]
    [InlineData("unauthenticated")]
    public async Task Review_create_rejects_invalid_or_unauthorized_requests(string scenario)
    {
        var client = scenario == "unauthenticated"
            ? factory.CreateClient()
            : await factory.LoginAsync(IntegrationTestSupport.UniqueEmail($"review-{scenario}"), "Review Invalid");

        (string Path, object Payload, HttpStatusCode Expected) request = scenario switch
        {
            "empty-ratings" => ("/api/v1/gyms/fitinn-favoritenstrasse/reviews", new { ratings = new { }, text = "Nur Text ohne Bewertung." }, HttpStatusCode.BadRequest),
            "too-many-links" => ("/api/v1/gyms/fitinn-favoritenstrasse/reviews", new { ratings = new { equipment = 3 }, text = "http://a.test http://b.test http://c.test http://d.test" }, HttpStatusCode.BadRequest),
            "too-long-text" => ("/api/v1/gyms/fitinn-favoritenstrasse/reviews", new { ratings = new { equipment = 3 }, text = new string('a', 4001) }, HttpStatusCode.BadRequest),
            "missing-gym" => ("/api/v1/gyms/does-not-exist/reviews", new { ratings = new { equipment = 3 }, text = "Gueltige Bewertung fuer fehlendes Studio." }, HttpStatusCode.NotFound),
            _ => ("/api/v1/gyms/fitinn-favoritenstrasse/reviews", new { ratings = new { equipment = 3 }, text = "Nicht angemeldet." }, HttpStatusCode.Unauthorized),
        };

        var response = await client.PostAsJsonAsync(request.Path, request.Payload);

        response.StatusCode.Should().Be(request.Expected);
        if (request.Expected is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), (int)request.Expected);
        }
    }

    [Fact]
    public async Task Duplicate_review_for_same_user_and_gym_returns_conflict()
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Duplicate Review", district: 3);
        var client = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("duplicate-review"), "Doppelt");
        (await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { equipment = 4 } }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { staff = 3 } });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(duplicate), 409);
    }

    [Theory]
    [InlineData("Draft", HttpStatusCode.NotFound)]
    [InlineData("PermanentlyClosed", HttpStatusCode.Conflict)]
    public async Task Reviews_are_rejected_for_draft_or_closed_gyms(string status, HttpStatusCode expectedStatus)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Closed Review", district: 4, status: status);
        var client = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("closed-review"), "Closed");

        var response = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { equipment = 3 } });

        response.StatusCode.Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 2, 0)]
    public async Task Review_list_pagination_is_stable_for_dedicated_gym(int page, int pageSize, int expectedCount)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Review Page", district: 5);
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 5 }, "review-page-a");
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 4 }, "review-page-b");
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 3 }, "review-page-c");

        var list = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews?page={page}&pageSize={pageSize}");

        list["page"]!.GetValue<int>().Should().Be(page);
        list["pageSize"]!.GetValue<int>().Should().Be(pageSize);
        list["items"]!.AsArray().Should().HaveCount(expectedCount);
        list["totalCount"]!.GetValue<int>().Should().Be(3);
    }

    // Layering decision: XSS defense is OUTPUT ENCODING, not input mutilation. The API returns
    // JSON (never interprets text as HTML) and the Next.js frontend escapes all text on render.
    // Stripping markup server-side would corrupt legitimate reviews ("Preis < 20 Euro"). The
    // sanitizer intentionally only trims, normalizes newlines and drops control characters.
    [Fact]
    public async Task Review_text_returns_markup_verbatim_because_xss_defense_is_output_encoding()
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Xss Review", district: 2);
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 5 }, "xss-review", "<script>alert(1)</script> gutes Studio");

        var list = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews");

        list["items"]!.AsArray().Single()!["text"]!.GetValue<string>().Should().Contain("<script>alert(1)</script>");
    }
}