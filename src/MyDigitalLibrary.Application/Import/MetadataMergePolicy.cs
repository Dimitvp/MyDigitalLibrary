namespace MyDigitalLibrary.Application.Import;

/// <summary>
/// Merges same-ISBN candidates from multiple providers into one (plan section
/// 5.4): first provider in priority order that has a non-empty value for a
/// field wins that field. Pure/stateless — order of <paramref name="candidates"/>
/// is the priority order (composed by <see cref="CompositeBookMetadataProvider"/>
/// from DI registration order, Open Library before Google Books).
/// </summary>
public static class MetadataMergePolicy
{
    public static BookMetadataCandidate? Merge(IReadOnlyList<BookMetadataCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        return new BookMetadataCandidate(
            ProviderKey: "merged",
            Title: FirstNonEmpty(candidates, c => c.Title),
            OriginalTitle: FirstNonEmpty(candidates, c => c.OriginalTitle),
            AuthorNames: FirstNonEmptyList(candidates, c => c.AuthorNames),
            Publisher: FirstNonEmpty(candidates, c => c.Publisher),
            PublicationYear: FirstNonNull(candidates, c => c.PublicationYear),
            Language: FirstNonEmpty(candidates, c => c.Language),
            PageCount: FirstNonNull(candidates, c => c.PageCount),
            Description: FirstNonEmpty(candidates, c => c.Description),
            CoverUrl: FirstNonNull(candidates, c => c.CoverUrl),
            SeriesName: FirstNonEmpty(candidates, c => c.SeriesName),
            SeriesPosition: FirstNonNull(candidates, c => c.SeriesPosition),
            Genres: FirstNonEmptyList(candidates, c => c.Genres));
    }

    private static string? FirstNonEmpty(IReadOnlyList<BookMetadataCandidate> candidates, Func<BookMetadataCandidate, string?> select)
        => candidates.Select(select).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static T? FirstNonNull<T>(IReadOnlyList<BookMetadataCandidate> candidates, Func<BookMetadataCandidate, T?> select)
        => candidates.Select(select).FirstOrDefault(v => v is not null);

    private static IReadOnlyList<string> FirstNonEmptyList(IReadOnlyList<BookMetadataCandidate> candidates, Func<BookMetadataCandidate, IReadOnlyList<string>> select)
        => candidates.Select(select).FirstOrDefault(v => v.Count > 0) ?? [];
}
