using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Works;

public sealed class WorkUpdateEndpointTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Updating_a_work_replaces_its_author_list()
    {
        var client = await AuthenticatedClientAsync("work-update-authors@test.local");
        var workId = await CreateWorkAsync(client, "Good Omens", ["Terry Pratchett"]);

        var putResponse = await client.PutJsonAsync($"/api/v1/works/{workId}", new
        {
            title = "Good Omens",
            originalTitle = (string?)null,
            description = (string?)null,
            firstPublicationYear = (int?)null,
            authorNames = new[] { "Terry Pratchett", "Neil Gaiman" },
        });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        var authorNames = work.GetProperty("authors").EnumerateArray().Select(a => a.GetProperty("fullName").GetString()).ToList();
        authorNames.Should().BeEquivalentTo(["Terry Pratchett", "Neil Gaiman"]);
    }

    [Fact]
    public async Task Updating_a_work_without_author_names_leaves_existing_authors_untouched()
    {
        var client = await AuthenticatedClientAsync("work-update-no-authors@test.local");
        var workId = await CreateWorkAsync(client, "Small Gods", ["Terry Pratchett"]);

        var putResponse = await client.PutJsonAsync($"/api/v1/works/{workId}", new
        {
            title = "Small Gods",
            originalTitle = (string?)null,
            description = "A Discworld novel",
            firstPublicationYear = (int?)null,
        });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        var authorNames = work.GetProperty("authors").EnumerateArray().Select(a => a.GetProperty("fullName").GetString()).ToList();
        authorNames.Should().BeEquivalentTo(["Terry Pratchett"]);
        work.GetProperty("description").GetString().Should().Be("A Discworld novel");
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateWorkAsync(HttpClient client, string title, string[] authorNames)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title, authorNames },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var editionId = created.GetProperty("editionId").GetGuid();

        var edition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}");
        return edition.GetProperty("workId").GetGuid();
    }
}
