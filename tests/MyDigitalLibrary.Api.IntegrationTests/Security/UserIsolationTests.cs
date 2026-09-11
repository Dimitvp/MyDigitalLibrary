using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;
using MyDigitalLibrary.Infrastructure.Auth;

namespace MyDigitalLibrary.Api.IntegrationTests.Security;

/// <summary>
/// Plan section 8: "the most important security test in the project" — proves
/// user A's data is never visible to user B, end to end through the real HTTP
/// stack (cookie auth, antiforgery, the EF Core global query filter), not a
/// unit test of the filter expression in isolation.
/// </summary>
public sealed class UserIsolationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task User_B_cannot_see_or_list_user_A_library_item()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await CreateUserAsync(userManager, "user-a@isolation.test", "Password123!");
            await CreateUserAsync(userManager, "user-b@isolation.test", "Password123!");
        }

        var clientA = CreateClient();
        await LoginAsync(clientA, "user-a@isolation.test", "Password123!");

        var createResponse = await PostJsonAsync(clientA, "/api/v1/library-items", new
        {
            work = new { title = "Isolation Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Ebook",
            acquisition = new { acquiredOn = "2026-01-01", method = "Downloaded" },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = created.GetProperty("id").GetGuid();

        var clientB = CreateClient();
        await LoginAsync(clientB, "user-b@isolation.test", "Password123!");

        // Direct lookup by id: must look exactly like "doesn't exist", not
        // "exists but forbidden" — the filter makes B's query never see it.
        var directGet = await clientB.GetAsync($"/api/v1/library-items/{itemId}");
        directGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listB = await (await clientB.GetAsync("/api/v1/library-items")).Content.ReadFromJsonAsync<JsonElement>();
        listB.GetProperty("totalCount").GetInt32().Should().Be(0);

        // Control: A still sees their own item — proves this is isolation,
        // not a query filter that hides everything from everyone.
        var listA = await (await clientA.GetAsync("/api/v1/library-items")).Content.ReadFromJsonAsync<JsonElement>();
        listA.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue("user creation should succeed: {0}", string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/antiforgery");
        return response.GetProperty("token").GetString()!;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("X-XSRF-TOKEN", token);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body)
    {
        var token = await GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-XSRF-TOKEN", token);

        return await client.SendAsync(request);
    }
}
