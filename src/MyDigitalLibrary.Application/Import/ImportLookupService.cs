using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import;

public sealed record ImportLookupRequest(string? Url, string? Isbn);

public sealed record ImportLookupResult(string Isbn13, BookMetadataCandidate Candidate, IReadOnlyList<BookMetadataCandidate> Alternates);

/// <summary>
/// Orchestrates the plan section 5.1 pipeline for <c>POST /api/v1/import/lookup</c>:
/// URL or ISBN -&gt; ISBN (via link resolver chain, if a URL) -&gt; provider lookup -&gt; merged
/// candidate. Read-only — the review/confirm screen persists through the ordinary
/// composite <c>POST /library-items</c>/<c>POST /wishlist</c> endpoints (plan section 4),
/// not through this one.
/// </summary>
public sealed class ImportLookupService(CompositeBookLinkResolver linkResolver, CompositeBookMetadataProvider metadataProvider)
{
    public async Task<ImportLookupResult> LookupAsync(ImportLookupRequest request, CancellationToken ct)
    {
        var hasUrl = !string.IsNullOrWhiteSpace(request.Url);
        var hasIsbn = !string.IsNullOrWhiteSpace(request.Isbn);

        if (hasUrl == hasIsbn)
            throw new AppValidationException("import.invalid_request", "Provide exactly one of url or isbn.");

        var isbn = hasIsbn
            ? ParseIsbnOrThrow(request.Isbn!)
            : await ResolveIsbnFromUrlAsync(request.Url!, ct);

        var (merged, alternates) = await metadataProvider.LookupByIsbnAsync(isbn, ct);

        if (merged is null)
            throw new NotFoundException("import.metadata_not_found", $"No provider had metadata for ISBN {isbn.Value}.");

        return new ImportLookupResult(isbn.Value, merged, alternates);
    }

    private async Task<Isbn> ResolveIsbnFromUrlAsync(string rawUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            throw new AppValidationException("import.invalid_url", $"'{rawUrl}' is not a valid http(s) URL.");

        var isbn = await linkResolver.ResolveAsync(url, ct);
        return isbn ?? throw new NotFoundException("import.isbn_not_found", $"No ISBN could be found on {url}.");
    }

    private static Isbn ParseIsbnOrThrow(string rawIsbn)
    {
        var result = Isbn.TryCreate(rawIsbn);
        if (result.IsFailure)
            throw new AppValidationException(result.ErrorCode, $"'{rawIsbn}' is not a valid ISBN.");

        return result.Value;
    }
}
