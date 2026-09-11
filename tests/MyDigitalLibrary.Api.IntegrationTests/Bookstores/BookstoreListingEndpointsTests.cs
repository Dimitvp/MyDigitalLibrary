using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Bookstores;

public sealed class BookstoreListingEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Adding_a_manual_listing_resolves_or_creates_the_bookstore_and_shows_it_on_a_subsequent_list()
    {
        var client = await AuthenticatedClientAsync("listing-add@test.local");
        var editionId = await CreateEditionAsync(client);

        var createResponse = await client.PostJsonAsync($"/api/v1/editions/{editionId}/listings", new
        {
            bookstoreName = "Test Bookstore",
            bookstoreBaseUrl = "https://example-bookstore.test",
            listingUrl = "https://example-bookstore.test/book/123",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}/listings");
        var listing = list.EnumerateArray().Single();
        listing.GetProperty("bookstoreName").GetString().Should().Be("Test Bookstore");
        listing.GetProperty("availability").GetString().Should().Be("Unknown");
    }

    [Fact]
    public async Task Adding_a_second_listing_for_the_same_bookstore_name_reuses_the_existing_bookstore()
    {
        var client = await AuthenticatedClientAsync("listing-reuse@test.local");
        var editionA = await CreateEditionAsync(client);
        var editionB = await CreateEditionAsync(client);

        await client.PostJsonAsync($"/api/v1/editions/{editionA}/listings", new
        {
            bookstoreName = "Shared Bookstore",
            bookstoreBaseUrl = "https://shared-bookstore.test",
            listingUrl = "https://shared-bookstore.test/book/a",
        });
        var secondResponse = await client.PostJsonAsync($"/api/v1/editions/{editionB}/listings", new
        {
            bookstoreName = "Shared Bookstore",
            bookstoreBaseUrl = "https://shared-bookstore.test",
            listingUrl = "https://shared-bookstore.test/book/b",
        });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var listingB = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionB}/listings")).EnumerateArray().Single();
        listingB.GetProperty("bookstoreName").GetString().Should().Be("Shared Bookstore");
    }

    [Fact]
    public async Task An_invalid_listing_url_is_rejected()
    {
        var client = await AuthenticatedClientAsync("listing-invalid@test.local");
        var editionId = await CreateEditionAsync(client);

        var response = await client.PostJsonAsync($"/api/v1/editions/{editionId}/listings", new
        {
            bookstoreName = "Test Bookstore",
            bookstoreBaseUrl = "https://example-bookstore.test",
            listingUrl = "not a url",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Marking_a_listing_discontinued_works_even_with_no_adapter_registered_for_it()
    {
        // Plan section 6.3 — the manual override always works, adapter or not.
        var client = await AuthenticatedClientAsync("listing-discontinued@test.local");
        var editionId = await CreateEditionAsync(client);

        var createResponse = await client.PostJsonAsync($"/api/v1/editions/{editionId}/listings", new
        {
            bookstoreName = "Test Bookstore",
            bookstoreBaseUrl = "https://example-bookstore.test",
            listingUrl = "https://example-bookstore.test/book/123",
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listingId = created.GetProperty("id").GetGuid();

        var patchResponse = await client.SendPatchAsync($"/api/v1/editions/{editionId}/listings/{listingId}/discontinued");
        patchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/editions/{editionId}/listings");
        list.EnumerateArray().Single().GetProperty("availability").GetString().Should().Be("Discontinued");
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateEditionAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = $"Listing Test Book {Guid.NewGuid()}", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("editionId").GetGuid();
    }
}
