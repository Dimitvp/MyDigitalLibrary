using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Quotes;

public sealed class QuoteEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Adding_a_quote_to_a_work_shows_it_on_a_subsequent_list()
    {
        var client = await AuthenticatedClientAsync("quote-add@test.local");
        var workId = await CreateWorkAsync(client);

        var createResponse = await client.PostJsonAsync($"/api/v1/works/{workId}/quotes", new { text = "Fear is the mind-killer.", pageOrPosition = "p. 8" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}/quotes");
        var quote = list.EnumerateArray().Single();
        quote.GetProperty("text").GetString().Should().Be("Fear is the mind-killer.");
        quote.GetProperty("pageOrPosition").GetString().Should().Be("p. 8");
    }

    [Fact]
    public async Task Updating_a_quote_changes_its_text()
    {
        var client = await AuthenticatedClientAsync("quote-update@test.local");
        var workId = await CreateWorkAsync(client);
        var quoteId = await CreateQuoteAsync(client, workId);

        var updateResponse = await client.PutJsonAsync($"/api/v1/quotes/{quoteId}", new { text = "Revised quote.", pageOrPosition = (string?)null });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}/quotes");
        list.EnumerateArray().Single().GetProperty("text").GetString().Should().Be("Revised quote.");
    }

    [Fact]
    public async Task Deleting_a_quote_removes_it_from_the_list()
    {
        var client = await AuthenticatedClientAsync("quote-delete@test.local");
        var workId = await CreateWorkAsync(client);
        var quoteId = await CreateQuoteAsync(client, workId);

        var deleteResponse = await client.DeleteWithAntiforgeryAsync($"/api/v1/quotes/{quoteId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}/quotes");
        list.EnumerateArray().Should().BeEmpty();
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
            work = new { title = "Quote Test Book", authorNames = new[] { "Test Author" } },
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

    private static async Task<Guid> CreateQuoteAsync(HttpClient client, Guid workId)
    {
        var response = await client.PostJsonAsync($"/api/v1/works/{workId}/quotes", new { text = "Original quote.", pageOrPosition = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();
        return quote.GetProperty("id").GetGuid();
    }
}
