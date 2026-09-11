using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

public sealed class CsvImportEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Importing_a_goodreads_csv_creates_library_items_and_reports_stats()
    {
        var client = await AuthenticatedClientAsync("csv-goodreads@test.local");
        var csv =
            "Book Id,Title,Author,ISBN13,My Rating,Binding,Number of Pages,Publisher,Year Published,Exclusive Shelf,My Review\n"
            + "1,Dune,Frank Herbert,\"=\"\"9780441013593\"\"\",5,Paperback,412,Ace Books,1990,read,Great book\n"
            + "2,Foundation,Isaac Asimov,,4,Hardcover,255,Bantam,1991,to-read,";

        var response = await client.PostFileAsync("/api/v1/import/csv", "goodreads.csv", csv);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var job = await response.Content.ReadFromJsonAsync<JsonElement>();
        job.GetProperty("status").GetString().Should().Be("Completed");
        job.GetProperty("totalRows").GetInt32().Should().Be(2);
        job.GetProperty("succeededRows").GetInt32().Should().Be(2);
        job.GetProperty("failedRows").GetInt32().Should().Be(0);

        var items = await client.GetFromJsonAsync<JsonElement>("/api/v1/library-items?pageSize=50");
        items.GetProperty("totalCount").GetInt32().Should().Be(1, "the 'to-read' row should become a wishlist entry, not a library item");
        var libraryItem = items.GetProperty("items").EnumerateArray().Single();
        libraryItem.GetProperty("workTitle").GetString().Should().Be("Dune");

        var wishlist = await client.GetFromJsonAsync<JsonElement>("/api/v1/wishlist");
        wishlist.EnumerateArray().Should().ContainSingle(w => w.GetProperty("workTitle").GetString() == "Foundation");
    }

    [Fact]
    public async Task A_row_with_a_bad_series_position_fails_without_aborting_the_rest_of_the_import()
    {
        var client = await AuthenticatedClientAsync("csv-partial-fail@test.local");
        var csv =
            "title,authors,series,series_index\n"
            + "Good Book,An Author,,\n"
            + "Bad Series Book,Another Author,Some Series,\n"; // series set but no series_index -> work.series_position_required

        var response = await client.PostFileAsync("/api/v1/import/csv", "calibre.csv", csv);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var job = await response.Content.ReadFromJsonAsync<JsonElement>();
        job.GetProperty("totalRows").GetInt32().Should().Be(2);
        job.GetProperty("succeededRows").GetInt32().Should().Be(1);
        job.GetProperty("failedRows").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Getting_a_completed_job_by_id_reflects_its_final_stats()
    {
        var client = await AuthenticatedClientAsync("csv-getjob@test.local");
        var csv = "title,authors\nSolo Book,An Author";

        var createResponse = await client.PostFileAsync("/api/v1/import/csv", "calibre.csv", csv);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("id").GetGuid();

        var job = await client.GetFromJsonAsync<JsonElement>($"/api/v1/import/jobs/{jobId}");

        job.GetProperty("status").GetString().Should().Be("Completed");
        job.GetProperty("succeededRows").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task An_unrecognized_csv_format_is_rejected()
    {
        var client = await AuthenticatedClientAsync("csv-unrecognized@test.local");
        var csv = "Name,Value\nfoo,bar";

        var response = await client.PostFileAsync("/api/v1/import/csv", "unknown.csv", csv);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("import_job.unrecognized_csv_format");
    }

    [Fact]
    public async Task Reimporting_the_same_isbn_reuses_the_existing_edition_instead_of_duplicating_it()
    {
        var client = await AuthenticatedClientAsync("csv-reimport@test.local");
        var csv = "title,authors,isbn\nDune,Frank Herbert,9780441013593";

        await client.PostFileAsync("/api/v1/import/csv", "calibre.csv", csv);
        var secondResponse = await client.PostFileAsync("/api/v1/import/csv", "calibre.csv", csv);

        var secondJob = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        secondJob.GetProperty("succeededRows").GetInt32().Should().Be(1, "a matching ISBN should be reused, not rejected");

        var report = await client.GetFromJsonAsync<JsonElement>("/api/v1/duplicates");
        report.GetProperty("duplicateLibraryItems").EnumerateArray().Should().ContainSingle(g => g.GetProperty("count").GetInt32() == 2);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }
}
