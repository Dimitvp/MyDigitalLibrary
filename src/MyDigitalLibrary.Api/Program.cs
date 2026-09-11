using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Infrastructure.Persistence;
using MyDigitalLibrary.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<MyDigitalLibraryDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("MyDigitalLibrary"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddHealthChecks();

var app = builder.Build();

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

app.Run();
