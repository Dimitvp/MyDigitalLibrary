using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Infrastructure.Persistence;

public sealed class MyDigitalLibraryDbContext(DbContextOptions<MyDigitalLibraryDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    // Shared catalog (no UserId) — plan section 3.7.
    public DbSet<Work> Works => Set<Work>();
    public DbSet<Edition> Editions => Set<Edition>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Bookstore> Bookstores => Set<Bookstore>();
    public DbSet<BookstoreListing> BookstoreListings => Set<BookstoreListing>();

    // Per-user data — plan section 3.7.
    public DbSet<LibraryItem> LibraryItems => Set<LibraryItem>();
    public DbSet<WishlistEntry> WishlistEntries => Set<WishlistEntry>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<ReadingGoal> ReadingGoals => Set<ReadingGoal>();
    public DbSet<WorkRating> WorkRatings => Set<WorkRating>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ReadingSession> ReadingSessions => Set<ReadingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyDigitalLibraryDbContext).Assembly);
    }
}
