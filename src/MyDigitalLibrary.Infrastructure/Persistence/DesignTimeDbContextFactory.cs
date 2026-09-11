using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyDigitalLibrary.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without running the Api host.
/// The connection string here is only used at design time (schema generation);
/// the real one comes from configuration at runtime.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MyDigitalLibraryDbContext>
{
    public MyDigitalLibraryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MyDigitalLibraryDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=mydigitallibrary;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention();

        return new MyDigitalLibraryDbContext(optionsBuilder.Options);
    }
}
