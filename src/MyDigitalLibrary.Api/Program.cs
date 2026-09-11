using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MyDigitalLibrary.Api;
using MyDigitalLibrary.Api.Auth;
using MyDigitalLibrary.Api.Endpoints;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Bookstores;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Duplicates;
using MyDigitalLibrary.Application.Editions;
using MyDigitalLibrary.Application.Export;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Application.Import.Csv;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Application.Loans;
using MyDigitalLibrary.Application.Notes;
using MyDigitalLibrary.Application.Quotes;
using MyDigitalLibrary.Application.Reading;
using MyDigitalLibrary.Application.ReadingGoals;
using MyDigitalLibrary.Application.Shelves;
using MyDigitalLibrary.Application.Statistics;
using MyDigitalLibrary.Application.Wishlist;
using MyDigitalLibrary.Application.Works;
using MyDigitalLibrary.Infrastructure.Auth;
using MyDigitalLibrary.Infrastructure.Bookstores;
using MyDigitalLibrary.Infrastructure.Import;
using MyDigitalLibrary.Infrastructure.Import.Covers;
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
builder.Services.AddScoped<ReadingSessionService>();
builder.Services.AddScoped<ShelfService>();
builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddScoped<BookstoreListingService>();
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<ReadingGoalService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<DuplicateDetectionService>();
builder.Services.AddScoped<CsvImportService>();

// Plan section 5 — import by link/ISBN. Priority order = registration order
// (Open Library before Google Books, per plan section 5.3/5.4).
builder.Services.Configure<GoogleBooksOptions>(builder.Configuration.GetSection("GoogleBooks"));
builder.Services.Configure<CoverStorageOptions>(builder.Configuration.GetSection("Covers"));

// AddHttpClient<T> registers T itself via its own typed-client factory (so its
// HttpClient constructor parameter resolves correctly) — IBookLinkResolver/
// IBookMetadataProvider are then registered as factories that resolve *that*
// instance, not as a second, independently-constructed one (which would fail:
// HttpClient has no plain DI registration outside the typed-client system).
builder.Services.AddHttpClient<GenericIsbnPageResolver>(http =>
{
    http.Timeout = TimeSpan.FromSeconds(10);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("MyDigitalLibrary/1.0 (personal book catalog; contact via GitHub repo)");
}).AddStandardResilienceHandler();
builder.Services.AddScoped<IBookLinkResolver>(sp => sp.GetRequiredService<GenericIsbnPageResolver>());
builder.Services.AddScoped<CompositeBookLinkResolver>();

builder.Services.AddHttpClient<OpenLibraryProvider>(http =>
{
    http.BaseAddress = new Uri("https://openlibrary.org");
    http.Timeout = TimeSpan.FromSeconds(10);
    // Plan section 5.3: Open Library asks for a descriptive User-Agent with contact info.
    http.DefaultRequestHeaders.UserAgent.ParseAdd("MyDigitalLibrary/1.0 (personal book catalog; contact via GitHub repo)");
}).AddStandardResilienceHandler();
builder.Services.AddScoped<IBookMetadataProvider>(sp => sp.GetRequiredService<OpenLibraryProvider>());

builder.Services.AddHttpClient<GoogleBooksProvider>(http =>
{
    http.BaseAddress = new Uri("https://www.googleapis.com");
    http.Timeout = TimeSpan.FromSeconds(10);
}).AddStandardResilienceHandler();
builder.Services.AddScoped<IBookMetadataProvider>(sp => sp.GetRequiredService<GoogleBooksProvider>());

builder.Services.AddScoped<CompositeBookMetadataProvider>();
builder.Services.AddScoped<ImportLookupService>();

builder.Services.AddHttpClient("covers", http => http.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<CoverDownloadQueue>();
builder.Services.AddSingleton<ICoverDownloadQueue>(sp => sp.GetRequiredService<CoverDownloadQueue>());
builder.Services.AddScoped<ICoverStorage, LocalFileCoverStorage>();
builder.Services.AddHostedService<CoverDownloadBackgroundService>();

// Plan section 6 — bookstore availability. Bookstores:Enabled is the global
// kill switch (plan 6.2); per-adapter config lives at Bookstores:Adapters:{key}
// and is read directly here (not via IOptions) because each adapter needs its
// own named HttpClient, registered in this same loop.
builder.Services.Configure<BookstoresOptions>(builder.Configuration.GetSection("Bookstores"));

var bookstoreAdapterConfigs = builder.Configuration.GetSection("Bookstores:Adapters").Get<Dictionary<string, BookstoreAdapterOptions>>() ?? [];
foreach (var (adapterKey, adapterOptions) in bookstoreAdapterConfigs)
{
    builder.Services.AddHttpClient($"bookstore-{adapterKey}", http =>
    {
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MyDigitalLibrary/1.0 (personal book catalog; contact via GitHub repo)");
    }).AddStandardResilienceHandler();

    builder.Services.AddScoped<IBookstoreAdapter>(sp => new SchemaOrgBookstoreAdapter(
        adapterKey,
        sp.GetRequiredService<IHttpClientFactory>().CreateClient($"bookstore-{adapterKey}"),
        adapterOptions,
        sp.GetRequiredService<ILogger<SchemaOrgBookstoreAdapter>>()));
}

builder.Services.AddHostedService<AvailabilityRefreshService>();

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
app.MapImportEndpoints();
app.MapReadingSessionEndpoints();
app.MapShelfEndpoints();
app.MapNoteEndpoints();
app.MapQuoteEndpoints();
app.MapBookstoreListingEndpoints();
app.MapLoanEndpoints();
app.MapReadingGoalEndpoints();
app.MapExportEndpoints();
app.MapStatisticsEndpoints();
app.MapDuplicateEndpoints();
app.MapCsvImportEndpoints();

// Plan section 5.6: covers live on disk (a Docker volume in production), never
// in the database — served as plain static files under /covers.
var coversPath = app.Configuration["Covers:RootDirectory"] ?? "covers";
Directory.CreateDirectory(coversPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(coversPath)),
    RequestPath = "/covers",
});

app.Run();

public partial class Program;
