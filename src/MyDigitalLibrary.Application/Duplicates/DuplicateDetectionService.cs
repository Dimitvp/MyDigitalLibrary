using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;

namespace MyDigitalLibrary.Application.Duplicates;

/// <summary>
/// Plan section 12's Stage 9 scope names "откриване на дубликати" (duplicate
/// discovery) with no endpoint contract or algorithm in section 4 — designed
/// from scratch. Two distinct kinds of duplicate, deliberately kept simple
/// (exact match, not fuzzy/similarity matching — that needs pg_trgm or
/// similar, unverified and out of scope for a first pass):
///
/// 1. Duplicate Works: two catalog Work rows with the same title after
///    trimming/case-folding — most likely from manual entry twice, or two
///    separate imports of the same book. Works are shared catalog data
///    (plan section 3.7), so this scans the whole catalog, not just this
///    user's books — same as GET /works already does.
/// 2. Duplicate LibraryItems: this user owns the *same Edition in the same
///    Format* more than once — genuinely redundant, unlike owning "Dune" as
///    both a paperback and an audiobook (explicitly not a duplicate per
///    plan section 3.1).
/// </summary>
public sealed class DuplicateDetectionService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<DuplicatesReportDto> FindAsync(Guid userId, CancellationToken ct)
    {
        var duplicateWorks = await FindDuplicateWorksAsync(ct);
        var duplicateLibraryItems = await FindDuplicateLibraryItemsAsync(userId, ct);

        return new DuplicatesReportDto(duplicateWorks, duplicateLibraryItems);
    }

    private async Task<IReadOnlyList<DuplicateWorkGroup>> FindDuplicateWorksAsync(CancellationToken ct)
    {
        var works = await db.Works.AsNoTracking().Select(w => new { w.Id, w.Title }).ToListAsync(ct);

        return works
            .GroupBy(w => w.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateWorkGroup(g.First().Title, g.Select(w => w.Id).ToList()))
            .ToList();
    }

    private async Task<IReadOnlyList<DuplicateLibraryItemGroup>> FindDuplicateLibraryItemsAsync(Guid userId, CancellationToken ct)
    {
        var items = await db.LibraryItems.AsNoTracking()
            .Where(li => li.UserId == userId)
            .Select(li => new { li.Id, li.EditionId, li.Format })
            .ToListAsync(ct);

        var groups = items.GroupBy(i => (i.EditionId, i.Format)).Where(g => g.Count() > 1).ToList();
        if (groups.Count == 0)
            return [];

        var displayInfo = await catalog.GetEditionDisplayInfoAsync(groups.Select(g => g.Key.EditionId), ct);

        return groups
            .Select(g => new DuplicateLibraryItemGroup(
                g.Key.EditionId, displayInfo.GetValueOrDefault(g.Key.EditionId)?.WorkTitle ?? "?",
                g.Count(), g.Select(i => i.Id).ToList()))
            .ToList();
    }
}
