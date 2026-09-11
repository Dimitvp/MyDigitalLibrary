namespace MyDigitalLibrary.Domain.Reading;

/// <summary>
/// The "total size" of an edition, expressed the way that edition's format
/// measures it — page count for print/ebook, duration for audio. Used to turn
/// a <see cref="ProgressPoint"/> into a completion fraction for statistics.
/// </summary>
public abstract record EditionExtent
{
    public static EditionExtent FromPageCount(int pageCount) => new PageCountExtent(pageCount);

    public static EditionExtent FromDuration(TimeSpan duration) => new DurationExtent(duration);
}

public sealed record PageCountExtent(int PageCount) : EditionExtent;

public sealed record DurationExtent(TimeSpan Duration) : EditionExtent;
