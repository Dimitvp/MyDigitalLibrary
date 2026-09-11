using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Application.Reading;

public sealed record ProgressEntryDto(Guid Id, DateTimeOffset RecordedAt, string Kind, int? Page, decimal? Percent, int? PositionMinutes);

public sealed record ReadingSessionDto(
    Guid Id,
    Guid UserId,
    Guid LibraryItemId,
    BookFormat Format,
    DateOnly StartedOn,
    ReadingStatus Status,
    DateOnly? EndedOn,
    string? AbandonReason,
    IReadOnlyList<ProgressEntryDto> Progress);

public sealed record StartReadingSessionRequest(Guid LibraryItemId, DateOnly? StartedOn);

/// <summary>oneOf: exactly one of Page (Physical/Ebook), Percent (Ebook), or PositionMinutes (Audiobook) — see ProgressPoint.ValidFor.</summary>
public sealed record RecordProgressRequest(int? Page, decimal? Percent, int? PositionMinutes);

public sealed record FinishReadingSessionRequest(DateOnly? EndedOn);

public sealed record AbandonReadingSessionRequest(DateOnly? EndedOn, string? Reason);

public static class ReadingSessionMapper
{
    public static ReadingSessionDto ToDto(ReadingSession session) => new(
        session.Id, session.UserId, session.LibraryItemId, session.Format,
        session.StartedOn, session.Status, session.EndedOn, session.AbandonReason,
        session.Progress.Select(ToDto).ToList());

    private static ProgressEntryDto ToDto(ProgressEntry entry) => entry.Point switch
    {
        PageProgress p => new ProgressEntryDto(entry.Id, entry.RecordedAt, "page", p.Page, null, null),
        PercentProgress p => new ProgressEntryDto(entry.Id, entry.RecordedAt, "percent", null, p.Percent, null),
        TimestampProgress p => new ProgressEntryDto(entry.Id, entry.RecordedAt, "timestamp", null, null, (int)p.Position.TotalMinutes),
        _ => throw new InvalidOperationException($"Unknown progress point type '{entry.Point.GetType().Name}'."),
    };
}
