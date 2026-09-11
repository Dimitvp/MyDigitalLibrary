using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MyDigitalLibrary.Infrastructure.Auth;

namespace MyDigitalLibrary.Api.IntegrationTests.Fixtures;

/// <summary>Shared HTTP test helpers (login, antiforgery-wrapped writes) for integration tests beyond Security/UserIsolationTests, which keeps its own copies rather than risk touching a passing security test.</summary>
internal static class AuthenticatedClientExtensions
{
    public static async Task<ApplicationUser> CreateUserAsync(IServiceProvider services, string email, string password = "Password123!")
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue("user creation should succeed: {0}", string.Join("; ", result.Errors.Select(e => e.Description)));

        return user;
    }

    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/antiforgery");
        return response.GetProperty("token").GetString()!;
    }

    public static async Task LoginAsync(this HttpClient client, string email, string password = "Password123!")
    {
        var token = await client.GetAntiforgeryTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-XSRF-TOKEN", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body)
        => client.SendWithAntiforgeryAsync(HttpMethod.Post, url, body);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body)
        => client.SendWithAntiforgeryAsync(HttpMethod.Put, url, body);

    public static Task<HttpResponseMessage> DeleteWithAntiforgeryAsync(this HttpClient client, string url)
        => client.SendWithAntiforgeryAsync(HttpMethod.Delete, url, null);

    private static async Task<HttpResponseMessage> SendWithAntiforgeryAsync(this HttpClient client, HttpMethod method, string url, object? body)
    {
        var token = await client.GetAntiforgeryTokenAsync();

        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        request.Headers.Add("X-XSRF-TOKEN", token);

        return await client.SendAsync(request);
    }
}
