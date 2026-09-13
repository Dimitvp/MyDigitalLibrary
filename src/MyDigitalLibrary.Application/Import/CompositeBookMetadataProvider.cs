using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import;

/// <summary>Queries every registered <see cref="IBookMetadataProvider"/> and merges the results via <see cref="MetadataMergePolicy"/>.</summary>
public sealed class CompositeBookMetadataProvider(IEnumerable<IBookMetadataProvider> providers)
{
    public async Task<(BookMetadataCandidate? Merged, IReadOnlyList<BookMetadataCandidate> Candidates)> LookupByIsbnAsync(Isbn isbn, CancellationToken ct)
    {
        var results = new List<BookMetadataCandidate>();

        foreach (var provider in providers)
        {
            var candidate = await provider.LookupByIsbnAsync(isbn, ct);
            if (candidate is not null)
                results.Add(candidate);
        }

        return (MetadataMergePolicy.Merge(results), results);
    }

    /// <summary>
    /// Title/author search, used only to backfill a cover for records with no
    /// ISBN. Unlike <see cref="LookupByIsbnAsync"/>, results aren't merged —
    /// a free-text match is inherently less certain than an ISBN lookup, so
    /// the first provider (in registration order) that returns a candidate
    /// with an actual cover wins, rather than blending fields from several
    /// possibly-different editions/books.
    /// </summary>
    public async Task<BookMetadataCandidate?> SearchAsync(string title, IReadOnlyList<string> authorNames, CancellationToken ct)
    {
        foreach (var provider in providers)
        {
            var candidate = await provider.SearchAsync(title, authorNames, ct);
            if (candidate is { CoverUrl: not null })
                return candidate;
        }

        return null;
    }
}
