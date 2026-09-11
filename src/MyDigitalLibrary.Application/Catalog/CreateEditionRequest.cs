using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Application.Catalog;

public sealed record CreateEditionRequest(
    BookFormat Format,
    string? Isbn13,
    string? Publisher,
    string? Language,
    string? Translator,
    int? PublicationYear,
    int? PageCount,
    CoverType? CoverType,
    string? Narrator,
    int? DurationMinutes);
