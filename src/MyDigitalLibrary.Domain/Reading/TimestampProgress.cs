using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Reading;

public sealed record TimestampProgress : ProgressPoint
{
    public TimeSpan Position { get; }

    public TimestampProgress(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must not be negative.");

        Position = position;
    }

    public override BookFormat[] ValidFor => [BookFormat.Audiobook];

    public override decimal? ToFraction(EditionExtent? extent)
        => extent is DurationExtent { Duration.Ticks: > 0 } durationExtent
            ? (decimal)Position.Ticks / durationExtent.Duration.Ticks
            : null;
}
