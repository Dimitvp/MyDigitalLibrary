using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Application.Statistics;

/// <summary>GET /api/v1/statistics?year=2026 (plan section 4) — the endpoint signature is all the plan specifies; the payload shape below was designed from what a personal reading-tracker's stats screen would actually want.</summary>
public sealed class StatisticsService(IApplicationDbContext db)
{
    public async Task<StatisticsDto> GetAsync(int year, Guid userId, CancellationToken ct)
    {
        var libraryItems = await db.LibraryItems.AsNoTracking()
            .Where(li => li.UserId == userId)
            .Select(li => new { li.Format, li.Status, li.EditionId })
            .ToListAsync(ct);

        var byFormat = libraryItems.GroupBy(li => li.Format.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byStatus = libraryItems.GroupBy(li => li.Status.ToString()).ToDictionary(g => g.Key, g => g.Count());

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var finishedItemIds = await db.ReadingSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ReadingStatus.Finished && s.EndedOn >= yearStart && s.EndedOn <= yearEnd)
            .Select(s => s.LibraryItemId)
            .ToListAsync(ct);

        var editionIdsForFinished = await db.LibraryItems.AsNoTracking()
            .Where(li => finishedItemIds.Contains(li.Id))
            .Select(li => li.EditionId)
            .ToListAsync(ct);

        var pagesReadThisYear = await db.Editions.AsNoTracking()
            .Where(e => editionIdsForFinished.Contains(e.Id) && e.PageCount != null)
            .SumAsync(e => e.PageCount!.Value, ct);

        var currentlyReadingCount = await db.ReadingSessions.AsNoTracking()
            .CountAsync(s => s.UserId == userId && s.Status == ReadingStatus.Reading, ct);

        var ratings = await db.WorkRatings.AsNoTracking().Where(r => r.UserId == userId).Select(r => r.Score).ToListAsync(ct);
        double? averageRating = ratings.Count > 0 ? ratings.Average() : null;

        var topAuthors = await GetTopAuthorsAsync(libraryItems.Select(li => li.EditionId), ct);

        return new StatisticsDto(
            year, libraryItems.Count, byFormat, byStatus,
            finishedItemIds.Count, pagesReadThisYear, currentlyReadingCount,
            averageRating, topAuthors);
    }

    private async Task<IReadOnlyList<AuthorBookCount>> GetTopAuthorsAsync(IEnumerable<Guid> editionIds, CancellationToken ct)
    {
        var editionIdList = editionIds.Distinct().ToList();
        if (editionIdList.Count == 0)
            return [];

        var workIds = await db.Editions.AsNoTracking()
            .Where(e => editionIdList.Contains(e.Id))
            .Select(e => e.WorkId)
            .Distinct()
            .ToListAsync(ct);

        var authorIdsPerBook = await db.Works.AsNoTracking()
            .Where(w => workIds.Contains(w.Id))
            .SelectMany(w => w.Authors.Select(a => a.AuthorId))
            .ToListAsync(ct);

        var topAuthorIds = authorIdsPerBook
            .GroupBy(id => id)
            .Select(g => new { AuthorId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var authorNames = await db.Authors.AsNoTracking()
            .Where(a => topAuthorIds.Select(x => x.AuthorId).Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.FullName, ct);

        return topAuthorIds
            .Select(x => new AuthorBookCount(authorNames.GetValueOrDefault(x.AuthorId, "?"), x.Count))
            .ToList();
    }
}
