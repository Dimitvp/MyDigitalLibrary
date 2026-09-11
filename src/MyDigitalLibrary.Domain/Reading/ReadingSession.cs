using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Reading;

/// <summary>
/// One pass through a library item. Rereading creates a new session rather
/// than reusing this one, so reading history is never overwritten.
/// </summary>
public sealed class ReadingSession : Entity, IUserOwned
{
    public Guid UserId { get; }
    public Guid LibraryItemId { get; }

    // Captured at session start. Aggregates reference each other only by Guid
    // (no navigation properties), so without this the session would have no
    // way to validate which ProgressPoint kinds are legal to record.
    public BookFormat Format { get; }

    public DateOnly StartedOn { get; }
    public ReadingStatus Status { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public string? AbandonReason { get; private set; }

    private readonly List<ProgressEntry> _progress = [];
    public IReadOnlyList<ProgressEntry> Progress => _progress.AsReadOnly();

    public ReadingSession(Guid userId, Guid libraryItemId, BookFormat format, DateOnly startedOn)
    {
        UserId = userId;
        LibraryItemId = libraryItemId;
        Format = format;
        StartedOn = startedOn;
        Status = ReadingStatus.Reading;
    }

    public void RecordProgress(ProgressPoint point, DateTimeOffset at)
    {
        if (Status is ReadingStatus.Finished or ReadingStatus.Abandoned)
            throw new DomainException("reading_session.closed", "Cannot record progress on a finished or abandoned session.");

        if (!point.ValidFor.Contains(Format))
            throw new DomainException(
                "reading_session.progress_format_mismatch",
                $"{point.GetType().Name} is not valid for format {Format}.");

        _progress.Add(new ProgressEntry(Id, point, at));
    }

    public void Finish(DateOnly on)
    {
        if (on < StartedOn)
            throw new ArgumentOutOfRangeException(nameof(on), "End date cannot be before the start date.");

        Status = ReadingStatus.Finished;
        EndedOn = on;
    }

    public void Abandon(DateOnly on, string? reason)
    {
        if (on < StartedOn)
            throw new ArgumentOutOfRangeException(nameof(on), "End date cannot be before the start date.");

        Status = ReadingStatus.Abandoned;
        EndedOn = on;
        AbandonReason = reason;
    }
}
