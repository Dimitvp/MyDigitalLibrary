using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.LibraryItems;

public sealed class LibraryItemEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Listing_matches_q_against_author_name_as_well_as_title()
    {
        var client = await AuthenticatedClientAsync("list-author-search@test.local");
        await CreateLibraryItemAsync(client, "Neuromancer", ["William Gibson"]);
        await CreateLibraryItemAsync(client, "Foundation", ["Isaac Asimov"]);

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?q=Gibson");

        var items = results.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle(i => i.GetProperty("workTitle").GetString() == "Neuromancer");
    }

    [Fact]
    public async Task Listing_matches_q_case_insensitively_against_title_and_author()
    {
        var client = await AuthenticatedClientAsync("list-case-insensitive-search@test.local");
        await CreateLibraryItemAsync(client, "Когато тялото казва НЕ", ["Габор Мате"]);

        var byLowercasePrefixOfAuthor = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?q=" + Uri.EscapeDataString("габо"));
        var byUppercaseTitleWord = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?q=" + Uri.EscapeDataString("ТЯЛОТО"));

        byLowercasePrefixOfAuthor.GetProperty("items").EnumerateArray().Should().ContainSingle(i => i.GetProperty("workTitle").GetString() == "Когато тялото казва НЕ");
        byUppercaseTitleWord.GetProperty("items").EnumerateArray().Should().ContainSingle(i => i.GetProperty("workTitle").GetString() == "Когато тялото казва НЕ");
    }

    [Fact]
    public async Task Listing_does_not_match_q_against_the_middle_of_a_word()
    {
        var client = await AuthenticatedClientAsync("list-word-boundary-search@test.local");
        await CreateLibraryItemAsync(client, "Фермата на животните", ["George Orwell"]);
        await CreateLibraryItemAsync(client, "Когато тялото казва НЕ", ["Габор Мате"]);

        // "мат" sits inside "ферМАТа" but isn't the start of any word there —
        // it should only match the second book, via the author's surname.
        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?q=" + Uri.EscapeDataString("мат"));

        var titles = results.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("workTitle").GetString()).ToList();
        titles.Should().NotContain("Фермата на животните");
        titles.Should().Contain("Когато тялото казва НЕ");
    }

    [Fact]
    public async Task Listing_matches_q_against_isbn()
    {
        var client = await AuthenticatedClientAsync("list-isbn-search@test.local");
        await CreateLibraryItemAsync(client, "Dune", ["Frank Herbert"], isbn13: "9780441013593");
        await CreateLibraryItemAsync(client, "Foundation", ["Isaac Asimov"], isbn13: "9780553293357");

        var byIsbnWithDashes = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?q=" + Uri.EscapeDataString("978-0441-01359-3"));
        var byIsbnSuffix = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?q=013593");

        byIsbnWithDashes.GetProperty("items").EnumerateArray().Should().ContainSingle(i => i.GetProperty("workTitle").GetString() == "Dune");
        byIsbnSuffix.GetProperty("items").EnumerateArray().Should().ContainSingle(i => i.GetProperty("workTitle").GetString() == "Dune");
    }

    [Fact]
    public async Task Listing_can_be_filtered_by_genre()
    {
        var client = await AuthenticatedClientAsync("list-genre-filter@test.local");
        await CreateLibraryItemAsync(client, "Dune", ["Frank Herbert"], genreNames: ["Sci-Fi"]);
        await CreateLibraryItemAsync(client, "Emma", ["Jane Austen"], genreNames: ["Romance"]);

        var genres = await client.GetFromJsonAsync<JsonElement>("/api/v1/genres");
        var sciFiGenreId = genres.EnumerateArray().Single(g => g.GetProperty("name").GetString() == "Sci-Fi").GetProperty("id").GetGuid();

        var results = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items?genreId={sciFiGenreId}");

        var items = results.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle(i => i.GetProperty("workTitle").GetString() == "Dune");
    }

    [Fact]
    public async Task Listing_can_be_filtered_to_items_with_no_reading_session()
    {
        var client = await AuthenticatedClientAsync("list-notstarted-filter@test.local");
        var (_, unstartedItemId) = await CreateLibraryItemAsync(client, "Unstarted Book", ["Someone"]);
        var (_, startedItemId) = await CreateLibraryItemAsync(client, "Started Book", ["Someone Else"]);
        await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = startedItemId, startedOn = "2026-01-01" });

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?readingStatus=NotStarted");

        var titles = results.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("workTitle").GetString()).ToList();
        titles.Should().Contain("Unstarted Book");
        titles.Should().NotContain("Started Book");
    }

    [Fact]
    public async Task Listing_can_be_filtered_by_current_reading_status()
    {
        var client = await AuthenticatedClientAsync("list-reading-filter@test.local");
        var (_, itemId) = await CreateLibraryItemAsync(client, "In Progress Book", ["Someone"]);
        await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId, startedOn = "2026-01-01" });

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?readingStatus=Reading");

        var titles = results.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("workTitle").GetString()).ToList();
        titles.Should().Contain("In Progress Book");
    }

    [Fact]
    public async Task Listing_can_be_sorted_by_title_ascending()
    {
        var client = await AuthenticatedClientAsync("list-sort-title@test.local");
        await CreateLibraryItemAsync(client, "Zebra Book", ["Someone"]);
        await CreateLibraryItemAsync(client, "Apple Book", ["Someone"]);

        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?sortBy=title&sortDir=asc&pageSize=100");

        var titles = results.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("workTitle").GetString()).ToList();
        var indexOfApple = titles.IndexOf("Apple Book");
        var indexOfZebra = titles.IndexOf("Zebra Book");
        indexOfApple.Should().BeLessThan(indexOfZebra);
    }

    [Fact]
    public async Task ChangeFormat_updates_the_item_and_its_edition_and_clears_now_invalid_fields()
    {
        var client = await AuthenticatedClientAsync("change-format@test.local");
        var (_, itemId) = await CreateLibraryItemAsync(client, "Dune", ["Frank Herbert"], isbn13: "9780132350884");
        var item = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}");
        var editionId = item.GetProperty("editionId").GetGuid();

        var response = await client.SendPatchAsync($"/api/v1/library-items/{itemId}/format", new { format = "Audiobook" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updatedItem = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}");
        updatedItem.GetProperty("format").GetString().Should().Be("Audiobook");

        var updatedEdition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}");
        updatedEdition.GetProperty("format").GetString().Should().Be("Audiobook");
        updatedEdition.GetProperty("isbn13").ValueKind.Should().Be(JsonValueKind.Null, "ISBN only applies to physical/ebook editions");
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<(Guid WorkId, Guid LibraryItemId)> CreateLibraryItemAsync(
        HttpClient client, string title, string[] authorNames, string[]? genreNames = null, string? isbn13 = null)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title, authorNames, genreNames },
            edition = new { isbn13 },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var editionId = created.GetProperty("editionId").GetGuid();
        var libraryItemId = created.GetProperty("id").GetGuid();

        var edition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}");
        return (edition.GetProperty("workId").GetGuid(), libraryItemId);
    }
}
