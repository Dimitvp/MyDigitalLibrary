using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Api;
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

builder.Services.AddScoped<ICurrentUser, FixedCurrentUser>();

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
    await DevelopmentSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapWorkEndpoints();
app.MapEditionEndpoints();
app.MapLibraryItemEndpoints();
app.MapWishlistEndpoints();

app.Run();
