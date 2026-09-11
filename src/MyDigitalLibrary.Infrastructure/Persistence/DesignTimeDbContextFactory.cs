using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MyDigitalLibrary.Application.Abstractions;

namespace MyDigitalLibrary.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without running the Api host.
/// The connection string here is only used at design time (schema generation);
/// the real one comes from configuration at runtime. ICurrentUser is only
/// referenced inside a query filter expression tree here — its value is never
/// actually read at design time, so any implementation works.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MyDigitalLibraryDbContext>
{
    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
    }

    public MyDigitalLibraryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MyDigitalLibraryDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=mydigitallibrary;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention();

        return new MyDigitalLibraryDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }
}
