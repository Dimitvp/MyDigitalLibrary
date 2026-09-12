using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Application.Editions;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Works;

public sealed class WorkService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<PagedResult<WorkSummaryDto>> ListAsync(
        string? q, Guid? authorId, Guid? seriesId, int? page, int? pageSize, CancellationToken ct)
    {
        var (normalizedPage, normalizedPageSize) = PageRequest.Normalize(page, pageSize);

        var query = db.Works.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(w => w.Title.Contains(q));

        if (authorId is { } aId)
            query = query.Where(w => w.Authors.Any(a => a.AuthorId == aId));

        if (seriesId is { } sId)
            query = query.Where(w => w.SeriesId == sId);

        var totalCount = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(w => w.Title)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(w => new WorkRow(w.Id, w.Title, w.OriginalTitle, w.Description, w.FirstPublicationYear,
                w.SeriesId, w.PositionInSeries, w.Authors.Select(a => a.AuthorId).ToList(), w.GenreIds.ToList()))
            .ToListAsync(ct);

        var authorNames = await ResolveAuthorNamesAsync(rows, ct);

        var items = rows
            .Select(r => new WorkSummaryDto(
                r.Id, r.Title, r.OriginalTitle, r.FirstPublicationYear,
                r.AuthorIds.Select(id => authorNames.GetValueOrDefault(id, "?")).ToList()))
            .ToList();

        return new PagedResult<WorkSummaryDto>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<WorkDetailDto> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await Project(db.Works.AsNoTracking().Where(w => w.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("work.not_found", $"Work '{id}' was not found.");

        var authorNames = await ResolveAuthorNamesAsync([row], ct);

        var authors = row.AuthorIds
            .Select(authorId => new AuthorSummaryDto(authorId, authorNames.GetValueOrDefault(authorId, "?")))
            .ToList();

        var myRating = await db.WorkRatings.AsNoTracking()
            .Where(r => r.WorkId == id && r.UserId == userId)
            .Select(r => (int?)r.Score)
            .FirstOrDefaultAsync(ct);

        var myReview = await db.Reviews.AsNoTracking()
            .Where(r => r.WorkId == id && r.UserId == userId)
            .Select(r => r.Text)
            .FirstOrDefaultAsync(ct);

        var genreNames = await ResolveGenreNamesAsync(row.GenreIds, ct);

        return new WorkDetailDto(row.Id, row.Title, row.OriginalTitle, row.Description, row.FirstPublicationYear,
            row.SeriesId, row.PositionInSeries?.Value, authors, myRating, myReview,
            row.GenreIds.Select(gid => genreNames.GetValueOrDefault(gid, "?")).ToList());
    }

    /// <summary>Upsert by (UserId, WorkId) — one rating per user per work (plan section 7's unique index).</summary>
    public async Task RateAsync(Guid workId, UpsertRatingRequest request, Guid userId, CancellationToken ct)
    {
        var workExists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == workId, ct);
        if (!workExists)
            throw new NotFoundException("work.not_found", $"Work '{workId}' was not found.");

        var existing = await db.WorkRatings.FirstOrDefaultAsync(r => r.WorkId == workId && r.UserId == userId, ct);
        var ratedAt = DateTimeOffset.UtcNow;

        if (existing is null)
            db.WorkRatings.Add(new WorkRating(userId, workId, request.Score, ratedAt));
        else
            existing.ChangeScore(request.Score, ratedAt);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Upsert by (UserId, WorkId) — the last review replaces the previous one.
    /// Plan section 14 Q4 ("versioned review history, or does the latest overwrite?")
    /// is still an open question for the project owner; this implements the
    /// simpler no-history default rather than guessing at a more complex design.
    /// </summary>
    public async Task ReviewAsync(Guid workId, UpsertReviewRequest request, Guid userId, CancellationToken ct)
    {
        var workExists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == workId, ct);
        if (!workExists)
            throw new NotFoundException("work.not_found", $"Work '{workId}' was not found.");

        var existing = await db.Reviews.FirstOrDefaultAsync(r => r.WorkId == workId && r.UserId == userId, ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
            db.Reviews.Add(new Review(userId, workId, request.Text, now));
        else
            existing.UpdateText(request.Text, now);

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Guid id, UpdateWorkRequest request, CancellationToken ct)
    {
        var work = await db.Works.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("work.not_found", $"Work '{id}' was not found.");

        work.UpdateDetails(request.Title, request.OriginalTitle, request.Description, request.FirstPublicationYear);
        work.MarkFieldsOverridden(Work.Fields.Title, Work.Fields.OriginalTitle, Work.Fields.Description, Work.Fields.FirstPublicationYear);

        if (request.GenreNames is not null)
            await catalog.SetGenresAsync(work, request.GenreNames, ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EditionDto>> ListEditionsAsync(Guid workId, CancellationToken ct)
    {
        var exists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == workId, ct);
        if (!exists)
            throw new NotFoundException("work.not_found", $"Work '{workId}' was not found.");

        var rows = await EditionService.Project(db.Editions.AsNoTracking().Where(e => e.WorkId == workId)).ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<EditionDto> AddEditionAsync(Guid workId, CreateEditionRequest request, CancellationToken ct)
    {
        var exists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == workId, ct);
        if (!exists)
            throw new NotFoundException("work.not_found", $"Work '{workId}' was not found.");

        var edition = await catalog.CreateEditionAsync(workId, request, ct);
        await db.SaveChangesAsync(ct);

        return new EditionRow(
            edition.Id, edition.WorkId, edition.Format, edition.Isbn13, edition.Publisher, edition.Language,
            edition.Translator, edition.PublicationYear, edition.PageCount, edition.CoverType, edition.CoverImageUrl,
            edition.Narrator, edition.Duration).ToDto();
    }

    private async Task<Dictionary<Guid, string>> ResolveAuthorNamesAsync(IReadOnlyCollection<WorkRow> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(r => r.AuthorIds).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await db.Authors.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.FullName })
            .ToDictionaryAsync(a => a.Id, a => a.FullName, ct);
    }

    private static IQueryable<WorkRow> Project(IQueryable<Domain.Catalog.Work> source) => source
        .Select(w => new WorkRow(w.Id, w.Title, w.OriginalTitle, w.Description, w.FirstPublicationYear,
            w.SeriesId, w.PositionInSeries, w.Authors.Select(a => a.AuthorId).ToList(), w.GenreIds.ToList()));

    private async Task<Dictionary<Guid, string>> ResolveGenreNamesAsync(IEnumerable<Guid> genreIds, CancellationToken ct)
    {
        var ids = genreIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await db.Genres.AsNoTracking()
            .Where(g => ids.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);
    }
}

internal sealed record WorkRow(
    Guid Id,
    string Title,
    string? OriginalTitle,
    string? Description,
    int? FirstPublicationYear,
    Guid? SeriesId,
    SeriesPosition? PositionInSeries,
    List<Guid> AuthorIds,
    List<Guid> GenreIds);
