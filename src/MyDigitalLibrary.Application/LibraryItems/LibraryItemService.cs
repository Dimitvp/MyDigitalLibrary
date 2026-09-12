using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.LibraryItems;

public sealed class LibraryItemService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<PagedResult<LibraryItemDto>> ListAsync(
        BookFormat? format, OwnershipStatus? status, Guid? shelfId, string? q, int? page, int? pageSize,
        Guid userId, CancellationToken ct)
    {
        var (normalizedPage, normalizedPageSize) = PageRequest.Normalize(page, pageSize);

        var query = db.LibraryItems.AsNoTracking().Where(li => li.UserId == userId);

        if (format is { } f)
            query = query.Where(li => li.Format == f);

        if (status is { } s)
            query = query.Where(li => li.Status == s);

        if (shelfId is { } sId)
            query = query.Where(li => db.Shelves.Any(sh => sh.Id == sId && sh.Items.Any(i => i.LibraryItemId == li.Id)));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var editionsByWorkTitle = db.Editions.Join(db.Works, e => e.WorkId, w => w.Id, (e, w) => new { e.Id, w.Title });
            query = query.Where(li => editionsByWorkTitle.Any(x => x.Id == li.EditionId && x.Title.Contains(q)));
        }

        var totalCount = await query.CountAsync(ct);

        var rows = await ProjectToRow(query)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        var displayInfo = await catalog.GetEditionDisplayInfoAsync(rows.Select(r => r.EditionId), ct);
        var readingInfo = await GetLatestReadingInfoAsync(rows.Select(r => r.Id), ct);
        var items = rows
            .Select(r => r.ToDto(displayInfo.GetValueOrDefault(r.EditionId, EmptyDisplayInfo), readingInfo.GetValueOrDefault(r.Id, ReadingInfo.Empty)))
            .ToList();

        return new PagedResult<LibraryItemDto>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<LibraryItemDto> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await ProjectToRow(db.LibraryItems.AsNoTracking().Where(li => li.Id == id && li.UserId == userId)).FirstOrDefaultAsync(ct)
            ?? throw NotFound(id);

        var displayInfo = await catalog.GetEditionDisplayInfoAsync([row.EditionId], ct);
        var readingInfo = await GetLatestReadingInfoAsync([row.Id], ct);
        return row.ToDto(displayInfo.GetValueOrDefault(row.EditionId, EmptyDisplayInfo), readingInfo.GetValueOrDefault(row.Id, ReadingInfo.Empty));
    }

    public async Task<LibraryItemDto> CreateAsync(CreateLibraryItemRequest request, Guid userId, CancellationToken ct)
    {
        var hasEditionId = request.EditionId is not null;
        var hasNested = request.Work is not null || request.Edition is not null;

        if (hasEditionId == hasNested)
            throw new AppValidationException(
                "library_item.invalid_edition_reference",
                "Provide either editionId, or both work and edition, but not both.");

        if (hasNested && (request.Work is null || request.Edition is null))
            throw new AppValidationException(
                "library_item.invalid_edition_reference",
                "Both work and edition are required when editionId is not provided.");

        Guid editionId;

        if (request.EditionId is { } existingId)
        {
            var editionExists = await db.Editions.AsNoTracking().AnyAsync(e => e.Id == existingId, ct);
            if (!editionExists)
                throw new NotFoundException("edition.not_found", $"Edition '{existingId}' was not found.");

            editionId = existingId;
        }
        else
        {
            var work = await catalog.ResolveOrCreateWorkAsync(request.Work!, ct);
            var editionRequest = new CreateEditionRequest(
                request.Format, request.Edition!.Isbn13, request.Edition.Publisher, request.Edition.Language,
                request.Edition.Translator, request.Edition.PublicationYear, request.Edition.PageCount,
                request.Edition.CoverType, request.Edition.Narrator, request.Edition.DurationMinutes,
                request.Edition.CoverUrl);

            var edition = await catalog.CreateEditionAsync(work.Id, editionRequest, ct);
            editionId = edition.Id;
        }

        var item = new LibraryItem(userId, editionId, request.Format, ToDomainAcquisition(request.Acquisition), request.Status ?? OwnershipStatus.Owned);

        if (request.Location is not null)
            item.SetLocation(new PhysicalLocation(request.Location.Room, request.Location.Shelf, request.Location.Box));

        db.LibraryItems.Add(item);
        await db.SaveChangesAsync(ct);

        var displayInfo = await catalog.GetEditionDisplayInfoAsync([editionId], ct);
        return LibraryItemMapper.ToDto(item, displayInfo.GetValueOrDefault(editionId, EmptyDisplayInfo), ReadingInfo.Empty);
    }

    public async Task UpdateAsync(Guid id, UpdateLibraryItemRequest request, Guid userId, CancellationToken ct)
    {
        var item = await db.LibraryItems.FirstOrDefaultAsync(li => li.Id == id && li.UserId == userId, ct)
            ?? throw NotFound(id);

        item.UpdateAcquisition(ToDomainAcquisition(request.Acquisition));
        item.SetPersonalNote(request.PersonalNote);

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var item = await db.LibraryItems.FirstOrDefaultAsync(li => li.Id == id && li.UserId == userId, ct)
            ?? throw NotFound(id);

        db.LibraryItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateLocationAsync(Guid id, PhysicalLocationDto? location, Guid userId, CancellationToken ct)
    {
        var item = await db.LibraryItems.FirstOrDefaultAsync(li => li.Id == id && li.UserId == userId, ct)
            ?? throw NotFound(id);

        item.SetLocation(location is null ? null : new PhysicalLocation(location.Room, location.Shelf, location.Box));

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, OwnershipStatus status, Guid userId, CancellationToken ct)
    {
        var item = await db.LibraryItems.FirstOrDefaultAsync(li => li.Id == id && li.UserId == userId, ct)
            ?? throw NotFound(id);

        item.ChangeStatus(status);
        await db.SaveChangesAsync(ct);
    }

    private static readonly EditionDisplayInfo EmptyDisplayInfo = new("?", [], null, null, []);

    /// <summary>
    /// One row per library item, taken from its most recently started
    /// ReadingSession (rereads start a new session rather than reusing the
    /// old one, so "most recent" is the right notion of current status).
    /// </summary>
    private async Task<Dictionary<Guid, ReadingInfo>> GetLatestReadingInfoAsync(IEnumerable<Guid> libraryItemIds, CancellationToken ct)
    {
        var ids = libraryItemIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var latestSessions = await db.ReadingSessions.AsNoTracking()
            .Where(s => ids.Contains(s.LibraryItemId))
            .GroupBy(s => s.LibraryItemId)
            .Select(g => g.OrderByDescending(s => s.StartedOn).First())
            .ToListAsync(ct);

        return latestSessions.ToDictionary(s => s.LibraryItemId, s => new ReadingInfo(s.Status, s.StartedOn, s.EndedOn));
    }

    private static NotFoundException NotFound(Guid id) => new("library_item.not_found", $"Library item '{id}' was not found.");

    private static Acquisition ToDomainAcquisition(AcquisitionDto dto) => new(
        dto.AcquiredOn,
        dto.Method,
        dto.Price is null ? null : new Money(dto.Price.Amount, dto.Price.CurrencyCode),
        dto.Source);

    private static IQueryable<LibraryItemRow> ProjectToRow(IQueryable<LibraryItem> source) => source.Select(li => new LibraryItemRow(
        li.Id, li.UserId, li.EditionId, li.Format, li.Status,
        new AcquisitionDto(
            li.Acquisition.AcquiredOn,
            li.Acquisition.Method,
            li.Acquisition.Price == null ? null : new MoneyDto(li.Acquisition.Price.Amount, li.Acquisition.Price.CurrencyCode),
            li.Acquisition.Source),
        li.Location == null ? null : new PhysicalLocationDto(li.Location.Room, li.Location.Shelf, li.Location.Box),
        li.PersonalNote));
}

/// <summary>Everything a query can project directly; catalog display info (title/authors/cover) is stitched on afterward.</summary>
internal sealed record LibraryItemRow(
    Guid Id,
    Guid UserId,
    Guid EditionId,
    BookFormat Format,
    OwnershipStatus Status,
    AcquisitionDto Acquisition,
    PhysicalLocationDto? Location,
    string? PersonalNote)
{
    public LibraryItemDto ToDto(EditionDisplayInfo displayInfo, ReadingInfo readingInfo) => new(
        Id, UserId, EditionId, Format, Status, Acquisition, Location, PersonalNote,
        displayInfo.WorkTitle, displayInfo.AuthorNames, displayInfo.CoverImageUrl,
        readingInfo.Status, readingInfo.StartedOn, readingInfo.EndedOn,
        displayInfo.Language, displayInfo.GenreNames, displayInfo.WorkId);
}
