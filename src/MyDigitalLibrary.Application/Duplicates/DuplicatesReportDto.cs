namespace MyDigitalLibrary.Application.Duplicates;

public sealed record DuplicateWorkGroup(string Title, IReadOnlyList<Guid> WorkIds);

public sealed record DuplicateLibraryItemGroup(Guid EditionId, string WorkTitle, int Count, IReadOnlyList<Guid> LibraryItemIds);

public sealed record DuplicatesReportDto(
    IReadOnlyList<DuplicateWorkGroup> DuplicateWorks,
    IReadOnlyList<DuplicateLibraryItemGroup> DuplicateLibraryItems);
