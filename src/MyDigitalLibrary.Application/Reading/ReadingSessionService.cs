using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Application.Reading;

public sealed class ReadingSessionService(IApplicationDbContext db)
{
    public async Task<ReadingSessionDto> StartAsync(StartReadingSessionRequest request, Guid userId, CancellationToken ct)
    {
        var libraryItem = await db.LibraryItems.AsNoTracking()
            .FirstOrDefaultAsync(li => li.Id == request.LibraryItemId && li.UserId == userId, ct)
            ?? throw new NotFoundException("library_item.not_found", $"Library item '{request.LibraryItemId}' was not found.");

        var session = new ReadingSession(userId, libraryItem.Id, libraryItem.Format, request.StartedOn ?? Today());

        db.ReadingSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return ReadingSessionMapper.ToDto(session);
    }

    public async Task<ReadingSessionDto> RecordProgressAsync(Guid id, RecordProgressRequest request, Guid userId, CancellationToken ct)
    {
        var session = await LoadTrackedAsync(id, userId, ct);

        ProgressPoint point = (request.Page, request.Percent, request.PositionMinutes) switch
        {
            ({ } page, null, null) => new PageProgress(page),
            (null, { } percent, null) => new PercentProgress(percent),
            (null, null, { } minutes) => new TimestampProgress(TimeSpan.FromMinutes(minutes)),
            _ => throw new AppValidationException(
                "reading_session.progress_value_required",
                "Provide exactly one of page, percent, or positionMinutes."),
        };

        session.RecordProgress(point, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        return ReadingSessionMapper.ToDto(session);
    }

    public async Task<ReadingSessionDto> FinishAsync(Guid id, FinishReadingSessionRequest request, Guid userId, CancellationToken ct)
    {
        var session = await LoadTrackedAsync(id, userId, ct);

        session.Finish(request.EndedOn ?? Today());
        await db.SaveChangesAsync(ct);

        return ReadingSessionMapper.ToDto(session);
    }

    public async Task<ReadingSessionDto> AbandonAsync(Guid id, AbandonReadingSessionRequest request, Guid userId, CancellationToken ct)
    {
        var session = await LoadTrackedAsync(id, userId, ct);

        session.Abandon(request.EndedOn ?? Today(), request.Reason);
        await db.SaveChangesAsync(ct);

        return ReadingSessionMapper.ToDto(session);
    }

    public async Task<ReadingSessionDto> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var session = await db.ReadingSessions.AsNoTracking().Include(s => s.Progress)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("reading_session.not_found", $"Reading session '{id}' was not found.");

        return ReadingSessionMapper.ToDto(session);
    }

    public async Task<IReadOnlyList<ReadingSessionDto>> ListAsync(Guid libraryItemId, Guid userId, CancellationToken ct)
    {
        var sessions = await db.ReadingSessions.AsNoTracking().Include(s => s.Progress)
            .Where(s => s.LibraryItemId == libraryItemId && s.UserId == userId)
            .OrderByDescending(s => s.StartedOn)
            .ToListAsync(ct);

        return sessions.Select(ReadingSessionMapper.ToDto).ToList();
    }

    private async Task<ReadingSession> LoadTrackedAsync(Guid id, Guid userId, CancellationToken ct)
        => await db.ReadingSessions.Include(s => s.Progress).FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("reading_session.not_found", $"Reading session '{id}' was not found.");

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
