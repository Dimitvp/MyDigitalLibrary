using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Works;

public sealed class WorkRatingReviewEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Rating_a_work_is_reflected_on_a_subsequent_get()
    {
        var client = await AuthenticatedClientAsync("rating@test.local");
        var workId = await CreateWorkAsync(client);

        var putResponse = await client.PutJsonAsync($"/api/v1/works/{workId}/rating", new { score = 9 });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        work.GetProperty("myRating").GetInt32().Should().Be(9);
    }

    [Fact]
    public async Task Rating_the_same_work_twice_updates_the_existing_rating_rather_than_creating_a_second_one()
    {
        var client = await AuthenticatedClientAsync("rating-upsert@test.local");
        var workId = await CreateWorkAsync(client);

        await client.PutJsonAsync($"/api/v1/works/{workId}/rating", new { score = 5 });
        await client.PutJsonAsync($"/api/v1/works/{workId}/rating", new { score = 8 });

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        work.GetProperty("myRating").GetInt32().Should().Be(8);
    }

    [Fact]
    public async Task Rating_out_of_the_1_to_10_range_is_rejected()
    {
        var client = await AuthenticatedClientAsync("rating-range@test.local");
        var workId = await CreateWorkAsync(client);

        var response = await client.PutJsonAsync($"/api/v1/works/{workId}/rating", new { score = 11 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reviewing_a_work_is_reflected_on_a_subsequent_get()
    {
        var client = await AuthenticatedClientAsync("review@test.local");
        var workId = await CreateWorkAsync(client);

        var putResponse = await client.PutJsonAsync($"/api/v1/works/{workId}/review", new { text = "A desert planet epic." });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        work.GetProperty("myReview").GetString().Should().Be("A desert planet epic.");
    }

    [Fact]
    public async Task Reviewing_the_same_work_twice_replaces_the_previous_review_text()
    {
        var client = await AuthenticatedClientAsync("review-upsert@test.local");
        var workId = await CreateWorkAsync(client);

        await client.PutJsonAsync($"/api/v1/works/{workId}/review", new { text = "First draft." });
        await client.PutJsonAsync($"/api/v1/works/{workId}/review", new { text = "Revised review." });

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        work.GetProperty("myReview").GetString().Should().Be("Revised review.");
    }

    [Fact]
    public async Task A_work_with_no_rating_or_review_yet_reports_both_as_null()
    {
        var client = await AuthenticatedClientAsync("rating-none@test.local");
        var workId = await CreateWorkAsync(client);

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");

        work.GetProperty("myRating").ValueKind.Should().Be(JsonValueKind.Null);
        work.GetProperty("myReview").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateWorkAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Rating Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var editionId = created.GetProperty("editionId").GetGuid();

        var edition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}");
        return edition.GetProperty("workId").GetGuid();
    }
}
