using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.Enums;
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
        string? title;

        if (request.WorkId is { } existingWorkId)
        {
            title = await db.Works.AsNoTracking().Where(w => w.Id == existingWorkId).Select(w => w.Title).FirstOrDefaultAsync(ct);
            if (title is null)
                throw new NotFoundException("work.not_found", $"Work '{existingWorkId}' was not found.");

            workId = existingWorkId;
        }
        else
        {
            var work = await catalog.ResolveOrCreateWorkAsync(request.Work!, ct);
            workId = work.Id;
            title = work.Title;
        }

        await EnsureNotAlreadyOwnedAsync(title, request.Isbn13, request.Language, userId, ct);

        // If the caller already typed an ISBN/language on the add form, keep
        // it — creating the edition right away instead of discarding those
        // values and forcing the user to retype them on the edit page.
        var preferredEditionId = request.PreferredEditionId;
        if (preferredEditionId is null && (!string.IsNullOrWhiteSpace(request.Isbn13) || !string.IsNullOrWhiteSpace(request.Language)))
        {
            var edition = await catalog.CreateEditionAsync(workId, new CreateEditionRequest(
                request.DesiredFormat,
                Isbn13: request.Isbn13,
                Publisher: null,
                Language: request.Language,
                Translator: null,
                PublicationYear: null,
                PageCount: null,
                CoverType: null,
                Narrator: null,
                DurationMinutes: null), ct);
            preferredEditionId = edition.Id;
        }

        var entry = new Domain.Library.WishlistEntry(
            userId,
            workId,
            request.DesiredFormat,
            request.Priority,
            DateOnly.FromDateTime(DateTime.UtcNow),
            preferredEditionId,
            request.MaxPrice is null ? null : new Money(request.MaxPrice.Amount, request.MaxPrice.CurrencyCode),
            request.Note,
            request.IsOutOfStock);

        db.WishlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        var displayInfo = await catalog.GetWorkDisplayInfoAsync([workId], ct);
        EditionDisplayInfo? editionInfo = null;
        if (entry.PreferredEditionId is { } linkedEditionId)
        {
            var editionDisplay = await catalog.GetEditionDisplayInfoAsync([linkedEditionId], ct);
            editionInfo = editionDisplay.GetValueOrDefault(linkedEditionId);
        }

        return WishlistEntryMapper.ToDto(entry, displayInfo.GetValueOrDefault(workId, EmptyDisplayInfo), editionInfo);
    }

    /// <summary>
    /// Blocks adding a wish for a book already in "Имам я" (LibraryItems),
    /// checked by title and ISBN. Sold/GivenAway items don't count — the user
    /// no longer has those, so wanting one again is legitimate. An exact ISBN
    /// match always blocks. A title match blocks too, UNLESS the caller named
    /// a language that is known to differ from every owned copy's language —
    /// owning a Bulgarian edition doesn't make wanting the English one a
    /// duplicate, but we only know that when both languages are actually known.
    /// </summary>
    private async Task EnsureNotAlreadyOwnedAsync(string? title, string? isbn13, string? language, Guid userId, CancellationToken ct)
    {
        Isbn? requestedIsbn = null;
        if (!string.IsNullOrWhiteSpace(isbn13))
        {
            var isbnResult = Isbn.TryCreate(isbn13);
            if (isbnResult.IsSuccess)
                requestedIsbn = isbnResult.Value;
        }

        if (requestedIsbn is null && string.IsNullOrWhiteSpace(title))
            return;

        var owned = await (
            from li in db.LibraryItems
            join e in db.Editions on li.EditionId equals e.Id
            join w in db.Works on e.WorkId equals w.Id
            where li.UserId == userId && li.Status != OwnershipStatus.Sold && li.Status != OwnershipStatus.GivenAway
            select new { w.Id, w.Title, e.Isbn13, e.Language })
            .AsNoTracking()
            .ToListAsync(ct);

        if (requestedIsbn is not null)
        {
            var isbnMatch = owned.FirstOrDefault(o => o.Isbn13 == requestedIsbn);
            if (isbnMatch is not null)
                throw AlreadyOwned(isbnMatch.Id, isbnMatch.Title);
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var trimmedTitle = title.Trim();
            var titleMatches = owned.Where(o => string.Equals(o.Title.Trim(), trimmedTitle, StringComparison.OrdinalIgnoreCase)).ToList();

            if (titleMatches.Count > 0)
            {
                var knownToBeDifferentLanguage = !string.IsNullOrWhiteSpace(language)
                    && titleMatches.All(o => !string.IsNullOrWhiteSpace(o.Language) && !string.Equals(o.Language, language, StringComparison.OrdinalIgnoreCase));

                if (!knownToBeDifferentLanguage)
                    throw AlreadyOwned(titleMatches[0].Id, titleMatches[0].Title);
            }
        }
    }

    private static ConflictException AlreadyOwned(Guid workId, string title) =>
        new("wishlist_entry.already_owned",
            $"You already have '{title}' in your library.",
            new Dictionary<string, object?> { ["existingWorkId"] = workId, ["existingWorkTitle"] = title });

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
