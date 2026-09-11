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
    IReadOnlyList<AuthorSummaryDto> Authors,
    int? MyRating,
    string? MyReview);

public sealed record UpdateWorkRequest(string Title, string? OriginalTitle, string? Description, int? FirstPublicationYear);

/// <summary>Score is 1..10 (plan section 14 Q1, answered during Stage 1 — see WorkRating.MinScore/MaxScore).</summary>
public sealed record UpsertRatingRequest(int Score);

public sealed record UpsertReviewRequest(string Text);
