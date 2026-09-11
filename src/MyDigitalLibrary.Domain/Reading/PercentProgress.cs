using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Reading;

public sealed record PercentProgress : ProgressPoint
{
    public decimal Percent { get; }

    public PercentProgress(decimal percent)
    {
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 and 100.");

        Percent = percent;
    }

    public override BookFormat[] ValidFor => [BookFormat.Ebook];

    // Already self-contained as a fraction of the whole; no extent needed.
    public override decimal? ToFraction(EditionExtent? extent) => Percent / 100m;
}
