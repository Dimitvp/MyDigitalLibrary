using FluentAssertions;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Domain.Tests.Library;

public class WorkRatingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Constructor_rejects_a_score_outside_1_to_10(int score)
    {
        var act = () => new WorkRating(Guid.NewGuid(), Guid.NewGuid(), score, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_accepts_the_full_range()
    {
        var low = new WorkRating(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        var high = new WorkRating(Guid.NewGuid(), Guid.NewGuid(), 10, DateTimeOffset.UtcNow);

        low.Score.Should().Be(1);
        high.Score.Should().Be(10);
    }
}
