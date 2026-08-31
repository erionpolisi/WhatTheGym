using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace Gym.IntegrationTests;

internal static class IntegrationTestSupport
{
    internal static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.invalid";

    internal static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    internal static string SlugFor(string name) => name.ToLowerInvariant().Replace(' ', '-');

    internal static async Task<(Guid Id, string Slug)> CreateGymAsync(WtgApiFactory factory, string prefix, int district = 1, string status = "Active")
    {
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var name = UniqueName(prefix);
        var response = await admin.PostAsJsonAsync("/api/v1/admin/gyms", new
        {
            name,
            district,
            addressLine = $"{prefix} Testgasse 1",
            postalCode = $"1{district:00}0",
            status,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<Guid>();
        return (id, SlugFor(name));
    }

    internal static async Task<Guid> CreateReviewAsync(
        WtgApiFactory factory,
        string slug,
        object ratings,
        string emailPrefix,
        string text = "Integrationstest Bewertung mit ausreichend langem Text.")
    {
        var client = await factory.LoginAsync(UniqueEmail(emailPrefix), "Integration Testerin");
        var response = await client.PostAsJsonAsync($"/api/v1/gyms/{slug}/reviews", new { ratings, text });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<Guid>();
    }

    internal static async Task<HttpClient> LoginAsModeratorAsync(WtgApiFactory factory, string emailPrefix)
    {
        var email = UniqueEmail(emailPrefix);
        _ = await factory.LoginAsync(email, "Moderatorin");
        var admin = await factory.LoginAsync("admin@example.invalid", "Admin");
        var userId = await FindUserIdAsync(admin, email);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{userId}/role", new { role = "Moderator" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        return await factory.LoginAsync(email, "Moderatorin");
    }

    internal static async Task<Guid> FindUserIdAsync(HttpClient admin, string email)
    {
        for (var page = 1; page <= 20; page++)
        {
            var users = await admin.GetJsonAsync($"/api/v1/admin/users?page={page}&pageSize=100");
            var match = users["items"]!.AsArray()
                .FirstOrDefault(u => string.Equals(u!["email"]!.GetValue<string>(), email, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match["id"]!.GetValue<Guid>();
            }
        }

        throw new InvalidOperationException($"User {email} was not found in admin list.");
    }

    internal static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonNode>())!;

    internal static void AssertProblem(JsonNode problem, int status)
    {
        problem["title"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        problem["status"]!.GetValue<int>().Should().Be(status);
        problem["type"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }
}