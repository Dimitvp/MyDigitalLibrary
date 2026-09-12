namespace MyDigitalLibrary.Application.Catalog;

/// <summary>
/// Nested "create a new Work" shape used by the composite create endpoints
/// (POST /library-items, POST /wishlist) — see plan section 4.1. Authors and
/// series are matched by name, creating them if they don't already exist.
/// </summary>
public sealed record CreateWorkRequest(
    string Title,
    string? OriginalTitle,
    string? Description,
    int? FirstPublicationYear,
    IReadOnlyList<string>? AuthorNames,
    string? SeriesName,
    decimal? SeriesPosition,
    IReadOnlyList<string>? GenreNames = null);
