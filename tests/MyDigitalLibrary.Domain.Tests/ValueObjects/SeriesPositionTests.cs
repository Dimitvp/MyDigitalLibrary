using FluentAssertions;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.ValueObjects;

public class SeriesPositionTests
{
    [Fact]
    public void Constructor_accepts_a_fractional_position()
    {
        var position = new SeriesPosition(2.5m);

        position.Value.Should().Be(2.5m);
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_position()
    {
        var act = () => new SeriesPosition(0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
