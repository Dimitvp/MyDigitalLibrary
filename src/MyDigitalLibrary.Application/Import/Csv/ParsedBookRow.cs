using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Application.Import.Csv;

/// <summary>What both GoodreadsCsvParser and CalibreCsvParser normalize a row into, so CsvImportService only needs one code path to turn a row into catalog entries.</summary>
public sealed record ParsedBookRow(
    string Title,
    IReadOnlyList<string> AuthorNames,
    string? Isbn,
    string? Publisher,
    int? PublicationYear,
    int? PageCount,
    BookFormat Format,
    bool WantToRead,
    DateOnly? AcquiredOn,
    int? Rating,
    string? Review,
    string? SeriesName,
    decimal? SeriesPosition);
