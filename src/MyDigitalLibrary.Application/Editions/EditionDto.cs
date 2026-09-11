using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Editions;

public sealed record EditionDto(
    Guid Id,
    Guid WorkId,
    BookFormat Format,
    string? Isbn13,
    string? Publisher,
    string? Language,
    string? Translator,
    int? PublicationYear,
    int? PageCount,
    CoverType CoverType,
    string? CoverImageUrl,
    string? Narrator,
    int? DurationMinutes);

public sealed record UpdateEditionRequest(
    string? Isbn13,
    string? Publisher,
    string? Language,
    string? Translator,
    int? PublicationYear,
    int? PageCount,
    CoverType CoverType,
    string? Narrator,
    int? DurationMinutes);

/// <summary>
/// Raw shape of an EF projection — only directly-mapped/converted properties,
/// no sub-member access on converted types (that can't translate to SQL).
/// Unwrapped into <see cref="EditionDto"/> after materialization.
/// </summary>
internal sealed record EditionRow(
    Guid Id,
    Guid WorkId,
    BookFormat Format,
    Isbn? Isbn13,
    string? Publisher,
    string? Language,
    string? Translator,
    int? PublicationYear,
    int? PageCount,
    CoverType CoverType,
    Uri? CoverImageUrl,
    string? Narrator,
    AudioDuration? Duration)
{
    public EditionDto ToDto() => new(
        Id, WorkId, Format, Isbn13?.Value, Publisher, Language, Translator,
        PublicationYear, PageCount, CoverType, CoverImageUrl?.ToString(), Narrator,
        Duration is null ? null : (int)Duration.Value.TotalMinutes);
}
