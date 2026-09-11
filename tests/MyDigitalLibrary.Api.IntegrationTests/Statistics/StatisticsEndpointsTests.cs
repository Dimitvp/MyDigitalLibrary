using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Statistics;

public sealed class StatisticsEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task A_fresh_user_has_zeroed_statistics()
    {
        var client = await AuthenticatedClientAsync("stats-empty@test.local");

        var stats = await client.GetFromJsonAsync<JsonElement>("/api/v1/statistics?year=2026");

        stats.GetProperty("totalLibraryItems").GetInt32().Should().Be(0);
        stats.GetProperty("averageRating").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("topAuthors").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Counts_reflect_format_status_and_finished_reading_within_the_year()
    {
        var client = await AuthenticatedClientAsync("stats-counts@test.local");

        var createResponse = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Stats Test Book", authorNames = new[] { "Frank Herbert" } },
            edition = new { pageCount = 400 },
            format = "Physical",
            acquisition = new { acquiredOn = "2020-01-01", method = "Bought" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = created.GetProperty("id").GetGuid();
        var editionId = created.GetProperty("editionId").GetGuid();
        var edition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}");
        var workId = edition.GetProperty("workId").GetGuid();

        await client.PutJsonAsync($"/api/v1/works/{workId}/rating", new { score = 7 });

        var startResponse = await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId, startedOn = "2020-01-01" });
        var sessionId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/finish", new { endedOn = "2020-03-01" });

        var stats = await client.GetFromJsonAsync<JsonElement>("/api/v1/statistics?year=2020");

        stats.GetProperty("totalLibraryItems").GetInt32().Should().Be(1);
        stats.GetProperty("byFormat").GetProperty("Physical").GetInt32().Should().Be(1);
        stats.GetProperty("byStatus").GetProperty("Owned").GetInt32().Should().Be(1);
        stats.GetProperty("booksFinishedThisYear").GetInt32().Should().Be(1);
        stats.GetProperty("pagesReadThisYear").GetInt32().Should().Be(400);
        stats.GetProperty("averageRating").GetDouble().Should().Be(7);
        stats.GetProperty("topAuthors").EnumerateArray().Single().GetProperty("authorName").GetString().Should().Be("Frank Herbert");
    }

    [Fact]
    public async Task A_currently_reading_session_is_counted_regardless_of_year()
    {
        var client = await AuthenticatedClientAsync("stats-reading@test.local");
        var createResponse = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Currently Reading Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        var itemId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId });

        var stats = await client.GetFromJsonAsync<JsonElement>("/api/v1/statistics?year=2026");

        stats.GetProperty("currentlyReadingCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Omitting_year_defaults_to_the_current_year()
    {
        var client = await AuthenticatedClientAsync("stats-default-year@test.local");

        var response = await client.GetAsync("/api/v1/statistics");

        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<JsonElement>();
        stats.GetProperty("year").GetInt32().Should().Be(DateTime.UtcNow.Year);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }
}
