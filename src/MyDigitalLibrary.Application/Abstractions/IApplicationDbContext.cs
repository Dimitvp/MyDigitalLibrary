using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.Abstractions;

/// <summary>
/// The port Application code writes queries/changes against; Infrastructure's
/// EF Core DbContext is the adapter. Keeps Application from referencing
/// Infrastructure directly, per the Domain/Application/Infrastructure/Api rule.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Work> Works { get; }
    DbSet<Edition> Editions { get; }
    DbSet<Author> Authors { get; }
    DbSet<Series> Series { get; }
    DbSet<LibraryItem> LibraryItems { get; }
    DbSet<WishlistEntry> WishlistEntries { get; }
    DbSet<Shelf> Shelves { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
