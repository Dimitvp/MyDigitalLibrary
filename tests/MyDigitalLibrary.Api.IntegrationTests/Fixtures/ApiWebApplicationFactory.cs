using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace MyDigitalLibrary.Api.IntegrationTests.Fixtures;

/// <summary>
/// Runs the real Api host against a real, disposable Postgres container (plan
/// section 1: Testcontainers) rather than a fake/in-memory provider — the
/// point of these tests is to prove the actual deployed stack (EF Core global
/// query filters, Identity, antiforgery) behaves correctly together.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("mydigitallibrary_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // Program.cs only migrates/seeds in Development.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MyDigitalLibrary"] = _postgres.GetConnectionString(),
                // Deliberately unset: tests create exactly the users they need
                // via UserManager, so the fixed dev-admin seed stays out of the way.
                ["SEED_ADMIN_EMAIL"] = null,
                ["SEED_ADMIN_PASSWORD"] = null,
            });
        });
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }
}
