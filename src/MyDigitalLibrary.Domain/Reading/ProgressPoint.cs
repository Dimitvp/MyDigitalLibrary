using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Reading;

public abstract record ProgressPoint
{
    public abstract BookFormat[] ValidFor { get; }

    /// <summary>
    /// Projects this progress point onto [0, 1] given the edition's extent.
    /// Returns null rather than guessing when the extent is missing or the
    /// wrong kind for this progress point.
    /// </summary>
    public abstract decimal? ToFraction(EditionExtent? extent);
}
