namespace MyDigitalLibrary.Application.Statistics;

public sealed record AuthorBookCount(string AuthorName, int Count);

public sealed record StatisticsDto(
    int Year,
    int TotalLibraryItems,
    IReadOnlyDictionary<string, int> ByFormat,
    IReadOnlyDictionary<string, int> ByStatus,
    int BooksFinishedThisYear,
    int PagesReadThisYear,
    int CurrentlyReadingCount,
    double? AverageRating,
    IReadOnlyList<AuthorBookCount> TopAuthors);
