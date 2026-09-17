using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Auth;

public sealed class AuthEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "Password123!";

    [Fact]
    public async Task Login_locks_the_account_out_after_repeated_wrong_passwords()
    {
        var email = "lockout-test@test.local";
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email, Password);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Identity's default lockout threshold is 5 failed attempts.
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword!" });
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // The account is now locked out — even the *correct* password must be rejected.
        var finalAttempt = await client.PostJsonAsync("/api/v1/auth/login", new { email, password = Password });
        finalAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials_and_sets_the_session_cookie()
    {
        var email = "login-success-test@test.local";
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email, Password);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var response = await client.PostJsonAsync("/api/v1/auth/login", new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
