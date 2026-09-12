using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Wishlist;

public sealed class WishlistService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<IReadOnlyList<WishlistEntryDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        // Mapped in memory, not via Select(), because ToDto isn't translatable to SQL.
        var entries = await db.WishlistEntries.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedOn)
            .ToListAsync(ct);

        var displayInfo = await catalog.GetWorkDisplayInfoAsync(entries.Select(e => e.WorkId), ct);
        return entries.Select(e => WishlistEntryMapper.ToDto(e, displayInfo.GetValueOrDefault(e.WorkId, EmptyDisplayInfo))).ToList();
    }

    public async Task<WishlistEntryDto> CreateAsync(CreateWishlistEntryRequest request, Guid userId, CancellationToken ct)
    {
        var hasWorkId = request.WorkId is not null;
        var hasNestedWork = request.Work is not null;

        if (hasWorkId == hasNestedWork)
            throw new AppValidationException("wishlist_entry.invalid_work_reference", "Provide either workId or work, but not both.");

        Guid workId;

        if (request.WorkId is { } existingWorkId)
        {
            var exists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == existingWorkId, ct);
            if (!exists)
                throw new NotFoundException("work.not_found", $"Work '{existingWorkId}' was not found.");

            workId = existingWorkId;
        }
        else
        {
            var work = await catalog.ResolveOrCreateWorkAsync(request.Work!, ct);
            workId = work.Id;
        }

        var entry = new Domain.Library.WishlistEntry(
            userId,
            workId,
            request.DesiredFormat,
            request.Priority,
            DateOnly.FromDateTime(DateTime.UtcNow),
            request.PreferredEditionId,
            request.MaxPrice is null ? null : new Money(request.MaxPrice.Amount, request.MaxPrice.CurrencyCode),
            request.Note);

        db.WishlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        var displayInfo = await catalog.GetWorkDisplayInfoAsync([workId], ct);
        return WishlistEntryMapper.ToDto(entry, displayInfo.GetValueOrDefault(workId, EmptyDisplayInfo));
    }

    public async Task<LibraryItemDto> FulfillAsync(Guid id, FulfillWishlistEntryRequest request, Guid userId, CancellationToken ct)
    {
        var entry = await db.WishlistEntries.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("wishlist_entry.not_found", $"Wishlist entry '{id}' was not found.");

        var editionExists = await db.Editions.AsNoTracking().AnyAsync(e => e.Id == request.EditionId, ct);
        if (!editionExists)
            throw new NotFoundException("edition.not_found", $"Edition '{request.EditionId}' was not found.");

        var acquisition = new Acquisition(
            request.Acquisition.AcquiredOn,
            request.Acquisition.Method,
            request.Acquisition.Price is null ? null : new Money(request.Acquisition.Price.Amount, request.Acquisition.Price.CurrencyCode),
            request.Acquisition.Source);

        var libraryItem = entry.Fulfill(request.EditionId, acquisition);

        if (request.Location is not null)
            libraryItem.SetLocation(new PhysicalLocation(request.Location.Room, request.Location.Shelf, request.Location.Box));

        db.LibraryItems.Add(libraryItem);
        await db.SaveChangesAsync(ct);

        var editionDisplayInfo = await catalog.GetEditionDisplayInfoAsync([request.EditionId], ct);
        return LibraryItemMapper.ToDto(libraryItem, editionDisplayInfo.GetValueOrDefault(request.EditionId, EmptyEditionDisplayInfo), ReadingInfo.Empty);
    }

    private static readonly WorkDisplayInfo EmptyDisplayInfo = new("?", [], []);
    private static readonly EditionDisplayInfo EmptyEditionDisplayInfo = new("?", [], null, null, []);
}
