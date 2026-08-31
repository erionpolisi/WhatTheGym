using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Gym.Application.Abstractions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Gym.IntegrationTests.IntegrationTestSupport;

namespace Gym.IntegrationTests;

/// <summary>End-to-end coverage for the 2026-08 security hardening fixes.</summary>
[Collection("api")]
public sealed class SecurityHardeningApiTests(WtgApiFactory factory)
{
    [Fact]
    public async Task Csrf_check_blocks_authenticated_body_less_write_without_header()
    {
        var client = await factory.LoginAsync(UniqueEmail("csrf"), "Csrf Testerin");
        client.DefaultRequestHeaders.Remove("X-CSRF");

        var response = await client.PostAsync(new Uri("/api/v1/auth/logout", UriKind.Relative), null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await ReadJsonAsync(response);
        problem["code"]!.GetValue<string>().Should().Be("auth.csrf");
    }

    [Fact]
    public async Task Csrf_check_accepts_json_requests_and_anonymous_requests()
    {
        // Anonymous body-less POST passes (no session to protect).
        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Remove("X-CSRF");
        (await anonymous.PostAsync(new Uri("/api/v1/auth/logout", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Authenticated JSON request passes without the header (forms cannot send JSON).
        var client = await factory.LoginAsync(UniqueEmail("csrf-json"), "Csrf Json");
        client.DefaultRequestHeaders.Remove("X-CSRF");
        var response = await client.PutAsJsonAsync("/api/v1/me", new { displayName = "Neuer Name" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Role_change_takes_effect_on_the_existing_session()
    {
        var email = UniqueEmail("session-reval");
        var client = await factory.LoginAsync(email, "Reval Testerin");

        (await client.GetAsync(new Uri("/api/v1/moderation/reviews", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var userId = await FindUserIdAsync(admin, email);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{userId}/role", new { role = "Moderator" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Same cookie, no re-login: promotion is picked up by per-request revalidation.
        (await client.GetAsync(new Uri("/api/v1/moderation/reviews", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{userId}/role", new { role = "User" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri("/api/v1/moderation/reviews", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deleted_account_session_is_rejected_immediately()
    {
        var client = await factory.LoginAsync(UniqueEmail("session-delete"), "Delete Testerin");
        (await client.DeleteAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Database_enforces_one_active_review_per_user_and_gym()
    {
        var (gymId, _) = await CreateGymAsync(factory, "UniqueIdx");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.CreateFromGoogle($"sub-{Guid.NewGuid():N}", UniqueEmail("uniqueidx"), true, "Racerin", DateTimeOffset.UtcNow);
        context.Users.Add(user);
        context.Reviews.Add(Review.Create(gymId, user.Id, new ReviewRatings { Staff = 4 }, null, DateTimeOffset.UtcNow).Value);
        await ((IUnitOfWork)context).SaveChangesAsync(CancellationToken.None);

        // Second active review for the same user+gym bypasses the handler check on purpose.
        context.Reviews.Add(Review.Create(gymId, user.Id, new ReviewRatings { Staff = 5 }, null, DateTimeOffset.UtcNow).Value);
        var act = () => ((IUnitOfWork)context).SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task Audit_trail_never_contains_usable_tokens()
    {
        var (_, slug) = await CreateGymAsync(factory, "AuditMask");
        var reviewId = await CreateReviewAsync(factory, slug, new { staff = 2 }, "auditmask");

        var reporter = factory.CreateClient();
        var reportResponse = await reporter.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "Defamation",
            reporterName = "Melderin",
            reporterEmail = UniqueEmail("reporter"),
            description = "Diese Bewertung enthaelt eine nachweislich falsche Tatsachenbehauptung.",
        });
        reportResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = await ReadJsonAsync(reportResponse);
        var statusToken = report["statusToken"]!.GetValue<string>();
        var caseNumber = report["caseNumber"]!.GetValue<string>();

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var cases = await admin.GetJsonAsync("/api/v1/admin/legal-cases?pageSize=100");
        var caseId = cases["items"]!.AsArray()
            .First(c => c!["caseNumber"]!.GetValue<string>() == caseNumber)!["id"]!.GetValue<Guid>();

        // Decide to also generate the appeal-token notification events.
        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/decide", new
        {
            decision = "KeepOnline",
            rationale = "Zulaessige Meinungsaeusserung nach Pruefung.",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await admin.GetJsonAsync($"/api/v1/admin/legal-cases/{caseId}");
        var detailJson = detail.ToJsonString();
        detailJson.Should().NotContain(statusToken);
        detailJson.Should().ContainAny("***", "%2A%2A%2A"); // masked token marker (raw or url-encoded)

        // The confidential status link keeps working for the reporter.
        (await reporter.GetAsync(new Uri($"/api/v1/legal/cases/{caseNumber}/status?token={statusToken}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reclassifying_fast_track_to_normal_republishes_the_review()
    {
        var (_, slug) = await CreateGymAsync(factory, "FastTrackBack");
        var reviewId = await CreateReviewAsync(factory, slug, new { staff = 1 }, "fasttrackback");

        var reportResponse = await factory.CreateClient().PostAsJsonAsync($"/api/v1/reviews/{reviewId}/report", new
        {
            category = "IllegalContent",
            reporterName = "Melder",
            reporterEmail = UniqueEmail("ftreporter"),
            description = "Der Inhalt dieser Bewertung ist offensichtlich rechtswidrig.",
        });
        var caseNumber = (await ReadJsonAsync(reportResponse))["caseNumber"]!.GetValue<string>();

        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var cases = await admin.GetJsonAsync("/api/v1/admin/legal-cases?pageSize=100");
        var caseId = cases["items"]!.AsArray()
            .First(c => c!["caseNumber"]!.GetValue<string>() == caseNumber)!["id"]!.GetValue<Guid>();

        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/classify", new { classification = "FastTrackObviouslyIllegal" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var hidden = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews");
        hidden["items"]!.AsArray().Should().NotContain(r => r!["id"]!.GetValue<Guid>() == reviewId);

        (await admin.PostAsJsonAsync($"/api/v1/admin/legal-cases/{caseId}/classify", new { classification = "Normal" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var visible = await factory.CreateClient().GetJsonAsync($"/api/v1/gyms/{slug}/reviews");
        visible["items"]!.AsArray().Should().Contain(r => r!["id"]!.GetValue<Guid>() == reviewId);
    }

    [Fact]
    public async Task Retention_sweeper_honors_user_scoped_legal_holds()
    {
        var (gymId, slug) = await CreateGymAsync(factory, "UserHold");
        var reviewId = await CreateReviewAsync(factory, slug, new { staff = 3 }, "userhold");
        _ = gymId;

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = await context.Reviews.SingleAsync(r => r.Id == reviewId);
        var longAgo = DateTimeOffset.UtcNow.AddYears(-10);

        context.ReviewRevisions.Add(ReviewRevision.Create(review.Id, 1, "Alt", "{}", review.UserId, longAgo));
        review.SoftDelete(ReviewDeletionOrigin.Author, "Test", longAgo);
        context.LegalHolds.Add(LegalHold.Create("Beweissicherung fuer offenes Verfahren.", null, null, review.UserId, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        var sweeper = new RetentionSweeper(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Gym.Application.Common.RetentionOptions>>(),
            factory.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetentionSweeper>>());
        await sweeper.SweepAsync(CancellationToken.None);

        (await context.ReviewRevisions.CountAsync(r => r.ReviewId == reviewId)).Should().Be(1, "the user-scoped hold must pause deletion");
    }

    [Fact]
    public async Task Chain_website_must_be_an_absolute_http_url()
    {
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/chains", new
        {
            name = UniqueName("BadChain"),
            website = "javascript:alert(1)",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadJsonAsync(response);
        problem["code"]!.GetValue<string>().Should().Be("chain.website");
    }
}
