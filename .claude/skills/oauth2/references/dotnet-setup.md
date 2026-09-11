# .NET OAuth 2.0 Setup — Reference (Duende IdentityServer)

## IdentityServer Bootstrap (Program.cs)

```csharp
// Install: dotnet add package Duende.IdentityServer.AspNetIdentity
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents       = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents     = true;
    options.Events.RaiseSuccessEvents     = true;
    options.IssuerUri = builder.Configuration["IdentityServer:IssuerUri"];
})
.AddInMemoryIdentityResources(Config.IdentityResources)  // dev only
.AddInMemoryApiScopes(Config.ApiScopes)                  // dev only
.AddInMemoryClients(Config.Clients)                      // dev only
.AddAspNetIdentity<ApplicationUser>()
.AddProfileService<CustomProfileService>();              // custom claims

// In production — use EF Core stores:
// .AddConfigurationStore(opts => opts.ConfigureDbContext = b => b.UseSqlServer(...))
// .AddOperationalStore(opts => opts.ConfigureDbContext = b => b.UseSqlServer(...))
```

---

## Resources and Scopes Configuration

```csharp
public static class Config
{
    // Identity resources — control what goes in the ID token
    public static IEnumerable<IdentityResource> IdentityResources => new[]
    {
        new IdentityResources.OpenId(),   // sub claim — required
        new IdentityResources.Profile(),  // name, family_name, etc.
        new IdentityResources.Email(),    // email, email_verified
    };

    // API scopes — permissions a client can request for an API
    public static IEnumerable<ApiScope> ApiScopes => new[]
    {
        new ApiScope("products.read",   "Read product catalogue"),
        new ApiScope("products.write",  "Create and update products"),
        new ApiScope("orders.place",    "Place orders"),
        new ApiScope("orders.manage",   "Manage all orders"),
        new ApiScope("reports.view",    "View analytics reports"),
    };

    // API resource — groups scopes under a logical API + validates audience
    public static IEnumerable<ApiResource> ApiResources => new[]
    {
        new ApiResource("mycompany-api", "My Company API")
        {
            Scopes = { "products.read", "products.write", "orders.place", "orders.manage" }
        }
    };
}
```

---

## Protecting an ASP.NET Core API

```csharp
// Install: dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

// Program.cs — Resource Server (your .NET API)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];  // IdentityServer URL
        options.Audience  = "mycompany-api";                           // must match ApiResource name

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(opts =>
{
    // Policy requiring specific scope
    opts.AddPolicy("ReadProducts",  p => p.RequireAuthenticatedUser()
                                          .RequireClaim("scope", "products.read"));
    opts.AddPolicy("WriteProducts", p => p.RequireAuthenticatedUser()
                                          .RequireClaim("scope", "products.write"));
    opts.AddPolicy("PlaceOrders",   p => p.RequireAuthenticatedUser()
                                          .RequireClaim("scope", "orders.place"));

    // Default policy — all endpoints require auth unless [AllowAnonymous]
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

app.UseAuthentication();
app.UseAuthorization();
```

---

## Custom Claims via IProfileService

```csharp
public class CustomProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomProfileService(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var user = await _userManager.GetUserAsync(context.Subject);
        if (user is null) return;

        // Add claims to access token
        if (context.Caller == IdentityServerConstants.ProfileDataCallers.ClaimsProviderAccessToken)
        {
            context.IssuedClaims.Add(new Claim("tenant_id", user.TenantId));
            var roles = await _userManager.GetRolesAsync(user);
            context.IssuedClaims.AddRange(roles.Select(r => new Claim("role", r)));
        }

        // Add claims to ID token
        context.IssuedClaims.Add(new Claim("preferred_username", user.UserName ?? ""));
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var user = await _userManager.GetUserAsync(context.Subject);
        context.IsActive = user is { IsEnabled: true };
    }
}
```

---

## EF Core Persistence (Production Setup)

```csharp
// Install: dotnet add package Duende.IdentityServer.EntityFramework

builder.Services.AddIdentityServer()
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseSqlServer(builder.Configuration.GetConnectionString("IdentityServer"),
                sql => sql.MigrationsAssembly(migrationsAssembly));
    })
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseSqlServer(builder.Configuration.GetConnectionString("IdentityServer"),
                sql => sql.MigrationsAssembly(migrationsAssembly));

        // Automatically clean up expired tokens
        options.EnableTokenCleanup    = true;
        options.TokenCleanupInterval  = 3600;  // every hour
    });

// Apply migrations at startup
public static void InitializeDatabase(IApplicationBuilder app)
{
    using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()!.CreateScope();
    serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
    var configContext = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
    configContext.Database.Migrate();
    // Seed clients and resources if not already present
}
```

---

## Key Management (Production)

```csharp
// ❌ Development only — auto-generated in-memory keys
.AddDeveloperSigningCredential()

// ✅ Production — automatic key rotation via data protection
builder.Services.AddIdentityServer()
    .AddSigningKeyManagement(options =>
    {
        options.Licensee = "YOUR_LICENSE";
        // Keys stored automatically, rotated per schedule
    });

// ✅ Or explicit RSA key from Azure Key Vault
builder.Services.AddIdentityServer()
    .AddSigningCredential(LoadKeyFromVault());
```
