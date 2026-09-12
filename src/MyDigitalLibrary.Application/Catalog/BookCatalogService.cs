using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Catalog;

/// <summary>
/// Shared write logic for the composite "create a book" flow (plan section
/// 4.1): resolving or creating a Work (with its authors/series by name) and
/// creating an Edition under it. Used by WorkService.AddEdition and by the
/// LibraryItem/Wishlist composite-create paths. Callers own SaveChangesAsync
/// so everything lands in one transaction.
/// </summary>
public sealed class BookCatalogService(IApplicationDbContext db, ICoverDownloadQueue coverDownloadQueue)
{
    public async Task<Work> ResolveOrCreateWorkAsync(CreateWorkRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AppValidationException("work.title_required", "Title is required to create a new work.");

        var work = new Work(request.Title.Trim(), request.OriginalTitle, request.Description, request.FirstPublicationYear);

        if (request.AuthorNames is { Count: > 0 })
        {
            foreach (var name in request.AuthorNames)
            {
                var author = await ResolveOrCreateAuthorAsync(name, ct);
                work.AddAuthor(author.Id, WorkAuthorRole.Author);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SeriesName))
        {
            if (request.SeriesPosition is null)
                throw new AppValidationException("work.series_position_required", "seriesPosition is required when seriesName is set.");

            var series = await ResolveOrCreateSeriesAsync(request.SeriesName, ct);
            work.AssignToSeries(series.Id, new SeriesPosition(request.SeriesPosition.Value));
        }

        if (request.GenreNames is { Count: > 0 })
        {
            foreach (var name in request.GenreNames)
            {
                var genre = await ResolveOrCreateGenreAsync(name, ct);
                work.AddGenre(genre.Id);
            }
        }

        db.Works.Add(work);
        return work;
    }

    /// <summary>Replaces a work's author list by name, creating any that don't exist yet — same resolve-or-create pattern as SetGenresAsync. Always the Author role; co-authors/translators aren't editable via this path yet.</summary>
    public async Task SetAuthorsAsync(Work work, IReadOnlyList<string>? authorNames, CancellationToken ct)
    {
        foreach (var existing in work.Authors.ToList())
            work.RemoveAuthor(existing.AuthorId, existing.Role);

        if (authorNames is not { Count: > 0 })
            return;

        foreach (var name in authorNames)
        {
            var author = await ResolveOrCreateAuthorAsync(name, ct);
            work.AddAuthor(author.Id, WorkAuthorRole.Author);
        }
    }

    /// <summary>Replaces a work's genre set by name, creating any that don't exist yet — same resolve-or-create pattern as authors/series.</summary>
    public async Task SetGenresAsync(Work work, IReadOnlyList<string>? genreNames, CancellationToken ct)
    {
        foreach (var existingId in work.GenreIds.ToList())
            work.RemoveGenre(existingId);

        if (genreNames is not { Count: > 0 })
            return;

        foreach (var name in genreNames)
        {
            var genre = await ResolveOrCreateGenreAsync(name, ct);
            work.AddGenre(genre.Id);
        }
    }

    public async Task<Edition> CreateEditionAsync(Guid workId, CreateEditionRequest request, CancellationToken ct)
    {
        Isbn? isbn = null;

        if (!string.IsNullOrWhiteSpace(request.Isbn13))
        {
            var isbnResult = Isbn.TryCreate(request.Isbn13);
            if (isbnResult.IsFailure)
                throw new AppValidationException(isbnResult.ErrorCode, $"'{request.Isbn13}' is not a valid ISBN.");

            isbn = isbnResult.Value;

            var existingId = await db.Editions.AsNoTracking()
                .Where(e => e.Isbn13 == isbn)
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(ct);

            if (existingId is { } duplicateId)
                throw new ConflictException(
                    "edition.duplicate",
                    $"An edition with ISBN {isbn.Value} already exists.",
                    new Dictionary<string, object?> { ["existingEditionId"] = duplicateId });
        }

        var edition = new Edition(workId, request.Format);
        edition.SetIsbn(isbn);
        edition.SetPublicationDetails(request.Publisher, request.Language, request.Translator, request.PublicationYear, request.PageCount);

        if (request.CoverType is { } coverType)
            edition.SetCoverType(coverType);

        if (request.Narrator is not null || request.DurationMinutes is not null)
        {
            var duration = request.DurationMinutes is { } minutes ? new AudioDuration(TimeSpan.FromMinutes(minutes)) : null;
            edition.SetAudioDetails(request.Narrator, duration);
        }

        db.Editions.Add(edition);

        // Plan 5.6: never fetch the cover synchronously — queue it, the book save must not wait on it.
        if (request.CoverUrl is { } coverUrl)
            coverDownloadQueue.Enqueue(edition.Id, coverUrl);

        return edition;
    }

