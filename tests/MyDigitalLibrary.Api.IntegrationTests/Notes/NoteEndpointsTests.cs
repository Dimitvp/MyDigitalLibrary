using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Notes;

public sealed class NoteEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Adding_a_note_to_a_library_item_shows_it_on_a_subsequent_list()
    {
        var client = await AuthenticatedClientAsync("note-add@test.local");
        var itemId = await CreateLibraryItemAsync(client);

        var createResponse = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/notes", new { body = "Great opening chapter.", locationInBook = "p. 12" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}/notes");
        var note = list.EnumerateArray().Single();
        note.GetProperty("body").GetString().Should().Be("Great opening chapter.");
        note.GetProperty("locationInBook").GetString().Should().Be("p. 12");
    }

    [Fact]
    public async Task Updating_a_note_changes_its_body()
    {
        var client = await AuthenticatedClientAsync("note-update@test.local");
        var itemId = await CreateLibraryItemAsync(client);
        var noteId = await CreateNoteAsync(client, itemId);

        var updateResponse = await client.PutJsonAsync($"/api/v1/notes/{noteId}", new { body = "Revised note.", locationInBook = (string?)null });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}/notes");
        list.EnumerateArray().Single().GetProperty("body").GetString().Should().Be("Revised note.");
    }

    [Fact]
    public async Task Deleting_a_note_removes_it_from_the_list()
    {
        var client = await AuthenticatedClientAsync("note-delete@test.local");
        var itemId = await CreateLibraryItemAsync(client);
        var noteId = await CreateNoteAsync(client, itemId);

        var deleteResponse = await client.DeleteWithAntiforgeryAsync($"/api/v1/notes/{noteId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}/notes");
        list.EnumerateArray().Should().BeEmpty();
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateLibraryItemAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Note Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateNoteAsync(HttpClient client, Guid libraryItemId)
    {
        var response = await client.PostJsonAsync($"/api/v1/library-items/{libraryItemId}/notes", new { body = "Original note.", locationInBook = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var note = await response.Content.ReadFromJsonAsync<JsonElement>();
        return note.GetProperty("id").GetGuid();
    }
}
