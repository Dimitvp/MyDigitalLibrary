using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Bookstores;

/// <summary>Plan section 6.1 — one scraping strategy per bookstore, keyed by AdapterKey (matches Bookstore.AdapterKey).</summary>
public interface IBookstoreAdapter
{
    string AdapterKey { get; }

    /// <summary>Returns null only when the fetch itself failed (network error, unparseable page) — the caller must record this as a failed check, never as OutOfStock. A successful fetch that can't determine in-stock/out-of-stock still returns a snapshot with Availability = Unknown.</summary>
    Task<OfferSnapshot?> FetchAsync(Uri listingUrl, CancellationToken ct);
}

public sealed record OfferSnapshot(MarketAvailability Availability, Money? Price, DateTimeOffset CheckedAt);
