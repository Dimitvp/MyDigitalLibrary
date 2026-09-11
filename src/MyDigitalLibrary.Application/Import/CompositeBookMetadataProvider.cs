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
}
