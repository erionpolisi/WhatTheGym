using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public class ReviewFlowTests(WtgApiFactory factory)
{
    private const string GymSlug = "fitinn-favoritenstrasse";

    private static object Ratings(int? priceValue = null, int? equipment = null, int? cleanliness = null) => new
    {
        priceValue,
        equipment,
        cleanliness,
    };

    [Fact]
    public async Task Full_review_lifecycle_updates_scores_and_revisions()
    {
        var client = await factory.LoginAsync("reviewer1@example.invalid", "Rezensentin");

        // /me works after dev login.
        var me = await client.GetJsonAsync("/api/v1/me");
        me["emailVerified"]!.GetValue<bool>().Should().BeTrue();
        me["role"]!.GetValue<string>().Should().Be("User");

        // Create review with membership + studio ratings.
        var create = await client.PostAsJsonAsync($"/api/v1/gyms/{GymSlug}/reviews", new
        {
            ratings = new { priceValue = 2, equipment = 5, cleanliness = 4 },
            text = "Solides Studio, faire Konditionen.",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var review = (await create.Content.ReadFromJsonAsync<JsonNode>())!;
        var reviewId = review["id"]!.GetValue<Guid>();

        // Score summary is materialized: 50/50 across both areas.
        var summary = await client.GetJsonAsync($"/api/v1/gyms/{GymSlug}/summary");
        summary["scoreBasis"]!.GetValue<string>().Should().Be("both");
        summary["membershipScore"]!.GetValue<double>().Should().Be(2);
        summary["studioScore"]!.GetValue<double>().Should().Be(4.5);
        summary["totalScore"]!.GetValue<double>().Should().Be(3.25);
        summary["reviewCount"]!.GetValue<int>().Should().Be(1);

        // Public review list shows the verified badge and the author name.
        var list = await client.GetJsonAsync($"/api/v1/gyms/{GymSlug}/reviews");
        var item = list["items"]!.AsArray().Single()!;
        item["author"]!["verifiedViaGoogle"]!.GetValue<bool>().Should().BeTrue();
        item["author"]!["displayName"]!.GetValue<string>().Should().Be("Rezensentin");

        // A second active review for the same gym is rejected.
        var duplicate = await client.PostAsJsonAsync($"/api/v1/gyms/{GymSlug}/reviews", new
        {
            ratings = new { equipment = 1 },
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Editing archives a revision and bumps the edit count.
        var update = await client.PutAsJsonAsync($"/api/v1/reviews/{reviewId}", new
        {
            ratings = new { equipment = 3 },
            text = "Nach einem Monat: Bewertung angepasst.",
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<JsonNode>())!;
        updated["editCount"]!.GetValue<int>().Should().Be(1);

        // The summary follows the edit (membership rating removed -> studioOnly).
        var afterEdit = await client.GetJsonAsync($"/api/v1/gyms/{GymSlug}/summary");
        afterEdit["scoreBasis"]!.GetValue<string>().Should().Be("studioOnly");
        afterEdit["totalScore"]!.GetValue<double>().Should().Be(3);

        // Personal data export contains the review and its revision.
        var export = await client.GetJsonAsync("/api/v1/me/export");
        export["reviews"]!.AsArray().Should().HaveCount(1);
        export["reviewRevisions"]!.AsArray().Should().HaveCount(1);

        // Soft delete removes it from public lists and scores.
        (await client.DeleteAsync(new Uri($"/api/v1/reviews/{reviewId}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetJsonAsync($"/api/v1/gyms/{GymSlug}/summary");
        afterDelete["scoreBasis"]!.GetValue<string>().Should().Be("none");
        afterDelete["reviewCount"]!.GetValue<int>().Should().Be(0);
        afterDelete["totalScore"].Should().BeNull();

        (await client.GetJsonAsync($"/api/v1/gyms/{GymSlug}/reviews"))["items"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Unverified_accounts_cannot_review()
    {
        var client = await factory.LoginAsync("unverified@example.invalid", "Unverifiziert", emailVerified: false);

        var response = await client.PostAsJsonAsync("/api/v1/gyms/fitinn-thaliastrasse/reviews", new
        {
            ratings = new { equipment = 3 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_users_cannot_review()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/gyms/fitinn-thaliastrasse/reviews", new
        {
            ratings = new { equipment = 3 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Review_without_any_rating_is_rejected()
    {
        var client = await factory.LoginAsync("reviewer2@example.invalid", "Zweite");

        var response = await client.PostAsJsonAsync("/api/v1/gyms/fitinn-thaliastrasse/reviews", new
        {
            ratings = new { },
            text = "Nur Text ohne Bewertung.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Account_deletion_anonymizes_and_removes_reviews_from_public()
    {
        const string slug = "fitinn-bruenner-strasse";
        var client = await factory.LoginAsync("deleteme@example.invalid", "Bald Geloescht");
        (await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { staff = 4 } }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.DeleteAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Session is terminated and the account cannot act anymore.
        (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The review no longer appears publicly nor in scores.
        var anonymous = factory.CreateClient();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().BeEmpty();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/summary"))["reviewCount"]!.GetValue<int>().Should().Be(0);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_logout_revokes_it()
    {
        var client = await factory.LoginAsync("rotator@example.invalid", "Rotiererin");

        (await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(new Uri("/api/v1/auth/logout", UriKind.Relative), null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After logout the refresh token is revoked.
        (await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
