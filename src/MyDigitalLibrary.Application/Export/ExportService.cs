using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;

namespace MyDigitalLibrary.Application.Export;

/// <summary>Plan section 4 — GET /api/v1/export: the whole archive, denormalized, so it's readable without a second export to resolve foreign keys ("без vendor lock-in").</summary>
public sealed class ExportService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<ExportArchive> BuildArchiveAsync(Guid userId, CancellationToken ct)
    {
        var libraryItemRows = await db.LibraryItems.AsNoTracking()
            .Where(li => li.UserId == userId)
            .Select(li => new
            {
                li.Id,
                li.EditionId,
                li.Format,
                li.Status,
                li.PersonalNote,
                AcquiredOn = li.Acquisition.AcquiredOn,
                Method = li.Acquisition.Method,
                PriceAmount = li.Acquisition.Price == null ? (decimal?)null : li.Acquisition.Price.Amount,
                PriceCurrency = li.Acquisition.Price == null ? null : li.Acquisition.Price.CurrencyCode,
                Source = li.Acquisition.Source,
            })
            .ToListAsync(ct);

        var editionIds = libraryItemRows.Select(li => li.EditionId).Distinct().ToList();

        // Isbn13 is a scalar HasConversion type (like AudioDuration) — its .Value
        // can't be unwrapped inside Select(); fetch the whole object, unwrap after.
        var editionRows = await db.Editions.AsNoTracking()
            .Where(e => editionIds.Contains(e.Id))
            .Select(e => new { e.Id, e.WorkId, e.Isbn13 })
            .ToListAsync(ct);
        var isbnByEdition = editionRows.ToDictionary(e => e.Id, e => e.Isbn13?.Value);
        var workIdByEdition = editionRows.ToDictionary(e => e.Id, e => e.WorkId);

        var editionDisplayInfo = await catalog.GetEditionDisplayInfoAsync(editionIds, ct);

        var workIds = workIdByEdition.Values.Distinct().ToList();
        var ratingsByWork = await db.WorkRatings.AsNoTracking()
            .Where(r => r.UserId == userId && workIds.Contains(r.WorkId))
            .ToDictionaryAsync(r => r.WorkId, r => (int?)r.Score, ct);
        var reviewsByWork = await db.Reviews.AsNoTracking()
            .Where(r => r.UserId == userId && workIds.Contains(r.WorkId))
            .ToDictionaryAsync(r => r.WorkId, r => r.Text, ct);

        var exportLibraryItems = libraryItemRows.Select(li =>
        {
            var info = editionDisplayInfo.GetValueOrDefault(li.EditionId);
            var workId = workIdByEdition.GetValueOrDefault(li.EditionId);

            return new ExportLibraryItem(
                li.Id, info?.WorkTitle ?? "?", info?.AuthorNames ?? [], isbnByEdition.GetValueOrDefault(li.EditionId),
                li.Format.ToString(), li.Status.ToString(), li.AcquiredOn, li.Method.ToString(),
                li.PriceAmount, li.PriceCurrency, li.Source, li.PersonalNote,
                ratingsByWork.GetValueOrDefault(workId), reviewsByWork.GetValueOrDefault(workId));
        }).ToList();

        // Shelves/Notes/Loans/ReadingSessions all reference the user's own
        // LibraryItems by id — this one dictionary covers every one of them.
        var itemTitleById = libraryItemRows.ToDictionary(
            li => li.Id, li => editionDisplayInfo.GetValueOrDefault(li.EditionId)?.WorkTitle ?? "?");

        var wishlistEntries = await db.WishlistEntries.AsNoTracking().Where(w => w.UserId == userId).ToListAsync(ct);
        var wishlistWorkInfo = await catalog.GetWorkDisplayInfoAsync(wishlistEntries.Select(w => w.WorkId), ct);
        var exportWishlist = wishlistEntries.Select(w => new ExportWishlistEntry(
            w.Id, wishlistWorkInfo.GetValueOrDefault(w.WorkId)?.Title ?? "?", wishlistWorkInfo.GetValueOrDefault(w.WorkId)?.AuthorNames ?? [],
            w.DesiredFormat.ToString(), w.Priority, w.AddedOn, w.IsFulfilled)).ToList();

        var shelfRows = await db.Shelves.AsNoTracking().Where(s => s.UserId == userId)
            .Select(s => new { s.Name, ItemIds = s.Items.Select(i => i.LibraryItemId).ToList() })
            .ToListAsync(ct);
        var exportShelves = shelfRows
            .Select(s => new ExportShelf(s.Name, s.ItemIds.Select(id => itemTitleById.GetValueOrDefault(id, "?")).ToList()))
            .ToList();

        var notes = await db.Notes.AsNoTracking().Where(n => n.UserId == userId).ToListAsync(ct);
        var exportNotes = notes
            .Select(n => new ExportNote(itemTitleById.GetValueOrDefault(n.LibraryItemId, "?"), n.Body, n.LocationInBook, n.CreatedAt))
            .ToList();

        var quotes = await db.Quotes.AsNoTracking().Where(q => q.UserId == userId).ToListAsync(ct);
        var quoteWorkInfo = await catalog.GetWorkDisplayInfoAsync(quotes.Select(q => q.WorkId), ct);
        var exportQuotes = quotes
            .Select(q => new ExportQuote(quoteWorkInfo.GetValueOrDefault(q.WorkId)?.Title ?? "?", q.Text, q.PageOrPosition, q.CreatedAt))
            .ToList();

        var loans = await db.Loans.AsNoTracking().Where(l => l.UserId == userId).ToListAsync(ct);
        var exportLoans = loans
            .Select(l => new ExportLoan(itemTitleById.GetValueOrDefault(l.LibraryItemId, "?"), l.BorrowerName, l.LentOn, l.DueOn, l.ReturnedOn))
            .ToList();

        var sessions = await db.ReadingSessions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        var exportSessions = sessions
            .Select(s => new ExportReadingSession(itemTitleById.GetValueOrDefault(s.LibraryItemId, "?"), s.Format.ToString(), s.StartedOn, s.Status.ToString(), s.EndedOn))
            .ToList();

        var goals = await db.ReadingGoals.AsNoTracking().Where(g => g.UserId == userId).ToListAsync(ct);
        var exportGoals = goals.Select(g => new ExportReadingGoal(g.Year, g.TargetBooks, g.TargetPages)).ToList();

        return new ExportArchive(
            DateTimeOffset.UtcNow, exportLibraryItems, exportWishlist, exportShelves,
            exportNotes, exportQuotes, exportLoans, exportSessions, exportGoals);
    }
}
