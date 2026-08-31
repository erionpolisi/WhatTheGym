using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Gym.IntegrationTests;

[Collection("api")]
public sealed class AuthContractTests(WtgApiFactory factory)
{
    public static TheoryData<string, bool> DevLoginCases => new()
    {
        { "verified", true },
        { "unverified", false },
        { "trimmed", true },
        { "caps", true },
    };

    [Theory]
    [MemberData(nameof(DevLoginCases))]
    public async Task Dev_login_returns_me_contract(string prefix, bool verified)
    {
        var email = IntegrationTestSupport.UniqueEmail($"auth-{prefix}");
        var client = await factory.LoginAsync(email, "Auth Nutzerin", verified);

        var me = await client.GetJsonAsync("/api/v1/me");

        me["email"]!.GetValue<string>().Should().Be(email);
        me["emailVerified"]!.GetValue<bool>().Should().Be(verified);
        me["displayName"]!.GetValue<string>().Should().Be("Auth Nutzerin");
        me["role"]!.GetValue<string>().Should().Be("User");
        me["id"]!.GetValue<Guid>().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData(" ", HttpStatusCode.BadRequest)]
    [InlineData("A", HttpStatusCode.BadRequest)]
    [InlineData("AB", HttpStatusCode.OK)]
    [InlineData("A valid public display name", HttpStatusCode.OK)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", HttpStatusCode.OK)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", HttpStatusCode.BadRequest)]
    public async Task Profile_update_validates_display_name_bounds(string displayName, HttpStatusCode expectedStatus)
    {
        var client = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("profile"), "Profil");

        var response = await client.PutAsJsonAsync("/api/v1/me", new { displayName });

        response.StatusCode.Should().Be(expectedStatus);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var me = await IntegrationTestSupport.ReadJsonAsync(response);
            me["displayName"]!.GetValue<string>().Should().Be(displayName.Trim());
        }
        else
        {
            IntegrationTestSupport.AssertProblem(await IntegrationTestSupport.ReadJsonAsync(response), 400);
        }
    }

    [Theory]
    [InlineData("anonymous", "/api/v1/me", HttpStatusCode.Unauthorized)]
    [InlineData("anonymous", "/api/v1/me/export", HttpStatusCode.Unauthorized)]
    [InlineData("anonymous", "/api/v1/me/reviews", HttpStatusCode.Unauthorized)]
    [InlineData("user", "/api/v1/me", HttpStatusCode.OK)]
    [InlineData("user", "/api/v1/me/export", HttpStatusCode.OK)]
    [InlineData("user", "/api/v1/me/reviews", HttpStatusCode.OK)]
    public async Task Me_endpoints_require_authentication_and_return_user_data(string role, string path, HttpStatusCode expectedStatus)
    {
        var client = role == "user"
            ? await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("me-contract"), "Ich")
            : factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(expectedStatus);
        if (expectedStatus == HttpStatusCode.OK)
        {
            (await IntegrationTestSupport.ReadJsonAsync(response)).Should().NotBeNull();
        }
    }

    public static TheoryData<string, string, HttpStatusCode> RoleMatrixCases => new()
    {
        { "anonymous", "admin-users", HttpStatusCode.Unauthorized },
        { "user", "admin-users", HttpStatusCode.Forbidden },
        { "moderator", "admin-users", HttpStatusCode.Forbidden },
        { "admin", "admin-users", HttpStatusCode.OK },
        { "anonymous", "admin-gyms", HttpStatusCode.Unauthorized },
        { "user", "admin-gyms", HttpStatusCode.Forbidden },
        { "moderator", "admin-gyms", HttpStatusCode.Forbidden },
        { "admin", "admin-gyms", HttpStatusCode.OK },
        { "anonymous", "admin-contacts", HttpStatusCode.Unauthorized },
        { "user", "admin-contacts", HttpStatusCode.Forbidden },
        { "moderator", "admin-contacts", HttpStatusCode.Forbidden },
        { "admin", "admin-contacts", HttpStatusCode.OK },
        { "anonymous", "admin-legal-cases", HttpStatusCode.Unauthorized },
        { "user", "admin-legal-cases", HttpStatusCode.Forbidden },
        { "moderator", "admin-legal-cases", HttpStatusCode.OK },
        { "admin", "admin-legal-cases", HttpStatusCode.OK },
        { "anonymous", "moderation-reviews", HttpStatusCode.Unauthorized },
        { "user", "moderation-reviews", HttpStatusCode.Forbidden },
        { "moderator", "moderation-reviews", HttpStatusCode.OK },
        { "admin", "moderation-reviews", HttpStatusCode.OK },
        { "anonymous", "admin-summary-rebuild", HttpStatusCode.Unauthorized },
        { "user", "admin-summary-rebuild", HttpStatusCode.Forbidden },
        { "moderator", "admin-summary-rebuild", HttpStatusCode.Forbidden },
        { "admin", "admin-summary-rebuild", HttpStatusCode.OK },
        { "anonymous", "admin-role-change", HttpStatusCode.Unauthorized },
        { "user", "admin-role-change", HttpStatusCode.Forbidden },
        { "moderator", "admin-role-change", HttpStatusCode.Forbidden },
        { "admin", "admin-role-change", HttpStatusCode.NotFound },
    };

    [Theory]
    [MemberData(nameof(RoleMatrixCases))]
    public async Task Protected_endpoint_role_matrix_matches_contract(string role, string endpoint, HttpStatusCode expectedStatus)
    {
        var client = role switch
        {
            "admin" => await factory.LoginAsync("admin@example.invalid", "Admin"),
            "moderator" => await IntegrationTestSupport.LoginAsModeratorAsync(factory, "auth-matrix-mod"),
            "user" => await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("auth-matrix-user"), "Normalo"),
            _ => factory.CreateClient(),
        };

        var response = await SendProtectedRequestAsync(client, endpoint);

        response.StatusCode.Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData("/api/v1/auth/refresh", HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/auth/logout", HttpStatusCode.NoContent)]
    public async Task Auth_session_endpoints_have_anonymous_contract(string path, HttpStatusCode expectedStatus)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri(path, UriKind.Relative), null);

        response.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Logout_clears_session_and_revokes_refresh_cookie()
    {
        var client = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("logout"), "Logout");

        (await client.PostAsync(new Uri("/api/v1/auth/logout", UriKind.Relative), null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unverified_account_has_no_verified_author_badge_when_content_exists_from_verified_login_later()
    {
        var unverifiedEmail = IntegrationTestSupport.UniqueEmail("unverified-badge");
        var unverified = await factory.LoginAsync(unverifiedEmail, "Badge Test", emailVerified: false);
        (await unverified.PostAsJsonAsync("/api/v1/gyms/fitinn-wien-kendlerstrasse/reviews", new { ratings = new { equipment = 3 } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var verified = await factory.LoginAsync(IntegrationTestSupport.UniqueEmail("verified-badge"), "Badge Test Verified");
        var create = await verified.PostAsJsonAsync("/api/v1/gyms/fitinn-wien-kendlerstrasse/reviews", new { ratings = new { equipment = 3 } });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await verified.GetJsonAsync("/api/v1/gyms/fitinn-wien-kendlerstrasse/reviews");
        list["items"]!.AsArray().Should().Contain(i => i!["author"]!["verifiedViaGoogle"]!.GetValue<bool>());
    }

    private static Task<HttpResponseMessage> SendProtectedRequestAsync(HttpClient client, string endpoint) => endpoint switch
    {
        "admin-users" => client.GetAsync(new Uri("/api/v1/admin/users?pageSize=1", UriKind.Relative)),
        "admin-gyms" => client.GetAsync(new Uri("/api/v1/admin/gyms?pageSize=1", UriKind.Relative)),
        "admin-contacts" => client.GetAsync(new Uri("/api/v1/admin/contact-requests?pageSize=1", UriKind.Relative)),
        "admin-legal-cases" => client.GetAsync(new Uri("/api/v1/admin/legal-cases?pageSize=1", UriKind.Relative)),
        "moderation-reviews" => client.GetAsync(new Uri("/api/v1/moderation/reviews?pageSize=1", UriKind.Relative)),
        "admin-summary-rebuild" => client.PostAsJsonAsync("/api/v1/admin/summaries/rebuild", new { }),
        "admin-role-change" => client.PutAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/role", new { role = "User" }),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint."),
    };
}