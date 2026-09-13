using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Wishlist;

public sealed class WishlistService(IApplicationDbContext db, BookCatalogService catalog, CompositeBookMetadataProvider metadataProvider)
{
    public async Task<IReadOnlyList<WishlistEntryDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        // Mapped in memory, not via Select(), because ToDto isn't translatable to SQL.
        // Fulfilled entries have already become a LibraryItem (Fulfill is a
        // one-way transition, see WishlistEntry) — they no longer belong in
        // the "still want it" list.
        var entries = await db.WishlistEntries.AsNoTracking()
            .Where(w => w.UserId == userId && !w.IsFulfilled)
            .OrderByDescending(w => w.AddedOn)
            .ToListAsync(ct);

        var displayInfo = await catalog.GetWorkDisplayInfoAsync(entries.Select(e => e.WorkId), ct);
        var editionIds = entries.Where(e => e.PreferredEditionId is not null).Select(e => e.PreferredEditionId!.Value);
        var editionDisplay = await catalog.GetEditionDisplayInfoAsync(editionIds, ct);

        return entries.Select(e => WishlistEntryMapper.ToDto(
            e,
            displayInfo.GetValueOrDefault(e.WorkId, EmptyDisplayInfo),
            e.PreferredEditionId is { } editionId ? editionDisplay.GetValueOrDefault(editionId) : null)).ToList();
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
            request.Note,
            request.IsOutOfStock);

        db.WishlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        var displayInfo = await catalog.GetWorkDisplayInfoAsync([workId], ct);
        EditionDisplayInfo? editionInfo = null;
        if (entry.PreferredEditionId is { } preferredEditionId)
        {
            var editionDisplay = await catalog.GetEditionDisplayInfoAsync([preferredEditionId], ct);
            editionInfo = editionDisplay.GetValueOrDefault(preferredEditionId);
        }

        return WishlistEntryMapper.ToDto(entry, displayInfo.GetValueOrDefault(workId, EmptyDisplayInfo), editionInfo);
    }

    public async Task<WishlistEntryDto> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var entry = await db.WishlistEntries.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("wishlist_entry.not_found", $"Wishlist entry '{id}' was not found.");

        var displayInfo = await catalog.GetWorkDisplayInfoAsync([entry.WorkId], ct);
        EditionDisplayInfo? editionInfo = null;
        if (entry.PreferredEditionId is { } preferredEditionId)
        {
            var editionDisplay = await catalog.GetEditionDisplayInfoAsync([preferredEditionId], ct);
            editionInfo = editionDisplay.GetValueOrDefault(preferredEditionId);
        }

        return WishlistEntryMapper.ToDto(entry, displayInfo.GetValueOrDefault(entry.WorkId, EmptyDisplayInfo), editionInfo);
    }

    public async Task<WishlistEntryDto> UpdateAsync(Guid id, UpdateWishlistEntryRequest request, Guid userId, CancellationToken ct)
    {
        var entry = await db.WishlistEntries.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("wishlist_entry.not_found", $"Wishlist entry '{id}' was not found.");

        if (request.PreferredEditionId is { } editionId)
        {
            var editionExists = await db.Editions.AsNoTracking().AnyAsync(e => e.Id == editionId, ct);
            if (!editionExists)
                throw new NotFoundException("edition.not_found", $"Edition '{editionId}' was not found.");
        }

        entry.Update(
            request.DesiredFormat,
            request.Priority,
            request.PreferredEditionId,
            request.MaxPrice is null ? null : new Money(request.MaxPrice.Amount, request.MaxPrice.CurrencyCode),
            request.Note,
            request.IsOutOfStock);

        await db.SaveChangesAsync(ct);

        var displayInfo = await catalog.GetWorkDisplayInfoAsync([entry.WorkId], ct);
        EditionDisplayInfo? editionInfo = null;
        if (entry.PreferredEditionId is { } preferredEditionId)
        {
            var editionDisplay = await catalog.GetEditionDisplayInfoAsync([preferredEditionId], ct);
            editionInfo = editionDisplay.GetValueOrDefault(preferredEditionId);
        }

        return WishlistEntryMapper.ToDto(entry, displayInfo.GetValueOrDefault(entry.WorkId, EmptyDisplayInfo), editionInfo);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var entry = await db.WishlistEntries.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("wishlist_entry.not_found", $"Wishlist entry '{id}' was not found.");

        db.WishlistEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Best-effort cover backfill for entries with no edition on file (plan:
    /// wishlist entries created without ever going through an ISBN/link
    /// search never get one — see CreateAsync). A title/author match is
    /// inherently uncertain, so this only ever accepts a candidate whose
    /// title actually resembles the entry's — otherwise it reports
    /// Found=false rather than risk pinning the wrong book's cover.
    /// </summary>
    public async Task<FindCoverResultDto> FindCoverAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var entry = await db.WishlistEntries.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("wishlist_entry.not_found", $"Wishlist entry '{id}' was not found.");

        if (entry.PreferredEditionId is not null)
            return await BuildFindCoverResultAsync(entry, found: false, coverUrl: null, ct);

        var displayInfo = (await catalog.GetWorkDisplayInfoAsync([entry.WorkId], ct)).GetValueOrDefault(entry.WorkId, EmptyDisplayInfo);

        var candidate = await metadataProvider.SearchAsync(displayInfo.Title, displayInfo.AuthorNames, ct);
        if (candidate?.CoverUrl is null || !TitleLooksLikeAMatch(displayInfo.Title, candidate.Title))
            return await BuildFindCoverResultAsync(entry, found: false, coverUrl: null, ct);

        var edition = await catalog.CreateEditionAsync(entry.WorkId, new CreateEditionRequest(
            entry.DesiredFormat,
            Isbn13: null, // never trust an ISBN found by title match — it names a specific edition we haven't verified is this one
            Publisher: candidate.Publisher,
            Language: candidate.Language,
            Translator: null,
            PublicationYear: candidate.PublicationYear,
            PageCount: candidate.PageCount,
            CoverType: null,
            Narrator: null,
            DurationMinutes: null,
            CoverUrl: candidate.CoverUrl), ct);

        entry.Update(entry.DesiredFormat, entry.Priority, edition.Id, entry.MaxPrice, entry.Note, entry.IsOutOfStock);
        await db.SaveChangesAsync(ct);

        return await BuildFindCoverResultAsync(entry, found: true, coverUrl: candidate.CoverUrl.ToString(), ct);
    }

    private async Task<FindCoverResultDto> BuildFindCoverResultAsync(Domain.Library.WishlistEntry entry, bool found, string? coverUrl, CancellationToken ct)
    {
        var displayInfo = await catalog.GetWorkDisplayInfoAsync([entry.WorkId], ct);
        EditionDisplayInfo? editionInfo = null;
        if (entry.PreferredEditionId is { } preferredEditionId)
        {
            var editionDisplay = await catalog.GetEditionDisplayInfoAsync([preferredEditionId], ct);
            editionInfo = editionDisplay.GetValueOrDefault(preferredEditionId);
        }

        var dto = WishlistEntryMapper.ToDto(entry, displayInfo.GetValueOrDefault(entry.WorkId, EmptyDisplayInfo), editionInfo);
        return new FindCoverResultDto(found, coverUrl, dto);
    }

    /// <summary>
    /// Loose word-overlap check, not exact equality — providers normalize
    /// subtitles/punctuation differently. Requires at least half of the
    /// shorter title's significant words to appear in the other, which is
    /// enough to reject an unrelated book without being so strict it rejects
    /// every real match over a translated/reformatted title.
    /// </summary>
    private static bool TitleLooksLikeAMatch(string ourTitle, string? candidateTitle)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle))
            return false;

        var ourWords = SignificantWords(ourTitle);
        var candidateWords = SignificantWords(candidateTitle);
        if (ourWords.Count == 0 || candidateWords.Count == 0)
            return false;

        var overlap = ourWords.Count(w => candidateWords.Contains(w));
        var shorterCount = Math.Min(ourWords.Count, candidateWords.Count);
        return overlap >= Math.Max(1, shorterCount / 2);
    }

    private static HashSet<string> SignificantWords(string title) =>
        Regex.Matches(title.ToLowerInvariant(), @"[\p{L}\p{Nd}]+")
            .Select(m => m.Value)
            .Where(w => w.Length > 2)
            .ToHashSet();

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
