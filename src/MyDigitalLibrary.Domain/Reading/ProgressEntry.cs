using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Reading;

public sealed class ProgressEntry : Entity
{
    public Guid ReadingSessionId { get; }
    public ProgressPoint Point { get; }
    public DateTimeOffset RecordedAt { get; }

    internal ProgressEntry(Guid readingSessionId, ProgressPoint point, DateTimeOffset recordedAt)
    {
        ReadingSessionId = readingSessionId;
        Point = point;
        RecordedAt = recordedAt;
    }
}
