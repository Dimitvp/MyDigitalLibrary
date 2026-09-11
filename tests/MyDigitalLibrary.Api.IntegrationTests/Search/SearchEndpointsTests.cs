using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Search;

public sealed class SearchEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Search_matches_a_work_by_title()
    {
        var client = await AuthenticatedClientAsync("search-title@test.local");
        await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "The Fellowship of the Ring", authorNames = new[] { "J.R.R. Tolkien" } },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/search?q=fellowship");

        results.EnumerateArray().Should().ContainSingle(r => r.GetProperty("title").GetString() == "The Fellowship of the Ring");
    }

    [Fact]
    public async Task Search_matches_a_work_by_author_name()
    {
        var client = await AuthenticatedClientAsync("search-author@test.local");
        await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Neuromancer", authorNames = new[] { "William Gibson" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/search?q=Gibson");

        results.EnumerateArray().Should().ContainSingle(r => r.GetProperty("title").GetString() == "Neuromancer");
    }

    [Fact]
    public async Task Search_matches_real_cyrillic_text_under_the_simple_configuration()
    {
        // Plan section 9: verified against a real Postgres 17 instance that it
        // has no Bulgarian stemmer dictionary — "simple" (case-fold + stopword
        // removal only) is used throughout, and does correctly match Cyrillic.
        var client = await AuthenticatedClientAsync("search-cyrillic@test.local");
        await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new
            {
                title = "Дюна",
                originalTitle = "Dune",
                authorNames = new[] { "Франк Хърбърт" },
            },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });

        var byTitle = await client.GetFromJsonAsync<JsonElement>("/api/v1/search?q=" + Uri.EscapeDataString("дюна"));
        var byAuthor = await client.GetFromJsonAsync<JsonElement>("/api/v1/search?q=" + Uri.EscapeDataString("Хърбърт"));

        byTitle.EnumerateArray().Should().ContainSingle(r => r.GetProperty("title").GetString() == "Дюна");
        byAuthor.EnumerateArray().Should().ContainSingle(r => r.GetProperty("title").GetString() == "Дюна");
    }

    [Fact]
    public async Task Search_returns_empty_for_no_match()
    {
        var client = await AuthenticatedClientAsync("search-empty@test.local");

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/search?q=zzz-nonexistent-xyz");

        results.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Search_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search?q=anything");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }
}
