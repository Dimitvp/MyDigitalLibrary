using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

public sealed class Shelf : Entity
{
    public Guid UserId { get; }
    public string Name { get; private set; }
    public bool IsSystem { get; }

    private readonly List<ShelfItem> _items = [];
    public IReadOnlyList<ShelfItem> Items => _items.AsReadOnly();

    public Shelf(Guid userId, string name, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        UserId = userId;
        Name = name;
        IsSystem = isSystem;
    }

    public void Rename(string name)
    {
        if (IsSystem)
            throw new DomainException("shelf.system_shelf_immutable", "System shelves cannot be renamed.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
    }

    public void AddItem(Guid libraryItemId, DateOnly addedOn)
    {
        if (_items.Any(i => i.LibraryItemId == libraryItemId))
            throw new DomainException("shelf.item_already_present", "This item is already on the shelf.");

        var sortOrder = _items.Count == 0 ? 0 : _items.Max(i => i.SortOrder) + 1;
        _items.Add(new ShelfItem(Id, libraryItemId, addedOn, sortOrder));
    }

    public void RemoveItem(Guid libraryItemId) => _items.RemoveAll(i => i.LibraryItemId == libraryItemId);

    public void Reorder(Guid libraryItemId, int newSortOrder)
    {
        var item = _items.SingleOrDefault(i => i.LibraryItemId == libraryItemId)
            ?? throw new DomainException("shelf.item_not_found", "This item is not on the shelf.");

        item.SetSortOrder(newSortOrder);
    }
}
