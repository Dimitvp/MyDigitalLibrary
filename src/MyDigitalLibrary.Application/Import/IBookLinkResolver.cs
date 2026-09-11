using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import;

/// <summary>
/// One link in the chain-of-responsibility that turns an arbitrary book-listing
/// URL into an ISBN (plan section 5.1/5.2). Site-specific resolvers can be added
/// later, ahead of the generic fallback, without touching the pipeline.
/// </summary>
public interface IBookLinkResolver
{
    /// <summary>Returns null if this resolver doesn't recognize/can't extract an ISBN from the page, letting the chain fall through.</summary>
    Task<Isbn?> TryResolveAsync(Uri pageUrl, CancellationToken ct);
}
