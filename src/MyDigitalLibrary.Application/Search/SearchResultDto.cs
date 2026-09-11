namespace MyDigitalLibrary.Application.Search;

public sealed record SearchResultDto(Guid WorkId, string Title, IReadOnlyList<string> AuthorNames);
