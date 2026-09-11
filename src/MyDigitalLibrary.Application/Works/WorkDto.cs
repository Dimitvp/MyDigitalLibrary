namespace MyDigitalLibrary.Application.Works;

public sealed record AuthorSummaryDto(Guid Id, string FullName);

public sealed record WorkSummaryDto(Guid Id, string Title, string? OriginalTitle, int? FirstPublicationYear, IReadOnlyList<string> AuthorNames);

public sealed record WorkDetailDto(
    Guid Id,
    string Title,
    string? OriginalTitle,
    string? Description,
    int? FirstPublicationYear,
    Guid? SeriesId,
    decimal? SeriesPosition,
    IReadOnlyList<AuthorSummaryDto> Authors);

public sealed record UpdateWorkRequest(string Title, string? OriginalTitle, string? Description, int? FirstPublicationYear);
