using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Domain.Enums;
using DomainLibraryItem = MyDigitalLibrary.Domain.Library.LibraryItem;

namespace MyDigitalLibrary.Application.LibraryItems;

public sealed record MoneyDto(decimal Amount, string CurrencyCode);

public sealed record AcquisitionDto(DateOnly AcquiredOn, AcquisitionMethod Method, MoneyDto? Price, string? Source);

public sealed record PhysicalLocationDto(string? Room, string? Shelf, string? Box);

public sealed record LibraryItemDto(
    Guid Id,
    Guid UserId,
    Guid EditionId,
    BookFormat Format,
    OwnershipStatus Status,
    AcquisitionDto Acquisition,
    PhysicalLocationDto? Location,
    string? PersonalNote);

/// <summary>Nested edition fields for the composite create payload — Format comes
/// from the request's top-level Format, not repeated here (plan section 4.1).</summary>
public sealed record NestedEditionInput(
    string? Isbn13,
    string? Publisher,
    string? Language,
    string? Translator,
    int? PublicationYear,
    int? PageCount,
    CoverType? CoverType,
    string? Narrator,
    int? DurationMinutes);

/// <summary>
/// oneOf: either EditionId (existing edition) or both Work and Edition
/// (create a new Work + Edition together) — never both, never neither.
/// </summary>
public sealed record CreateLibraryItemRequest(
    Guid? EditionId,
    CreateWorkRequest? Work,
    NestedEditionInput? Edition,
    BookFormat Format,
    OwnershipStatus? Status,
    AcquisitionDto Acquisition,
    PhysicalLocationDto? Location);

public sealed record UpdateLibraryItemRequest(AcquisitionDto Acquisition, string? PersonalNote);

public sealed record UpdateLocationRequest(PhysicalLocationDto? Location);

public sealed record UpdateStatusRequest(OwnershipStatus Status);

public static class LibraryItemMapper
{
    public static LibraryItemDto ToDto(DomainLibraryItem item) => new(
        item.Id, item.UserId, item.EditionId, item.Format, item.Status,
        new AcquisitionDto(
            item.Acquisition.AcquiredOn,
            item.Acquisition.Method,
            item.Acquisition.Price is null ? null : new MoneyDto(item.Acquisition.Price.Amount, item.Acquisition.Price.CurrencyCode),
            item.Acquisition.Source),
        item.Location is null ? null : new PhysicalLocationDto(item.Location.Room, item.Location.Shelf, item.Location.Box),
        item.PersonalNote);
}
