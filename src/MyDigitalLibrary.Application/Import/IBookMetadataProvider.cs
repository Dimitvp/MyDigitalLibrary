using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import;

/// <summary>A metadata source looked up by ISBN (plan section 5.3/5.4) — e.g. Open Library, Google Books.</summary>
public interface IBookMetadataProvider
{
    string ProviderKey { get; }

    /// <summary>Returns null (never throws for "not found" or a transient provider failure) so the composite provider can fall through to the next source.</summary>
    Task<BookMetadataCandidate?> LookupByIsbnAsync(Isbn isbn, CancellationToken ct);

    /// <summary>
    /// Free-text title/author search, for records that never had an ISBN to
    /// begin with (e.g. wishlist entries added by hand) — used only to find a
    /// cover, so implementations should prefer a result that actually has one.
    /// Same never-throws contract as <see cref="LookupByIsbnAsync"/>.
    /// </summary>
    Task<BookMetadataCandidate?> SearchAsync(string title, IReadOnlyList<string> authorNames, CancellationToken ct);
}
