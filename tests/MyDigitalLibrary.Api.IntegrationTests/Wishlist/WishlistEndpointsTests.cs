using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Wishlist;

public sealed class WishlistEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Create_is_blocked_when_an_owned_copy_matches_title_language_and_format()
    {
        var client = await AuthenticatedClientAsync("wishlist-owned-exact-match@test.local");
        await CreateOwnedPhysicalBookAsync(client, "Nexus", ["Yuval Noah Harari"], "en");

        var response = await client.PostJsonAsync("/api/v1/wishlist", new
        {
            work = new { title = "Nexus", authorNames = new[] { "Yuval Noah Harari" } },
            desiredFormat = "Physical",
            priority = 3,
            language = "en",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("wishlist_entry.already_owned");
    }

    [Fact]
    public async Task Create_is_allowed_when_the_owned_copy_is_a_different_format_even_in_the_same_language()
    {
        var client = await AuthenticatedClientAsync("wishlist-different-format@test.local");
        await CreateOwnedPhysicalBookAsync(client, "Sapiens", ["Yuval Noah Harari"], "en");

        // Owns it as Physical (en) — wanting the Ebook (en) is a legitimate separate want, not a duplicate.
        var response = await client.PostJsonAsync("/api/v1/wishlist", new
        {
            work = new { title = "Sapiens", authorNames = new[] { "Yuval Noah Harari" } },
            desiredFormat = "Ebook",
            priority = 3,
            language = "en",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_is_allowed_when_the_owned_copy_is_a_different_language_even_in_the_same_format()
    {
        var client = await AuthenticatedClientAsync("wishlist-different-language@test.local");
        await CreateOwnedPhysicalBookAsync(client, "Homo Deus", ["Yuval Noah Harari"], "bg");

        // Owns the Bulgarian physical edition — wanting the English physical original is a separate want.
        var response = await client.PostJsonAsync("/api/v1/wishlist", new
        {
            work = new { title = "Homo Deus", authorNames = new[] { "Yuval Noah Harari" } },
            desiredFormat = "Physical",
            priority = 3,
            language = "en",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task CreateOwnedPhysicalBookAsync(HttpClient client, string title, string[] authorNames, string language)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title, authorNames },
            edition = new { language },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
