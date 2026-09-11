using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Reading;

public sealed class ReadingSessionEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Starting_a_session_captures_the_library_items_format()
    {
        var client = await AuthenticatedClientAsync("reading-start@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);

        var response = await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        session.GetProperty("format").GetString().Should().Be("Ebook");
        session.GetProperty("status").GetString().Should().Be("Reading");
    }

    [Fact]
    public async Task Recording_percent_progress_on_an_ebook_session_succeeds()
    {
        var client = await AuthenticatedClientAsync("reading-progress@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);
        var sessionId = await StartSessionAsync(client, itemId);

        var response = await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/progress", new { percent = 42.5 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        var progress = session.GetProperty("progress").EnumerateArray().Single();
        progress.GetProperty("kind").GetString().Should().Be("percent");
        progress.GetProperty("percent").GetDecimal().Should().Be(42.5m);
    }

    [Fact]
    public async Task Recording_page_progress_on_an_ebook_session_is_also_valid()
    {
        // PageProgress.ValidFor is [Physical, Ebook] — this is the mismatch case's inverse control.
        var client = await AuthenticatedClientAsync("reading-page@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);
        var sessionId = await StartSessionAsync(client, itemId);

        var response = await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/progress", new { page = 120 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Recording_a_timestamp_point_on_an_ebook_session_is_rejected_as_a_format_mismatch()
    {
        var client = await AuthenticatedClientAsync("reading-mismatch@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);
        var sessionId = await StartSessionAsync(client, itemId);

        var response = await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/progress", new { positionMinutes = 30 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("reading_session.progress_format_mismatch");
    }

    [Fact]
    public async Task Recording_progress_with_no_value_is_rejected_as_a_bad_request()
    {
        var client = await AuthenticatedClientAsync("reading-empty@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);
        var sessionId = await StartSessionAsync(client, itemId);

        var response = await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/progress", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("reading_session.progress_value_required");
    }

    [Fact]
    public async Task Finishing_a_session_sets_status_and_ended_on()
    {
        var client = await AuthenticatedClientAsync("reading-finish@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);
        // startedOn is fixed explicitly so the endedOn below is deterministic
        // regardless of the real wall-clock date the test happens to run on —
        // StartedOn defaults to "today" when omitted (see StartSessionAsync).
        var startResponse = await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId, startedOn = "2020-01-01" });
        var sessionId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/finish", new { endedOn = "2020-02-01" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        session.GetProperty("status").GetString().Should().Be("Finished");
        session.GetProperty("endedOn").GetString().Should().Be("2020-02-01");
    }

    [Fact]
    public async Task Abandoning_a_session_records_the_reason()
    {
        var client = await AuthenticatedClientAsync("reading-abandon@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);
        var sessionId = await StartSessionAsync(client, itemId);

        var response = await client.PostJsonAsync($"/api/v1/reading-sessions/{sessionId}/abandon", new { reason = "Not for me" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        session.GetProperty("status").GetString().Should().Be("Abandoned");
        session.GetProperty("abandonReason").GetString().Should().Be("Not for me");
    }

    [Fact]
    public async Task Rereading_a_finished_session_creates_a_second_session_via_the_real_http_stack()
    {
        var client = await AuthenticatedClientAsync("reading-reread@test.local");
        var itemId = await CreateEbookLibraryItemAsync(client);

        var firstSessionId = await StartSessionAsync(client, itemId);
        await client.PostJsonAsync($"/api/v1/reading-sessions/{firstSessionId}/finish", new { });

        var secondResponse = await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId = itemId });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondSession = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        secondSession.GetProperty("id").GetGuid().Should().NotBe(firstSessionId);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reading-sessions?libraryItemId={itemId}");
        list.EnumerateArray().Should().HaveCount(2);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateEbookLibraryItemAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Reading Session Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> StartSessionAsync(HttpClient client, Guid libraryItemId)
    {
        var response = await client.PostJsonAsync("/api/v1/reading-sessions", new { libraryItemId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        return session.GetProperty("id").GetGuid();
    }
}
