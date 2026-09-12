using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Genres;

public sealed class GenreEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Renaming_a_genre_is_reflected_on_the_list_and_on_works_that_use_it()
    {
        var client = await AuthenticatedClientAsync("genre-rename@test.local");
        var workId = await CreateWorkWithGenreAsync(client, "??????", "Rename Target Book");

        var genreId = await GetGenreIdByNameAsync(client, "??????");
        var putResponse = await client.PutJsonAsync($"/api/v1/genres/{genreId}", new { name = "Психология" });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var genres = await client.GetFromJsonAsync<JsonElement>("/api/v1/genres");
        genres.EnumerateArray().Should().ContainSingle(g => g.GetProperty("name").GetString() == "Психология");

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        work.GetProperty("genreNames").EnumerateArray().Should().ContainSingle(g => g.GetString() == "Психология");
    }

    [Fact]
    public async Task Renaming_a_genre_can_set_an_english_translation()
    {
        var client = await AuthenticatedClientAsync("genre-rename-en@test.local");
        await CreateWorkWithGenreAsync(client, "Научна литература", "Bilingual Genre Book");

        var genreId = await GetGenreIdByNameAsync(client, "Научна литература");
        var putResponse = await client.PutJsonAsync($"/api/v1/genres/{genreId}", new { name = "Научна литература", nameEn = "Non-fiction" });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var genres = await client.GetFromJsonAsync<JsonElement>("/api/v1/genres");
        var genre = genres.EnumerateArray().Single(g => g.GetProperty("id").GetGuid() == genreId);
        genre.GetProperty("name").GetString().Should().Be("Научна литература");
        genre.GetProperty("nameEn").GetString().Should().Be("Non-fiction");
    }

    [Fact]
    public async Task Renaming_a_genre_without_an_english_translation_clears_any_previous_one()
    {
        var client = await AuthenticatedClientAsync("genre-rename-clear-en@test.local");
        await CreateWorkWithGenreAsync(client, "Политика", "Clear English Name Book");
        var genreId = await GetGenreIdByNameAsync(client, "Политика");

        await client.PutJsonAsync($"/api/v1/genres/{genreId}", new { name = "Политика", nameEn = "Politics" });
        await client.PutJsonAsync($"/api/v1/genres/{genreId}", new { name = "Политика" });

        var genres = await client.GetFromJsonAsync<JsonElement>("/api/v1/genres");
        var genre = genres.EnumerateArray().Single(g => g.GetProperty("id").GetGuid() == genreId);
        genre.GetProperty("nameEn").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Deleting_a_genre_detaches_it_from_every_work_that_used_it()
    {
        var client = await AuthenticatedClientAsync("genre-delete@test.local");
        var workId = await CreateWorkWithGenreAsync(client, "Temp Category", "Delete Target Book");

        var genreId = await GetGenreIdByNameAsync(client, "Temp Category");
        var deleteResponse = await client.DeleteWithAntiforgeryAsync($"/api/v1/genres/{genreId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var genres = await client.GetFromJsonAsync<JsonElement>("/api/v1/genres");
        genres.EnumerateArray().Should().NotContain(g => g.GetProperty("name").GetString() == "Temp Category");

        var work = await client.GetFromJsonAsync<JsonElement>($"/api/v1/works/{workId}");
        work.GetProperty("genreNames").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_an_unknown_genre_returns_not_found()
    {
        var client = await AuthenticatedClientAsync("genre-delete-missing@test.local");

        var response = await client.DeleteWithAntiforgeryAsync($"/api/v1/genres/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateWorkWithGenreAsync(HttpClient client, string genreName, string title)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title, authorNames = new[] { "Test Author" }, genreNames = new[] { genreName } },
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

    private static async Task<Guid> GetGenreIdByNameAsync(HttpClient client, string name)
    {
        var genres = await client.GetFromJsonAsync<JsonElement>("/api/v1/genres");
        return genres.EnumerateArray().Single(g => g.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
    }
}
