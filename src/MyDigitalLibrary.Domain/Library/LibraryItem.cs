using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Library;

/// <summary>
/// My copy of an <see cref="Catalog.Edition"/> — always has an Acquisition,
/// unlike a <see cref="WishlistEntry"/> which never does. The same Work can
/// legitimately have several LibraryItems (paperback + ebook + audiobook);
/// that is not a duplicate.
/// </summary>
public sealed class LibraryItem : Entity
{
    public Guid UserId { get; }
    public Guid EditionId { get; }
    public BookFormat Format { get; }
    public OwnershipStatus Status { get; private set; }
    public Acquisition Acquisition { get; private set; }
    public PhysicalLocation? Location { get; private set; }
    public string? PersonalNote { get; private set; }

    public LibraryItem(
        Guid userId,
        Guid editionId,
        BookFormat format,
        Acquisition acquisition,
        OwnershipStatus status = OwnershipStatus.Owned)
    {
        UserId = userId;
        EditionId = editionId;
        Format = format;
        Acquisition = acquisition;
        Status = status;
    }

    // For EF Core materialization only: Acquisition is itself an owned type
    // and cannot be bound through a constructor parameter, so EF uses this
    // and sets every property directly instead.
    private LibraryItem()
    {
        Acquisition = null!;
    }

    public void SetLocation(PhysicalLocation? location)
    {
        if (location is not null && Format != BookFormat.Physical)
            throw new DomainException("library_item.location_requires_physical", "Only physical items can have a location.");

        Location = location;
    }

    public void ChangeStatus(OwnershipStatus status) => Status = status;

    public void SetPersonalNote(string? note) => PersonalNote = note;
}
