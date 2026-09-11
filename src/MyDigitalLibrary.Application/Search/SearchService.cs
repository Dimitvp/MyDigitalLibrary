using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using NpgsqlTypes;

namespace MyDigitalLibrary.Application.Search;

/// <summary>
/// Plan section 4: GET /api/v1/search?q=... (Stage 9). PostgreSQL full-text
/// search over Work.Title/OriginalTitle/Description (a generated tsvector
/// column + GIN index — see WorkConfiguration) plus a separate author-name
/// match, since an author's name search needs a different tsvector than a
/// work's own text. "simple" config throughout — verified against a real
/// Postgres 17 (2026-09-11) that it has no Bulgarian stemmer dictionary,
/// matching the plan's own hedge; unaccent is not wired in (Cyrillic doesn't
/// carry the kind of diacritics unaccent exists for, and "simple" alone
/// already matched real Cyrillic text correctly when verified).
/// </summary>
public sealed class SearchService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return [];

        var titleMatchIds = await db.Works.AsNoTracking()
            .Where(w => EF.Property<NpgsqlTsVector>(w, "SearchVector").Matches(EF.Functions.PlainToTsQuery("simple", q)))
            .Select(w => w.Id)
            .ToListAsync(ct);

        var matchingAuthorIds = await db.Authors.AsNoTracking()
            .Where(a => EF.Functions.ToTsVector("simple", a.FullName).Matches(EF.Functions.PlainToTsQuery("simple", q)))
            .Select(a => a.Id)
            .ToListAsync(ct);

        var authorMatchIds = matchingAuthorIds.Count == 0
            ? []
            : await db.Works.AsNoTracking()
                .Where(w => w.Authors.Any(wa => matchingAuthorIds.Contains(wa.AuthorId)))
                .Select(w => w.Id)
                .ToListAsync(ct);

        var workIds = titleMatchIds.Union(authorMatchIds).ToList();
        if (workIds.Count == 0)
            return [];

        var displayInfo = await catalog.GetWorkDisplayInfoAsync(workIds, ct);

        return workIds
            .Select(id => new SearchResultDto(id, displayInfo.GetValueOrDefault(id)?.Title ?? "?", displayInfo.GetValueOrDefault(id)?.AuthorNames ?? []))
            .ToList();
    }
}
