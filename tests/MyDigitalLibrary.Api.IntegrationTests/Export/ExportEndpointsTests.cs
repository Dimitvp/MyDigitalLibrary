using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Export;

public sealed class ExportEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Json_export_includes_the_library_item_with_its_rating()
    {
        var client = await AuthenticatedClientAsync("export-json@test.local");
        var (itemId, workId) = await CreateLibraryItemWithWorkIdAsync(client);
        await client.PutJsonAsync($"/api/v1/works/{workId}/rating", new { score = 8 });

        var archive = await client.GetFromJsonAsync<JsonElement>("/api/v1/export?format=json");

        var item = archive.GetProperty("libraryItems").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == itemId);
        item.GetProperty("workTitle").GetString().Should().Be("Export Test Book");
        item.GetProperty("myRating").GetInt32().Should().Be(8);
    }

    [Fact]
    public async Task Json_export_defaults_when_format_is_omitted()
    {
        var client = await AuthenticatedClientAsync("export-default@test.local");

        var response = await client.GetAsync("/api/v1/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Csv_export_has_a_header_row_and_one_row_per_library_item()
    {
        var client = await AuthenticatedClientAsync("export-csv@test.local");
        await CreateLibraryItemWithWorkIdAsync(client);

        var response = await client.GetAsync("/api/v1/export?format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("Title,Authors,Isbn13");
        lines.Should().HaveCount(2, "one header row + one book row");
        lines[1].Should().Contain("Export Test Book");
    }

    [Fact]
    public async Task Csv_export_quotes_fields_containing_commas()
    {
        var client = await AuthenticatedClientAsync("export-csv-escape@test.local");
        var createResponse = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Title, With Comma", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync("/api/v1/export?format=csv");
        var csv = await response.Content.ReadAsStringAsync();

        csv.Should().Contain("\"Title, With Comma\"");
    }

    [Fact]
    public async Task A_fresh_user_gets_an_empty_archive()
    {
        var client = await AuthenticatedClientAsync("export-empty@test.local");

        var archive = await client.GetFromJsonAsync<JsonElement>("/api/v1/export?format=json");

        archive.GetProperty("libraryItems").EnumerateArray().Should().BeEmpty();
        archive.GetProperty("wishlist").EnumerateArray().Should().BeEmpty();
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<(Guid ItemId, Guid WorkId)> CreateLibraryItemWithWorkIdAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Export Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought", price = new { amount = 19.99, currencyCode = "BGN" } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = created.GetProperty("id").GetGuid();
        var editionId = created.GetProperty("editionId").GetGuid();

        var edition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}");
        return (itemId, edition.GetProperty("workId").GetGuid());
    }
}
