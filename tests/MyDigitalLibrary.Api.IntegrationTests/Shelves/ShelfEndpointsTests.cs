using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Shelves;

public sealed class ShelfEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Creating_a_shelf_then_listing_shows_it_with_a_zero_item_count()
    {
        var client = await AuthenticatedClientAsync("shelf-create@test.local");

        var createResponse = await client.PostJsonAsync("/api/v1/shelves", new { name = "Currently Reading" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/shelves");
        var shelf = list.EnumerateArray().Single();
        shelf.GetProperty("name").GetString().Should().Be("Currently Reading");
        shelf.GetProperty("itemCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Creating_two_shelves_with_the_same_name_is_rejected_as_a_conflict()
    {
        var client = await AuthenticatedClientAsync("shelf-dup@test.local");
        await client.PostJsonAsync("/api/v1/shelves", new { name = "Favorites" });

        var second = await client.PostJsonAsync("/api/v1/shelves", new { name = "Favorites" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("shelf.duplicate_name");
    }

    [Fact]
    public async Task Adding_a_library_item_to_a_shelf_shows_it_enriched_with_title_and_authors_on_get()
    {
        var client = await AuthenticatedClientAsync("shelf-add-item@test.local");
        var shelfId = await CreateShelfAsync(client, "To Read");
        var itemId = await CreateLibraryItemAsync(client);

        var addResponse = await client.PostJsonAsync($"/api/v1/shelves/{shelfId}/items", new { libraryItemId = itemId });
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var shelf = await client.GetFromJsonAsync<JsonElement>($"/api/v1/shelves/{shelfId}");
        var item = shelf.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("libraryItemId").GetGuid().Should().Be(itemId);
        item.GetProperty("workTitle").GetString().Should().Be("Shelf Test Book");
    }

    [Fact]
    public async Task Removing_an_item_from_a_shelf_takes_it_out_of_the_list()
    {
        var client = await AuthenticatedClientAsync("shelf-remove-item@test.local");
        var shelfId = await CreateShelfAsync(client, "Temp Shelf");
        var itemId = await CreateLibraryItemAsync(client);
        await client.PostJsonAsync($"/api/v1/shelves/{shelfId}/items", new { libraryItemId = itemId });

        var removeResponse = await client.DeleteWithAntiforgeryAsync($"/api/v1/shelves/{shelfId}/items/{itemId}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var shelf = await client.GetFromJsonAsync<JsonElement>($"/api/v1/shelves/{shelfId}");
        shelf.GetProperty("items").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_shelf_removes_it_from_the_list()
    {
        var client = await AuthenticatedClientAsync("shelf-delete@test.local");
        var shelfId = await CreateShelfAsync(client, "Disposable");

        var deleteResponse = await client.DeleteWithAntiforgeryAsync($"/api/v1/shelves/{shelfId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/shelves");
        list.EnumerateArray().Should().BeEmpty();
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateShelfAsync(HttpClient client, string name)
    {
        var response = await client.PostJsonAsync("/api/v1/shelves", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var shelf = await response.Content.ReadFromJsonAsync<JsonElement>();
        return shelf.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLibraryItemAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Shelf Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}
