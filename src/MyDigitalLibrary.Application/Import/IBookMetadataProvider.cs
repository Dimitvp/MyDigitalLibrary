using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import;

/// <summary>A metadata source looked up by ISBN (plan section 5.3/5.4) — e.g. Open Library, Google Books.</summary>
public interface IBookMetadataProvider
{
    string ProviderKey { get; }

    /// <summary>Returns null (never throws for "not found" or a transient provider failure) so the composite provider can fall through to the next source.</summary>
    Task<BookMetadataCandidate?> LookupByIsbnAsync(Isbn isbn, CancellationToken ct);
}
