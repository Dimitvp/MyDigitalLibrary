using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Library;

/// <summary>
/// A book I want but don't own — deliberately a separate aggregate from
/// LibraryItem rather than a status, since "want" and "have" have different
/// required fields (no acquisition date/price/location) and a one-way
/// transition (<see cref="Fulfill"/>), not an enum flip.
/// </summary>
public sealed class WishlistEntry : Entity, IUserOwned
{
    public Guid UserId { get; }
    public Guid WorkId { get; }
    public Guid? PreferredEditionId { get; private set; }
    public BookFormat DesiredFormat { get; private set; }
    public int Priority { get; private set; }
    public Money? MaxPrice { get; private set; }
    public string? Note { get; private set; }
    public DateOnly AddedOn { get; }
    public bool IsFulfilled { get; private set; }

    /// <summary>
    /// Free-standing flag rather than a full per-store <c>MarketAvailability</c>
    /// (see <c>BookstoreListing</c>) — a wishlist entry isn't tied to one
    /// store, so this just records "the last time I checked, nobody had it",
    /// for the UI to flag. Manually set; nothing refreshes it automatically.
    /// </summary>
    public bool IsOutOfStock { get; private set; }

    public WishlistEntry(
        Guid userId,
        Guid workId,
        BookFormat desiredFormat,
        int priority,
        DateOnly addedOn,
        Guid? preferredEditionId = null,
        Money? maxPrice = null,
        string? note = null,
        bool isOutOfStock = false)
    {
        ValidatePriority(priority);

        UserId = userId;
        WorkId = workId;
        DesiredFormat = desiredFormat;
        Priority = priority;
        AddedOn = addedOn;
        PreferredEditionId = preferredEditionId;
        MaxPrice = maxPrice;
        Note = note;
        IsOutOfStock = isOutOfStock;
    }

    // For EF Core materialization only: MaxPrice is itself an owned type and
    // cannot be bound through a constructor parameter, so EF uses this and
    // sets every property directly instead.
    private WishlistEntry()
    {
    }

    public void Update(BookFormat desiredFormat, int priority, Guid? preferredEditionId, Money? maxPrice, string? note, bool isOutOfStock)
    {
        ValidatePriority(priority);

        DesiredFormat = desiredFormat;
        Priority = priority;
        PreferredEditionId = preferredEditionId;
        MaxPrice = maxPrice;
        Note = note;
        IsOutOfStock = isOutOfStock;
    }

    /// <summary>Bought it: close the wish and create the owned copy.</summary>
    public LibraryItem Fulfill(Guid editionId, Acquisition acquisition)
    {
        if (IsFulfilled)
            throw new DomainException("wishlist_entry.already_fulfilled", "This wishlist entry has already been fulfilled.");

        var item = new LibraryItem(UserId, editionId, DesiredFormat, acquisition);
        IsFulfilled = true;
        return item;
    }

    private static void ValidatePriority(int priority)
    {
        if (priority is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be between 1 and 5.");
    }
}
