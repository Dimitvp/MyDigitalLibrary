using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Api;
using MyDigitalLibrary.Api.Auth;
using MyDigitalLibrary.Api.Endpoints;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Editions;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Application.Wishlist;
using MyDigitalLibrary.Application.Works;
using MyDigitalLibrary.Infrastructure.Auth;
using MyDigitalLibrary.Infrastructure.Persistence;
using MyDigitalLibrary.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<MyDigitalLibraryDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("MyDigitalLibrary"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<MyDigitalLibraryDbContext>());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Plan section 8: ASP.NET Core Identity + cookie auth, no roles (out of scope),
// no self-registration (Auth:AllowRegistration stays false — no endpoint reads
// it yet since there is nothing to register through in Stage 4).
builder.Services
    .AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<MyDigitalLibraryDbContext>()
    .AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        // This is an API, not a page app — redirecting to a login page makes
        // no sense here; return the status code instead.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// The default antiforgery config only checks a form field, which a pure-JSON
// API never sends. HeaderName turns on header-based validation instead — the
// client reads the token from GET /api/v1/auth/antiforgery and echoes it back
// in this header on every state-changing request.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

builder.Services.AddScoped<BookCatalogService>();
builder.Services.AddScoped<WorkService>();
builder.Services.AddScoped<EditionService>();
builder.Services.AddScoped<LibraryItemService>();
builder.Services.AddScoped<WishlistService>();

builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MyDigitalLibraryDbContext>();
    await db.Database.MigrateAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
    var adminUserId = await IdentitySeeder.SeedAdminUserAsync(userManager, app.Configuration, seedLogger);

    if (adminUserId is { } userId)
        await DevelopmentSeeder.SeedAsync(db, userId);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapAuthEndpoints();
app.MapWorkEndpoints();
app.MapEditionEndpoints();
app.MapLibraryItemEndpoints();
app.MapWishlistEndpoints();

app.Run();

public partial class Program;
