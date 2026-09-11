namespace MyDigitalLibrary.Application.Import;

/// <summary>
/// Metadata for a single ISBN as reported by one provider (plan section 5.4).
/// Provider-specific DTOs never leave Infrastructure — this is the only shape
/// Application/Api ever see.
/// </summary>
public sealed record BookMetadataCandidate(
    string ProviderKey,
    string? Title,
    string? OriginalTitle,
    IReadOnlyList<string> AuthorNames,
    string? Publisher,
    int? PublicationYear,
    string? Language,
    int? PageCount,
    string? Description,
    Uri? CoverUrl,
    string? SeriesName,
    decimal? SeriesPosition,
    IReadOnlyList<string> Genres);
