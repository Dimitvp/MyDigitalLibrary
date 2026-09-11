using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

public sealed class ShelfItem : Entity
{
    public Guid ShelfId { get; }
    public Guid LibraryItemId { get; }
    public DateOnly AddedOn { get; }
    public int SortOrder { get; private set; }

    internal ShelfItem(Guid shelfId, Guid libraryItemId, DateOnly addedOn, int sortOrder)
    {
        ShelfId = shelfId;
        LibraryItemId = libraryItemId;
        AddedOn = addedOn;
        SortOrder = sortOrder;
    }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
