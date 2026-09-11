using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Catalog;

/// <summary>
/// A listing of an edition on a bookstore's site. A failed availability check
/// must never be recorded as OutOfStock — see <see cref="RecordFailedCheck"/>.
/// </summary>
public sealed class BookstoreListing : Entity
{
    public Guid EditionId { get; }
    public Guid BookstoreId { get; }
    public Uri Url { get; }
    public MarketAvailability Availability { get; private set; }
    public Money? Price { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    private readonly List<PriceHistoryEntry> _priceHistory = [];
    public IReadOnlyList<PriceHistoryEntry> PriceHistory => _priceHistory.AsReadOnly();

    public BookstoreListing(Guid editionId, Guid bookstoreId, Uri url)
    {
        EditionId = editionId;
        BookstoreId = bookstoreId;
        Url = url ?? throw new ArgumentNullException(nameof(url));
        Availability = MarketAvailability.Unknown;
    }

    public void RecordSuccessfulCheck(MarketAvailability availability, Money? price, DateTimeOffset checkedAt)
    {
        Availability = availability;
        Price = price;
        LastCheckedAt = checkedAt;
        ConsecutiveFailures = 0;

        if (price is not null)
            _priceHistory.Add(new PriceHistoryEntry(Id, price, checkedAt));
    }

    // Never touches Availability: a scrape failure must not be mistaken for "out of stock".
    public void RecordFailedCheck(DateTimeOffset checkedAt)
    {
        ConsecutiveFailures++;
        LastCheckedAt = checkedAt;
    }

    public void MarkUnknownDueToRepeatedFailures() => Availability = MarketAvailability.Unknown;

    public void SetDiscontinuedManually() => Availability = MarketAvailability.Discontinued;
}
