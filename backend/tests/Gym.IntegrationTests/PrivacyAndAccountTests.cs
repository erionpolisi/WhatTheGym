using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public sealed class PrivacyAndAccountTests(WtgApiFactory factory)
{
    [Theory]
    [InlineData("account")]
    [InlineData("reviews")]
    [InlineData("reviewRevisions")]
    [InlineData("legalCasesAsReporter")]
    [InlineData("contactRequests")]
    public async Task Export_contains_expected_personal_data_sections_after_activity(string section)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Export Data", district: 12);
        var email = IntegrationTestSupport.UniqueEmail("export");
        var client = await factory.LoginAsync(email, "Export Nutzerin");
        var create = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new
        {
            ratings = new { equipment = 4 },
            text = "Erste Fassung fuer den DSGVO Export.",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var reviewId = (await IntegrationTestSupport.ReadJsonAsync(create))["id"]!.GetValue<Guid>();
        (await client.PutAsJsonAsync($"/api/v1/reviews/{reviewId}", new
        {
            ratings = new { staff = 5 },
            text = "Zweite Fassung fuer den DSGVO Export.",
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.CreateClient().PostAsJsonAsync("/api/v1/contact-requests", new
        {
            type = "General",
            name = "Export Nutzerin",
            email,
            message = "Bitte diese Kontaktanfrage im Export beruecksichtigen.",
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await factory.CreateClient().PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Other",
            reporterName = "Export Nutzerin",
            reporterEmail = email,
            description = "Diese Meldung soll im eigenen Export als Reporter-Fall erscheinen.",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var export = await client.GetJsonAsync("/api/v1/me/export");

        export[section].Should().NotBeNull();
        if (section == "account")
        {
            export["account"]!["email"]!.GetValue<string>().Should().Be(email);
        }
        else
        {
            export[section]!.AsArray().Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Account_deletion_removes_reviews_recomputes_summary_and_allows_fresh_login()
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Delete Account", district: 13);
        var email = IntegrationTestSupport.UniqueEmail("delete-account");
        var client = await factory.LoginAsync(email, "Zu Loeschen");
        var beforeMe = await client.GetJsonAsync("/api/v1/me");
        await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 5 }, "delete-helper");
        var create = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { equipment = 2 } });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.DeleteAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var summary = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/summary");
        summary["reviewCount"]!.GetValue<int>().Should().Be(1);
        summary["totalScore"]!.GetValue<double>().Should().Be(5);
        var fresh = await factory.LoginAsync(email, "Neu Angelegt");
        var afterMe = await fresh.GetJsonAsync("/api/v1/me");
        afterMe["id"]!.GetValue<Guid>().Should().NotBe(beforeMe["id"]!.GetValue<Guid>());
        afterMe["displayName"]!.GetValue<string>().Should().Be("Neu Angelegt");
    }

    [Fact]
    public async Task Account_deletion_with_review_legal_hold_still_removes_review_from_public_outputs()
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Held Delete", district: 14);
        var email = IntegrationTestSupport.UniqueEmail("held-delete");
        var client = await factory.LoginAsync(email, "Hold Nutzerin");
        var create = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings = new { equipment = 3 } });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var reviewId = (await IntegrationTestSupport.ReadJsonAsync(create))["id"]!.GetValue<Guid>();
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        (await admin.PostAsJsonAsync("/api/v1/admin/legal-holds", new
        {
            reason = "Regressionstest: Hold darf keine Oeffentlichkeit erzwingen.",
            reviewId,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.DeleteAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = factory.CreateClient();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().BeEmpty();
        (await anonymous.GetJsonAsync($"/api/v1/gyms/{slug}/summary"))["reviewCount"]!.GetValue<int>().Should().Be(0);
    }

    public static TheoryData<string, HttpStatusCode> ModerationRemovalValidationCases => new()
    {
        { "", HttpStatusCode.BadRequest },
        { " ", HttpStatusCode.BadRequest },
        { "Nachvollziehbare Moderationsbegruedung.", HttpStatusCode.NoContent },
    };

    [Theory]
    [MemberData(nameof(ModerationRemovalValidationCases))]
    public async Task Moderation_remove_requires_reason(string reason, HttpStatusCode expectedStatus)
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Mod Reason", district: 15);
        var reviewId = await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 4 }, "mod-reason");
        var moderator = await IntegrationTestSupport.LoginAsModeratorAsync(factory, "mod-reason");

        var response = await moderator.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/remove", new { reason });

        response.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Moderator_cannot_restore_but_admin_can_restore_removed_review()
    {
        var (_, slug) = await IntegrationTestSupport.CreateGymAsync(factory, "Mod Restore", district: 16);
        var reviewId = await IntegrationTestSupport.CreateReviewAsync(factory, slug, new { equipment = 4 }, "mod-restore");
        var moderator = await IntegrationTestSupport.LoginAsModeratorAsync(factory, "mod-restore");
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        (await moderator.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/remove", new { reason = "Entfernung fuer Restore-Test." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await moderator.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/restore", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"/api/v1/moderation/reviews/{reviewId}/restore", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews"))["items"]!.AsArray().Should().Contain(i => i!["id"]!.GetValue<Guid>() == reviewId);
    }
}