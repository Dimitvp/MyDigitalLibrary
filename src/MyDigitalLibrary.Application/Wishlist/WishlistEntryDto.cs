using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.LibraryItems;
using MyDigitalLibrary.Domain.Enums;
using DomainWishlistEntry = MyDigitalLibrary.Domain.Library.WishlistEntry;

namespace MyDigitalLibrary.Application.Wishlist;

public sealed record WishlistEntryDto(
    Guid Id,
    Guid UserId,
    Guid WorkId,
    Guid? PreferredEditionId,
    BookFormat DesiredFormat,
    int Priority,
    MoneyDto? MaxPrice,
    string? Note,
    DateOnly AddedOn,
    bool IsFulfilled,
    string WorkTitle,
    IReadOnlyList<string> AuthorNames,
    string? CoverImageUrl,
    string? Language,
    IReadOnlyList<string> GenreNames);

/// <summary>oneOf: either WorkId (existing work) or Work (create a new one) — see plan section 4.1.</summary>
public sealed record CreateWishlistEntryRequest(
    Guid? WorkId,
    CreateWorkRequest? Work,
    BookFormat DesiredFormat,
    int Priority,
    Guid? PreferredEditionId,
    MoneyDto? MaxPrice,
    string? Note);

public sealed record FulfillWishlistEntryRequest(Guid EditionId, AcquisitionDto Acquisition, PhysicalLocationDto? Location);

public static class WishlistEntryMapper
{
    // editionInfo is null whenever the entry has no PreferredEditionId (or it
    // doesn't resolve) — cover/language are edition-level, so they're simply
    // absent until the user picks a preferred edition.
    public static WishlistEntryDto ToDto(DomainWishlistEntry entry, WorkDisplayInfo displayInfo, EditionDisplayInfo? editionInfo) => new(
        entry.Id, entry.UserId, entry.WorkId, entry.PreferredEditionId, entry.DesiredFormat, entry.Priority,
        entry.MaxPrice is null ? null : new MoneyDto(entry.MaxPrice.Amount, entry.MaxPrice.CurrencyCode),
        entry.Note, entry.AddedOn, entry.IsFulfilled,
        displayInfo.Title, displayInfo.AuthorNames,
        editionInfo?.CoverImageUrl, editionInfo?.Language, displayInfo.GenreNames);
}
