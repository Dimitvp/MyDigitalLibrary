using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.Reading;
using MyDigitalLibrary.Infrastructure.Auth;

namespace MyDigitalLibrary.Infrastructure.Persistence;

public sealed class MyDigitalLibraryDbContext(DbContextOptions<MyDigitalLibraryDbContext> options, ICurrentUser currentUser)
    : IdentityUserContext<ApplicationUser, Guid>(options), IApplicationDbContext
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
    public DbSet<FollowedBookSource> FollowedBookSources => Set<FollowedBookSource>();
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

    // The query filter below references THIS property (via `this`), not
    // `currentUser` directly. EF Core's model — and with it, the compiled
    // filter expression — is built once and cached per DbContextOptions, then
    // reused for every instance. A closure over the injected service would
    // freeze in whichever request happened to trigger the first model build.
    // A self-reference to `this.CurrentUserId` is special-cased by EF Core:
    // it gets rebound to the actual executing context instance per query.
    public Guid CurrentUserId => currentUser.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity's own tables (AspNetUsers, ...)

        // Identity hardcodes PascalCase table names via explicit ToTable() calls
        // above, which the snake_case naming convention leaves alone (it only
        // applies where no name was set explicitly) — override them here so the
        // whole schema stays consistent.
        modelBuilder.Entity<ApplicationUser>().ToTable("asp_net_users");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyDigitalLibraryDbContext).Assembly);

        // Plan section 8: every IUserOwned entity gets the same
        // `UserId == current user` filter, applied once here instead of
        // repeating it in each entity's IEntityTypeConfiguration. This is the
        // single most important security control in the project — it's what
        // stops one user's query from ever returning another user's rows.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IUserOwned).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var userIdAccess = Expression.Property(parameter, nameof(IUserOwned.UserId));
            var currentUserIdAccess = Expression.Property(Expression.Constant(this), nameof(CurrentUserId));
            var filter = Expression.Lambda(Expression.Equal(userIdAccess, currentUserIdAccess), parameter);

            entityType.SetQueryFilter(filter);
        }
    }
}
