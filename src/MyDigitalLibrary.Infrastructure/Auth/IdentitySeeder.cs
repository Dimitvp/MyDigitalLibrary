using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyDigitalLibrary.Infrastructure.Auth;

/// <summary>
/// Creates the single admin user from SEED_ADMIN_EMAIL/SEED_ADMIN_PASSWORD
/// (plan section 8) if it doesn't already exist — runs in every environment,
/// including production, since there is no self-registration endpoint and
/// this is the only way the one account this single-user app needs comes to
/// exist. The password is read from configuration/environment only — it is
/// never hard-coded or committed.
/// </summary>
public static class IdentitySeeder
{
    public static async Task<Guid?> SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct = default)
    {
        var email = configuration["SEED_ADMIN_EMAIL"];
        var password = configuration["SEED_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SEED_ADMIN_EMAIL/SEED_ADMIN_PASSWORD not configured — skipping admin user seed.");
            return null;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing.Id;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to seed admin user: {Errors}", errors);
            return null;
        }

        return user.Id;
    }
}
