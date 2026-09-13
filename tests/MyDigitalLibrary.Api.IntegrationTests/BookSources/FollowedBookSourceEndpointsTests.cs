using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.BookSources;

public sealed class FollowedBookSourceEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Adding_a_book_source_shows_it_on_a_subsequent_list()
    {
        var client = await AuthenticatedClientAsync("book-source-add@test.local");

        var createResponse = await client.PostJsonAsync("/api/v1/book-sources", new
        {
            name = "Ciela",
            url = "https://www.ciela.com",
            category = "bookstore",
            notes = (string?)null,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/book-sources");
        var source = list.EnumerateArray().Single();
        source.GetProperty("name").GetString().Should().Be("Ciela");
        source.GetProperty("url").GetString().Should().Be("https://www.ciela.com");
        source.GetProperty("category").GetString().Should().Be("bookstore");
    }

    [Fact]
    public async Task Adding_the_same_url_twice_is_rejected()
    {
        var client = await AuthenticatedClientAsync("book-source-dup@test.local");
        var request = new { name = "Libgen", url = "http://libgen.rs/", category = (string?)null, notes = (string?)null };

        var first = await client.PostJsonAsync("/api/v1/book-sources", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostJsonAsync("/api/v1/book-sources", request);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_a_book_source_changes_its_fields()
    {
        var client = await AuthenticatedClientAsync("book-source-update@test.local");
        var id = await CreateBookSourceAsync(client);

        var updateResponse = await client.PutJsonAsync($"/api/v1/book-sources/{id}", new
        {
            name = "Ciela.com",
            url = "https://www.ciela.com",
            category = "bookstore",
            notes = "Chain of physical stores too",
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/book-sources");
        list.EnumerateArray().Single().GetProperty("notes").GetString().Should().Be("Chain of physical stores too");
    }

    [Fact]
    public async Task Deleting_a_book_source_removes_it_from_the_list()
    {
        var client = await AuthenticatedClientAsync("book-source-delete@test.local");
        var id = await CreateBookSourceAsync(client);

        var deleteResponse = await client.DeleteWithAntiforgeryAsync($"/api/v1/book-sources/{id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/book-sources");
        list.EnumerateArray().Should().BeEmpty();
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateBookSourceAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/book-sources", new
        {
            name = "Ciela",
            url = "https://www.ciela.com",
            category = "bookstore",
            notes = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}
