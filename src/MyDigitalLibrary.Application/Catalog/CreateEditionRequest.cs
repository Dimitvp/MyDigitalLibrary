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
    int? DurationMinutes,
    /// <summary>Plan section 5.6 — carries a cover URL found by import (e.g. from a metadata provider) through to the composite create. Downloaded in the background; never fetched synchronously here.</summary>
    Uri? CoverUrl = null);
