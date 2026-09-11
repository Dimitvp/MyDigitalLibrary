namespace MyDigitalLibrary.Domain.ValueObjects;

public sealed record AudioDuration
{
    public TimeSpan Value { get; }

    public AudioDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value), "Audio duration must be positive.");

        Value = value;
    }
}
