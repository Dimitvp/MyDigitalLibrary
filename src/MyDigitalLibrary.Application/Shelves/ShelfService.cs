using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.Shelves;

/// <summary>
/// Not in plan section 4's endpoint list (only Work/Edition/LibraryItem/Wishlist/
/// ReadingSession/Import contracts are spelled out there) even though shelves are
/// explicit Stage 7 scope ("рафтове"). Designed by the same REST conventions as
/// every other resource in section 4 (plain CRUD + sub-resource item operations,
/// ProblemDetails errors, 201/204) rather than left unbuilt.
/// </summary>
public sealed class ShelfService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<IReadOnlyList<ShelfSummaryDto>> ListAsync(Guid userId, CancellationToken ct)
        => await db.Shelves.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Name)
            .Select(s => new ShelfSummaryDto(s.Id, s.Name, s.IsSystem, s.Items.Count))
            .ToListAsync(ct);

    public async Task<ShelfDetailDto> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var shelf = await db.Shelves.AsNoTracking()
            .Where(s => s.Id == id && s.UserId == userId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.IsSystem,
                Items = s.Items.Select(i => new { i.LibraryItemId, i.AddedOn, i.SortOrder }).ToList(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("shelf.not_found", $"Shelf '{id}' was not found.");

        var itemIds = shelf.Items.Select(i => i.LibraryItemId).ToList();
        var editionByItem = await db.LibraryItems.AsNoTracking()
            .Where(li => itemIds.Contains(li.Id))
            .Select(li => new { li.Id, li.EditionId })
            .ToDictionaryAsync(li => li.Id, li => li.EditionId, ct);

        var displayInfo = await catalog.GetEditionDisplayInfoAsync(editionByItem.Values, ct);

        var items = shelf.Items
            .OrderBy(i => i.SortOrder)
            .Select(i =>
            {
                var info = editionByItem.TryGetValue(i.LibraryItemId, out var editionId)
                    ? displayInfo.GetValueOrDefault(editionId)
                    : null;

                return new ShelfItemDto(i.LibraryItemId, i.AddedOn, i.SortOrder, info?.WorkTitle ?? "?", info?.AuthorNames ?? [], info?.CoverImageUrl);
            })
            .ToList();

        return new ShelfDetailDto(shelf.Id, shelf.Name, shelf.IsSystem, items);
    }

    public async Task<ShelfSummaryDto> CreateAsync(CreateShelfRequest request, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException("shelf.name_required", "Name is required.");

        await EnsureNameIsUniqueAsync(request.Name, userId, existingShelfId: null, ct);

        var shelf = new Shelf(userId, request.Name.Trim());
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync(ct);

        return new ShelfSummaryDto(shelf.Id, shelf.Name, shelf.IsSystem, 0);
    }

    public async Task RenameAsync(Guid id, RenameShelfRequest request, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException("shelf.name_required", "Name is required.");

        var shelf = await db.Shelves.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("shelf.not_found", $"Shelf '{id}' was not found.");

        await EnsureNameIsUniqueAsync(request.Name, userId, existingShelfId: id, ct);

        shelf.Rename(request.Name.Trim());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var shelf = await db.Shelves.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("shelf.not_found", $"Shelf '{id}' was not found.");

        db.Shelves.Remove(shelf);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddItemAsync(Guid id, AddShelfItemRequest request, Guid userId, CancellationToken ct)
    {
        var shelf = await db.Shelves.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("shelf.not_found", $"Shelf '{id}' was not found.");

        var itemExists = await db.LibraryItems.AsNoTracking().AnyAsync(li => li.Id == request.LibraryItemId && li.UserId == userId, ct);
        if (!itemExists)
            throw new NotFoundException("library_item.not_found", $"Library item '{request.LibraryItemId}' was not found.");

        shelf.AddItem(request.LibraryItemId, DateOnly.FromDateTime(DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveItemAsync(Guid id, Guid libraryItemId, Guid userId, CancellationToken ct)
    {
        var shelf = await db.Shelves.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("shelf.not_found", $"Shelf '{id}' was not found.");

        shelf.RemoveItem(libraryItemId);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderItemAsync(Guid id, Guid libraryItemId, ReorderShelfItemRequest request, Guid userId, CancellationToken ct)
    {
        var shelf = await db.Shelves.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("shelf.not_found", $"Shelf '{id}' was not found.");

        shelf.Reorder(libraryItemId, request.SortOrder);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid userId, Guid? existingShelfId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var duplicateId = await db.Shelves.AsNoTracking()
            .Where(s => s.UserId == userId && s.Name == trimmed && s.Id != existingShelfId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (duplicateId is not null)
            throw new ConflictException("shelf.duplicate_name", $"A shelf named '{trimmed}' already exists.");
    }
}
