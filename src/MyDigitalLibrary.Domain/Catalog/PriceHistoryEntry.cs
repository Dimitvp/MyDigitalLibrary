using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class PriceHistoryEntry : Entity
{
    public Guid ListingId { get; }
    public Money Price { get; }
    public DateTimeOffset ObservedAt { get; }

    internal PriceHistoryEntry(Guid listingId, Money price, DateTimeOffset observedAt)
    {
        ListingId = listingId;
        Price = price;
        ObservedAt = observedAt;
    }
}
