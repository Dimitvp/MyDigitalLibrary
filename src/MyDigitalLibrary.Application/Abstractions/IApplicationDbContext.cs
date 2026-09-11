using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.Reading;

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
    DbSet<Bookstore> Bookstores { get; }
    DbSet<BookstoreListing> BookstoreListings { get; }
    DbSet<LibraryItem> LibraryItems { get; }
    DbSet<WishlistEntry> WishlistEntries { get; }
    DbSet<Shelf> Shelves { get; }
    DbSet<Note> Notes { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<WorkRating> WorkRatings { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ReadingSession> ReadingSessions { get; }
    DbSet<Loan> Loans { get; }
    DbSet<ReadingGoal> ReadingGoals { get; }
    DbSet<ImportJob> ImportJobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
