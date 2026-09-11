using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.ReadingGoals;

/// <summary>Not in plan section 4's endpoint list, but explicit Stage 9 scope ("цели за четене") — designed by the same REST conventions as every other resource there. Statistics (also Stage 9) is deliberately not folded in here — see the Stage 9 history entry.</summary>
public sealed class ReadingGoalService(IApplicationDbContext db)
{
    public async Task<ReadingGoalDto> GetAsync(int year, Guid userId, CancellationToken ct)
    {
        var goal = await db.ReadingGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Year == year && g.UserId == userId, ct);
        var (booksFinished, pagesRead) = await ComputeProgressAsync(year, userId, ct);

        return goal is null
            ? new ReadingGoalDto(Guid.Empty, year, null, null, booksFinished, pagesRead)
            : new ReadingGoalDto(goal.Id, goal.Year, goal.TargetBooks, goal.TargetPages, booksFinished, pagesRead);
    }

    public async Task<ReadingGoalDto> UpsertAsync(int year, UpsertReadingGoalRequest request, Guid userId, CancellationToken ct)
    {
        var goal = await db.ReadingGoals.FirstOrDefaultAsync(g => g.Year == year && g.UserId == userId, ct);

        if (goal is null)
        {
            goal = new ReadingGoal(userId, year, request.TargetBooks, request.TargetPages);
            db.ReadingGoals.Add(goal);
        }
        else
        {
            goal.UpdateTargets(request.TargetBooks, request.TargetPages);
        }

        await db.SaveChangesAsync(ct);

        var (booksFinished, pagesRead) = await ComputeProgressAsync(year, userId, ct);
        return new ReadingGoalDto(goal.Id, goal.Year, goal.TargetBooks, goal.TargetPages, booksFinished, pagesRead);
    }

    /// <summary>Books finished this year = ReadingSessions ended Finished with EndedOn in the year. Pages read approximates from each finished session's edition PageCount (audiobooks/ebooks without a page count simply don't add to the page total, but still count toward BooksFinished).</summary>
    private async Task<(int BooksFinished, int PagesRead)> ComputeProgressAsync(int year, Guid userId, CancellationToken ct)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var finishedItemIds = await db.ReadingSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ReadingStatus.Finished && s.EndedOn >= yearStart && s.EndedOn <= yearEnd)
            .Select(s => s.LibraryItemId)
            .ToListAsync(ct);

        var booksFinished = finishedItemIds.Count;

        var editionIds = await db.LibraryItems.AsNoTracking()
            .Where(li => finishedItemIds.Contains(li.Id))
            .Select(li => li.EditionId)
            .ToListAsync(ct);

        var pagesRead = await db.Editions.AsNoTracking()
            .Where(e => editionIds.Contains(e.Id) && e.PageCount != null)
            .SumAsync(e => e.PageCount!.Value, ct);

        return (booksFinished, pagesRead);
    }
}
