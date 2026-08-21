using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public class ModerationTests(WtgApiFactory factory)
{
    private const string GymSlug = "club-danube-erdberg";

    [Fact]
    public async Task Moderator_removes_review_reversibly_and_admin_restores_it()
    {
        // Author creates a review.
        var author = await factory.LoginAsync("mod-target@example.invalid", "Zielperson");
        var create = await author.PostAsJsonAsync($"/api/v1/gyms/{GymSlug}/reviews", new
        {
            ratings = new { crowding = 1, atmosphere = 2 },
            text = "Grenzwertige Formulierung, die gemeldet wird.",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var reviewId = (await create.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<Guid>();

        // Set up a moderator.
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        _ = await factory.LoginAsync("mod2@example.invalid", "Zweite Moderatorin");
        var users = await admin.GetJsonAsync("/api/v1/admin/users?pageSize=100");
        var modId = users["items"]!.AsArray()
            .Single(u => u!["email"]!.GetValue<string>() == "mod2@example.invalid")!["id"]!.GetValue<Guid>();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{modId}/role", new { role = "Moderator" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var moderator = await factory.LoginAsync("mod2@example.invalid", "Zweite Moderatorin");

        // Removal requires a reason.
        (await moderator.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/remove", new { reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await moderator.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/remove", new
        {
            reason = "Verstoss gegen die Richtlinien (beleidigender Ton).",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Gone from public view and scores.
        var anonymous = factory.CreateClient();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{GymSlug}/reviews"))["items"]!.AsArray().Should().BeEmpty();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{GymSlug}/summary"))["reviewCount"]!.GetValue<int>().Should().Be(0);

        // Visible in the moderation queue with origin and reason.
        var queue = await moderator.GetJsonAsync("/api/v1/moderation/reviews?status=SoftDeleted&pageSize=100");
        var entry = queue["items"]!.AsArray().Single(i => i!["id"]!.GetValue<Guid>() == reviewId)!;
        entry["deletionOrigin"]!.GetValue<string>().Should().Be("Moderator");
        entry["deletionReason"]!.GetValue<string>().Should().Contain("Richtlinien");

        // Moderators cannot restore; admins can.
        (await moderator.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/restore", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/restore", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.GetJsonAsync($"/api/v1/gyms/{GymSlug}/reviews"))["items"]!.AsArray().Should().HaveCount(1);
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{GymSlug}/summary"))["reviewCount"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task Regular_users_cannot_access_moderation()
    {
        var user = await factory.LoginAsync("normalo2@example.invalid", "Normalo");

        (await user.GetAsync(new Uri("/api/v1/moderation/reviews?status=SoftDeleted", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Summary_rebuild_command_recomputes_all_gyms()
    {
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/summaries/rebuild", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonNode>())!["rebuiltGyms"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(45);
    }
}
