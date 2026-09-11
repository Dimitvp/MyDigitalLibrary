using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyDigitalLibrary.Api.IntegrationTests.Fixtures;

namespace MyDigitalLibrary.Api.IntegrationTests.Loans;

public sealed class LoanEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Lending_an_item_moves_its_status_to_lent_out()
    {
        var client = await AuthenticatedClientAsync("loan-lend@test.local");
        var itemId = await CreateLibraryItemAsync(client);

        var createResponse = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Ivan" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var item = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}");
        item.GetProperty("status").GetString().Should().Be("LentOut");
    }

    [Fact]
    public async Task Lending_an_already_lent_out_item_is_rejected_as_a_conflict()
    {
        var client = await AuthenticatedClientAsync("loan-double@test.local");
        var itemId = await CreateLibraryItemAsync(client);
        await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Ivan" });

        var second = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Maria" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("loan.already_lent_out");
    }

    [Fact]
    public async Task Returning_a_loan_moves_the_item_back_to_owned_and_shows_up_in_the_loan_history()
    {
        var client = await AuthenticatedClientAsync("loan-return@test.local");
        var itemId = await CreateLibraryItemAsync(client);
        var createResponse = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Ivan" });
        var loanId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var returnResponse = await client.PostJsonAsync($"/api/v1/loans/{loanId}/return", new { });

        returnResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var item = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}");
        item.GetProperty("status").GetString().Should().Be("Owned");

        var history = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}/loans");
        var loan = history.EnumerateArray().Single();
        loan.GetProperty("isReturned").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task After_a_returned_loan_the_item_can_be_lent_out_again()
    {
        var client = await AuthenticatedClientAsync("loan-relend@test.local");
        var itemId = await CreateLibraryItemAsync(client);
        var firstResponse = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Ivan" });
        var firstLoanId = (await firstResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostJsonAsync($"/api/v1/loans/{firstLoanId}/return", new { });

        var secondResponse = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Maria" });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library-items/{itemId}/loans");
        history.EnumerateArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task Returning_an_already_returned_loan_is_rejected()
    {
        var client = await AuthenticatedClientAsync("loan-double-return@test.local");
        var itemId = await CreateLibraryItemAsync(client);
        var createResponse = await client.PostJsonAsync($"/api/v1/library-items/{itemId}/loans", new { borrowerName = "Ivan" });
        var loanId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PostJsonAsync($"/api/v1/loans/{loanId}/return", new { });

        var second = await client.PostJsonAsync($"/api/v1/loans/{loanId}/return", new { });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        await AuthenticatedClientExtensions.CreateUserAsync(factory.Services, email);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> CreateLibraryItemAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/v1/library-items", new
        {
            work = new { title = "Loan Test Book", authorNames = new[] { "Test Author" } },
            edition = new { },
            format = "Physical",
            acquisition = new { acquiredOn = "2026-01-01", method = "Bought" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}
