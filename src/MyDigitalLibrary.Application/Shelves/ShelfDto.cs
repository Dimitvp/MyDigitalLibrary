namespace MyDigitalLibrary.Application.Shelves;

public sealed record ShelfSummaryDto(Guid Id, string Name, bool IsSystem, int ItemCount);

public sealed record ShelfItemDto(
    Guid LibraryItemId,
    DateOnly AddedOn,
    int SortOrder,
    string WorkTitle,
    IReadOnlyList<string> AuthorNames,
    string? CoverImageUrl);

public sealed record ShelfDetailDto(Guid Id, string Name, bool IsSystem, IReadOnlyList<ShelfItemDto> Items);

public sealed record CreateShelfRequest(string Name);

public sealed record RenameShelfRequest(string Name);

public sealed record AddShelfItemRequest(Guid LibraryItemId);

public sealed record ReorderShelfItemRequest(int SortOrder);
