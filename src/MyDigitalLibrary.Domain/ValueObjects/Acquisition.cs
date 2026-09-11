using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.ValueObjects;

public sealed record Acquisition(
    DateOnly AcquiredOn,
    AcquisitionMethod Method,
    Money? Price,
    string? Source);
