using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Reading;

public sealed record PageProgress : ProgressPoint
{
    public int Page { get; }

    public PageProgress(int page)
    {
        if (page < 0)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must not be negative.");

        Page = page;
    }

    public override BookFormat[] ValidFor => [BookFormat.Physical, BookFormat.Ebook];

    public override decimal? ToFraction(EditionExtent? extent)
        => extent is PageCountExtent { PageCount: > 0 } pageCount
            ? Page / (decimal)pageCount.PageCount
            : null;
}
