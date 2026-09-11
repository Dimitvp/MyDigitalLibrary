namespace MyDigitalLibrary.Domain.ValueObjects;

// Decimal, not int, because of entries like "book 2.5" (a novella between two volumes).
public sealed record SeriesPosition
{
    public decimal Value { get; }

    public SeriesPosition(decimal value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Series position must be positive.");

        Value = value;
    }
}
