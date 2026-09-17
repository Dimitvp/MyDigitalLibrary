using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
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
using MyDigitalLibrary.Application.Search;
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

// Persisted to a volume (not the container filesystem) so a redeploy/restart
// doesn't rotate the key ring and silently invalidate every signed-in cookie.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysDirectory"] ?? "keys"));

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

// Defense-in-depth alongside Identity's own per-account lockout (see the
// login handler in AuthEndpoints): caps *attempts*, not accounts, so it
// also blunts a spray of guesses across many different emails. Keyed by
// remote IP, which is only correct once ForwardedHeaders below has run —
// order matters, this must stay after that middleware in the pipeline.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));
});

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
builder.Services.AddScoped<FollowedBookSourceService>();
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<ReadingGoalService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<DuplicateDetectionService>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<SearchService>();

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

// Must run before anything that reads Request.Scheme/RemoteIpAddress
// (HttpsRedirection, the cookie's SecurePolicy=SameAsRequest, rate limiting
// below). Behind the host-level nginx reverse proxy this sits behind in
// production, the origin only ever sees plain HTTP with the real
// scheme/client IP carried in X-Forwarded-Proto/X-Forwarded-For — without
// this, every cookie would be issued without the Secure flag and every
// request would take a pointless extra HTTPS-redirect round trip.
// KnownNetworks/KnownProxies are cleared (trust the forwarded headers from
// any peer) rather than left at their loopback-only default: Docker's
// port-publishing NATs the proxy's connection to the bridge gateway IP, not
// literal loopback, so the default wouldn't recognize it as trusted — safe
// here specifically because the API port is bound to 127.0.0.1 in
// production, so nginx is the only thing that can ever connect at all.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownIPNetworks = { },
    KnownProxies = { },
});

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Runs in every environment, not just Development: a single-container app
// with no other migration/seed path, so this is how schema and the one
// admin account come to exist at all in production. Safe as auto-migrate
// only because this is a single instance, never horizontally scaled.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyDigitalLibraryDbContext>();
    await db.Database.MigrateAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
    var adminUserId = await IdentitySeeder.SeedAdminUserAsync(userManager, app.Configuration, seedLogger);

    // Demo/placeholder books — Development only, never in production.
    if (app.Environment.IsDevelopment() && adminUserId is { } userId)
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
app.MapGenreEndpoints();
app.MapEditionEndpoints();
app.MapLibraryItemEndpoints();
app.MapWishlistEndpoints();
app.MapImportEndpoints();
app.MapReadingSessionEndpoints();
app.MapShelfEndpoints();
app.MapNoteEndpoints();
app.MapQuoteEndpoints();
app.MapBookstoreListingEndpoints();
app.MapFollowedBookSourceEndpoints();
app.MapLoanEndpoints();
app.MapReadingGoalEndpoints();
app.MapExportEndpoints();
app.MapStatisticsEndpoints();
app.MapDuplicateEndpoints();
app.MapCsvImportEndpoints();
app.MapSearchEndpoints();

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
