using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.Catalog;

namespace MyDigitalLibrary.Application.Bookstores;

/// <summary>
/// GET/POST /api/v1/editions/{id}/listings (plan section 4). The manual
/// add-a-link path (plan 6.3) always works, independent of whether any
/// IBookstoreAdapter is registered for the resolved bookstore.
/// </summary>
public sealed class BookstoreListingService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<BookstoreListingDto>> ListForEditionAsync(Guid editionId, CancellationToken ct)
    {
        var editionExists = await db.Editions.AsNoTracking().AnyAsync(e => e.Id == editionId, ct);
        if (!editionExists)
            throw new NotFoundException("edition.not_found", $"Edition '{editionId}' was not found.");

        var listings = await db.BookstoreListings.AsNoTracking()
            .Where(l => l.EditionId == editionId)
            .Select(l => new { l.Id, l.EditionId, l.BookstoreId, l.Url, l.Availability, l.Price, l.LastCheckedAt, l.ConsecutiveFailures })
            .ToListAsync(ct);

        var bookstoreNames = await db.Bookstores.AsNoTracking()
            .Where(b => listings.Select(l => l.BookstoreId).Contains(b.Id))
            .Select(b => new { b.Id, b.Name })
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        return listings
            .Select(l => new BookstoreListingDto(
                l.Id, l.EditionId, bookstoreNames.GetValueOrDefault(l.BookstoreId, "?"), l.Url.ToString(),
                l.Availability, l.Price is null ? null : new MoneyDto(l.Price.Amount, l.Price.CurrencyCode),
                l.LastCheckedAt, l.ConsecutiveFailures))
            .ToList();
    }

    public async Task<BookstoreListingDto> AddManualAsync(Guid editionId, CreateBookstoreListingRequest request, CancellationToken ct)
    {
        var editionExists = await db.Editions.AsNoTracking().AnyAsync(e => e.Id == editionId, ct);
        if (!editionExists)
            throw new NotFoundException("edition.not_found", $"Edition '{editionId}' was not found.");

        if (!Uri.TryCreate(request.ListingUrl, UriKind.Absolute, out var listingUrl))
            throw new AppValidationException("bookstore_listing.invalid_url", $"'{request.ListingUrl}' is not a valid URL.");

        if (!Uri.TryCreate(request.BookstoreBaseUrl, UriKind.Absolute, out var baseUrl))
            throw new AppValidationException("bookstore_listing.invalid_url", $"'{request.BookstoreBaseUrl}' is not a valid URL.");

        var bookstore = await ResolveOrCreateBookstoreAsync(request.BookstoreName, baseUrl, request.AdapterKey, ct);

        var listing = new BookstoreListing(editionId, bookstore.Id, listingUrl);
        db.BookstoreListings.Add(listing);
        await db.SaveChangesAsync(ct);

        return new BookstoreListingDto(listing.Id, listing.EditionId, bookstore.Name, listing.Url.ToString(), listing.Availability, null, null, 0);
    }

    /// <summary>Plan section 6.3 — works even with every adapter disabled.</summary>
    public async Task MarkDiscontinuedAsync(Guid listingId, CancellationToken ct)
    {
        var listing = await db.BookstoreListings.FirstOrDefaultAsync(l => l.Id == listingId, ct)
            ?? throw new NotFoundException("bookstore_listing.not_found", $"Listing '{listingId}' was not found.");

        listing.SetDiscontinuedManually();
        await db.SaveChangesAsync(ct);
    }

    private async Task<Bookstore> ResolveOrCreateBookstoreAsync(string name, Uri baseUrl, string? adapterKey, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new AppValidationException("bookstore_listing.bookstore_name_required", "Bookstore name is required.");

        var existing = await db.Bookstores.FirstOrDefaultAsync(b => b.Name == trimmed, ct);
        if (existing is not null)
            return existing;

        // No adapter key given -> a slug of the name. If that slug never
        // matches a registered IBookstoreAdapter, the listing simply never
        // gets auto-refreshed — plan 6.3's manual path doesn't require one.
        var bookstore = new Bookstore(trimmed, baseUrl, adapterKey ?? Slugify(trimmed));
        db.Bookstores.Add(bookstore);
        return bookstore;
    }

    private static string Slugify(string name)
        => new([.. name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')]);
}
