using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Reading;

public sealed class ProgressEntry : Entity
{
    private const string PageKind = "page";
    private const string PercentKind = "percent";
    private const string TimestampKind = "timestamp";

    public Guid ReadingSessionId { get; }
    public DateTimeOffset RecordedAt { get; }

    // Point is flattened into these four fields rather than stored as a single
    // polymorphic object, so persistence can map each one to its own nullable
    // column (kind, page_value, percent_value, position_ticks) without needing
    // owned-type inheritance, which EF Core does not support.
    private readonly string _kind;
    private readonly int? _pageValue;
    private readonly decimal? _percentValue;
    private readonly long? _positionTicks;

    public ProgressPoint Point => _kind switch
    {
        PageKind => new PageProgress(_pageValue!.Value),
        PercentKind => new PercentProgress(_percentValue!.Value),
        TimestampKind => new TimestampProgress(TimeSpan.FromTicks(_positionTicks!.Value)),
        _ => throw new InvalidOperationException($"Unknown progress kind '{_kind}'."),
    };

    internal ProgressEntry(Guid readingSessionId, ProgressPoint point, DateTimeOffset recordedAt)
    {
        ReadingSessionId = readingSessionId;
        RecordedAt = recordedAt;

        (_kind, _pageValue, _percentValue, _positionTicks) = point switch
        {
            PageProgress p => (PageKind, (int?)p.Page, (decimal?)null, (long?)null),
            PercentProgress p => (PercentKind, (int?)null, (decimal?)p.Percent, (long?)null),
            TimestampProgress p => (TimestampKind, (int?)null, (decimal?)null, (long?)p.Position.Ticks),
            _ => throw new ArgumentOutOfRangeException(nameof(point), $"Unsupported progress point type '{point.GetType().Name}'."),
        };
    }

    // For EF Core materialization only: it reads/writes the four fields above
    // directly by name and never calls the constructor above.
    private ProgressEntry()
    {
        _kind = PageKind;
    }
}