    private async Task<Author> ResolveOrCreateAuthorAsync(string fullName, CancellationToken ct)
    {
        var trimmed = fullName.Trim();

        var existing = await db.Authors.FirstOrDefaultAsync(a => a.FullName == trimmed, ct);
        if (existing is not null)
            return existing;

        var author = new Author(trimmed, DeriveSortName(trimmed));
        db.Authors.Add(author);
        return author;
    }

    private async Task<Series> ResolveOrCreateSeriesAsync(string name, CancellationToken ct)
    {
        var trimmed = name.Trim();

        var existing = await db.Series.FirstOrDefaultAsync(s => s.Name == trimmed, ct);
        if (existing is not null)
            return existing;

        var series = new Series(trimmed);
        db.Series.Add(series);
        return series;
    }

    private async Task<Genre> ResolveOrCreateGenreAsync(string name, CancellationToken ct)
    {
        var trimmed = name.Trim();

        var existing = await db.Genres.FirstOrDefaultAsync(g => g.Name == trimmed, ct);
        if (existing is not null)
            return existing;

        var genre = new Genre(trimmed);
        db.Genres.Add(genre);
        return genre;
    }

    // "Frank Herbert" -> "Herbert, Frank". A naive last-token heuristic — good
    // enough for v1 manual entry, not meant to handle every naming convention.
    private static string DeriveSortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? fullName : $"{parts[^1]}, {string.Join(' ', parts[..^1])}";
    }

    /// <summary>
    /// Denormalized title/authors/cover for a batch of editions — list and
    /// detail screens need this (a LibraryItem only carries an EditionId; the
    /// domain deliberately has no navigation across aggregates to get there).
    /// </summary>
    public async Task<Dictionary<Guid, EditionDisplayInfo>> GetEditionDisplayInfoAsync(IEnumerable<Guid> editionIds, CancellationToken ct)
    {
        var ids = editionIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var editions = await db.Editions.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.WorkId, e.CoverImageUrl, e.Language })
            .ToListAsync(ct);

        var workInfo = await GetWorkDisplayInfoAsync(editions.Select(e => e.WorkId), ct);

        return editions.ToDictionary(
            e => e.Id,
            e => new EditionDisplayInfo(
                workInfo.TryGetValue(e.WorkId, out var w) ? w.Title : "?",
                workInfo.TryGetValue(e.WorkId, out var w2) ? w2.AuthorNames : [],
                e.CoverImageUrl?.ToString(),
                e.Language,
                workInfo.TryGetValue(e.WorkId, out var w3) ? w3.GenreNames : [],
                e.WorkId));
    }

    /// <summary>Denormalized title/authors/genres for a batch of works — same reason as <see cref="GetEditionDisplayInfoAsync"/>.</summary>
    public async Task<Dictionary<Guid, WorkDisplayInfo>> GetWorkDisplayInfoAsync(IEnumerable<Guid> workIds, CancellationToken ct)
    {
        var ids = workIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var works = await db.Works.AsNoTracking()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, w.Title, AuthorIds = w.Authors.Select(a => a.AuthorId).ToList(), w.GenreIds })
            .ToListAsync(ct);

        var authorIds = works.SelectMany(w => w.AuthorIds).Distinct().ToList();
        var authorNames = await db.Authors.AsNoTracking()
            .Where(a => authorIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FullName })
            .ToDictionaryAsync(a => a.Id, a => a.FullName, ct);

        var genreIds = works.SelectMany(w => w.GenreIds).Distinct().ToList();
        var genreNames = await db.Genres.AsNoTracking()
            .Where(g => genreIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        return works.ToDictionary(
            w => w.Id,
            w => new WorkDisplayInfo(
                w.Title,
                w.AuthorIds.Select(id => authorNames.GetValueOrDefault(id, "?")).ToList(),
                w.GenreIds.Select(id => genreNames.GetValueOrDefault(id, "?")).ToList()));
    }
}

public sealed record EditionDisplayInfo(
    string WorkTitle,
    IReadOnlyList<string> AuthorNames,
    string? CoverImageUrl,
    string? Language,
    IReadOnlyList<string> GenreNames,
    Guid WorkId = default);

public sealed record WorkDisplayInfo(string Title, IReadOnlyList<string> AuthorNames, IReadOnlyList<string> GenreNames);
