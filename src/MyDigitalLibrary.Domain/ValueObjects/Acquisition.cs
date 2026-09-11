using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.ValueObjects;

public sealed record Acquisition(
    DateOnly AcquiredOn,
    AcquisitionMethod Method,
    Money? Price,
    string? Source)
{
    // For EF Core materialization only: Price is itself an owned type and
    // cannot be bound through a constructor parameter, so EF uses this and
    // sets every property directly instead.
    private Acquisition() : this(default, default, null, null)
    {
    }
}
