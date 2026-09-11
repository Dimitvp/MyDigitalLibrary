using FluentAssertions;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.ValueObjects;

public class AudioDurationTests
{
    [Fact]
    public void Constructor_rejects_zero_or_negative_duration()
    {
        var act = () => new AudioDuration(TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_accepts_a_positive_duration()
    {
        var duration = new AudioDuration(TimeSpan.FromHours(10));

        duration.Value.Should().Be(TimeSpan.FromHours(10));
    }
}
