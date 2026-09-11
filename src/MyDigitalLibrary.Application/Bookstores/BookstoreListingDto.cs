using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Application.Bookstores;

public sealed record BookstoreListingDto(
    Guid Id,
    Guid EditionId,
    string BookstoreName,
    string Url,
    MarketAvailability Availability,
    MoneyDto? Price,
    DateTimeOffset? LastCheckedAt,
    int ConsecutiveFailures);

/// <summary>Plan section 6.3 — the manual path always works: a free-text bookstore name/URL, resolved-or-created (same pattern as Author/Series in BookCatalogService). AdapterKey is optional; when it doesn't match a registered IBookstoreAdapter, the listing just never gets auto-refreshed — the user can still see/edit it manually.</summary>
public sealed record CreateBookstoreListingRequest(string BookstoreName, string BookstoreBaseUrl, string ListingUrl, string? AdapterKey);
