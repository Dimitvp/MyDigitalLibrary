using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Persistence.Seed;

/// <summary>
/// Minimal Development-only seed data: 2 works, 3 editions, 2 library items,
/// all owned by <paramref name="userId"/> — the seeded admin user created by
/// <see cref="IdentitySeeder"/> (Stage 4 onward; nothing here creates users).
/// </summary>
public static class DevelopmentSeeder
{
    public static async Task SeedAsync(MyDigitalLibraryDbContext db, Guid userId, CancellationToken ct = default)
    {
        if (await db.Works.AnyAsync(ct))
            return;

        var dune = new Work("Dune", firstPublicationYear: 1965, description: "A duke's son leads desert warriors against the galactic empire.");
        var herbert = new Author("Frank Herbert", "Herbert, Frank");
        dune.AddAuthor(herbert.Id, WorkAuthorRole.Author);

        var dunePaperback = new Edition(dune.Id, BookFormat.Physical);
        dunePaperback.SetPublicationDetails("Ace Books", "en", null, 1990, 412);
        dunePaperback.SetCoverType(CoverType.Paperback);

        var duneEbook = new Edition(dune.Id, BookFormat.Ebook);
        duneEbook.SetPublicationDetails("Ace Books", "en", null, 2010, 412);

        var foundation = new Work("Foundation", firstPublicationYear: 1951);
        var asimov = new Author("Isaac Asimov", "Asimov, Isaac");
        foundation.AddAuthor(asimov.Id, WorkAuthorRole.Author);

        var foundationPaperback = new Edition(foundation.Id, BookFormat.Physical);
        foundationPaperback.SetPublicationDetails("Bantam Spectra", "en", null, 1991, 255);
        foundationPaperback.SetCoverType(CoverType.Paperback);

        var duneLibraryItem = new LibraryItem(
            userId,
            dunePaperback.Id,
            BookFormat.Physical,
            new Acquisition(new DateOnly(2020, 1, 15), AcquisitionMethod.Bought, new Money(14.99m, "USD"), "Local bookstore"));

        var foundationLibraryItem = new LibraryItem(
            userId,
            foundationPaperback.Id,
            BookFormat.Physical,
            new Acquisition(new DateOnly(2021, 6, 3), AcquisitionMethod.Bought, new Money(9.99m, "USD"), "Local bookstore"));

        db.Authors.AddRange(herbert, asimov);
        db.Works.AddRange(dune, foundation);
        db.Editions.AddRange(dunePaperback, duneEbook, foundationPaperback);
        db.LibraryItems.AddRange(duneLibraryItem, foundationLibraryItem);

        await db.SaveChangesAsync(ct);
    }
}
