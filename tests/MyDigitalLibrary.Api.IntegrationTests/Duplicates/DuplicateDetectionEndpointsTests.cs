using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Duplicates;

public sealed class DuplicateDetectionEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Two_works_with_the_same_title_are_reported_as_a_duplicate_group()
    {
        var client = await AuthenticatedClientAsync("dup-works@test.local");

        await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "The Duplicate Title", authorNames = new[] { "Author One" } },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        // A second, separately-created Work with the same title (composite
        // create never matches existing works by title — see BookCatalogService).
        await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "the duplicate title", authorNames = new[] { "Author Two" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });

        var report = await client.GetFromJsonAsync<JsonElement>("/api/v1/duplicates");

        var group = report.GetProperty("duplicateWorks").EnumerateArray()
            .Single(g => string.Equals(g.GetProperty("title").GetString(), "The Duplicate Title", StringComparison.OrdinalIgnoreCase));
        group.GetProperty("workIds").EnumerateArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task Owning_the_same_edition_and_format_twice_is_reported_as_a_duplicate_library_item_group()
    {
        var client = await AuthenticatedClientAsync("dup-items@test.local");

        var firstResponse = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Duplicate Copy Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        var editionId = (await firstResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("editionId").GetGuid();

        // Same edition, same format, attached via the editionId variant of the composite create.
        await client.PostJsonAsync("/api/v1/library-items", new
        {
            editionId,
            format = "Physical",
            acquisition = new { acquiredOn = "2026-02-01", method = "Gift" },
        });

        var report = await client.GetFromJsonAsync<JsonElement>("/api/v1/duplicates");

        var group = report.GetProperty("duplicateLibraryItems").EnumerateArray().Single();
        group.GetProperty("editionId").GetGuid().Should().Be(editionId);
        group.GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Owning_the_same_work_as_a_physical_copy_and_an_ebook_is_not_a_duplicate()
    {
        // Plan section 3.1: the same book as physical + ebook + audio is legitimate, not a duplicate.
        var client = await AuthenticatedClientAsync("dup-formats@test.local");

        var firstResponse = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Multi Format Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        var firstCreated = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var physicalEditionId = firstCreated.GetProperty("editionId").GetGuid();

        var edition = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{physicalEditionId}");
        var workId = edition.GetProperty("workId").GetGuid();

        // A second, distinct Edition (Ebook) of the same Work.
        var addEditionResponse = await client.PostJsonAsync($"/api/v1/works/{workId}/editions", new { format = "Ebook" });
        var ebookEditionId = (await addEditionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostJsonAsync("/api/v1/library-items", new
        {
            editionId = ebookEditionId,
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-02-01", method = "Downloaded" },
        });

        var report = await client.GetFromJsonAsync<JsonElement>("/api/v1/duplicates");

        report.GetProperty("duplicateLibraryItems").EnumerateArray()
            .Should().NotContain(g => g.GetProperty("editionId").GetGuid() == physicalEditionId || g.GetProperty("editionId").GetGuid() == ebookEditionId);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }
}
