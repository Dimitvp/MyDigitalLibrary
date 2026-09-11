using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.ReadingGoals;

public sealed class ReadingGoalEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Getting_a_goal_that_was_never_set_returns_null_targets_with_zero_progress()
    {
        var client = await AuthenticatedClientAsync("goal-none@test.local");

        var goal = await client.GetFromJsonAsync<JsonElement>("/api/v1/reading-goals/2019");

        goal.GetProperty("targetBooks").ValueKind.Should().Be(JsonValueKind.Null);
        goal.GetProperty("booksFinished").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Setting_a_goal_then_getting_it_reflects_the_targets()
    {
        var client = await AuthenticatedClientAsync("goal-set@test.local");

        var putResponse = await client.PutJsonAsync("/api/v1/reading-goals/2026", new { targetBooks = 24, targetPages = 6000 });
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var goal = await client.GetFromJsonAsync<JsonElement>("/api/v1/reading-goals/2026");
        goal.GetProperty("targetBooks").GetInt32().Should().Be(24);
        goal.GetProperty("targetPages").GetInt32().Should().Be(6000);
    }

    [Fact]
    public async Task Setting_a_goal_twice_for_the_same_year_updates_it_rather_than_duplicating()
    {
        var client = await AuthenticatedClientAsync("goal-upsert@test.local");

        await client.PutJsonAsync("/api/v1/reading-goals/2026", new { targetBooks = 12, targetPages = (int?)null });
        await client.PutJsonAsync("/api/v1/reading-goals/2026", new { targetBooks = 30, targetPages = (int?)null });

        var goal = await client.GetFromJsonAsync<JsonElement>("/api/v1/reading-goals/2026");
        goal.GetProperty("targetBooks").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task Finishing_a_session_within_the_year_counts_toward_books_and_pages_finished()
    {
        var client = await AuthenticatedClientAsync("goal-progress@test.local");

        var createResponse = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Goal Progress Book", authorNames = new[] { "Test Author" } },
            edition = new { pageCount = 320 },
            format = "Physical",
            acquisition = new { acquiredOn = "2020-01-01", method = "Bought" },
        });
        var itemId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var startResponse = await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId, startedOn = "2020-01-01" });
        var sessionId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/finish", new { endedOn = "2020-02-01" });

        var goal = await client.GetFromJsonAsync<JsonElement>("/api/v1/reading-goals/2020");
        goal.GetProperty("booksFinished").GetInt32().Should().Be(1);
        goal.GetProperty("pagesRead").GetInt32().Should().Be(320);

        var otherYearGoal = await client.GetFromJsonAsync<JsonElement>("/api/v1/reading-goals/2021");
        otherYearGoal.GetProperty("booksFinished").GetInt32().Should().Be(0, "the session finished in 2020, not 2021");
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }
}
