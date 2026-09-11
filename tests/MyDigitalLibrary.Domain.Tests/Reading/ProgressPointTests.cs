using FluentAssertions;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Domain.Tests.Reading;

public class ProgressPointTests
{
    [Fact]
    public void PageProgress_is_valid_for_physical_and_ebook_only()
    {
        var progress = new PageProgress(120);

        progress.ValidFor.Should().BeEquivalentTo([BookFormat.Physical, BookFormat.Ebook]);
    }

    [Fact]
    public void PercentProgress_is_valid_for_ebook_only()
    {
        var progress = new PercentProgress(50);

        progress.ValidFor.Should().BeEquivalentTo([BookFormat.Ebook]);
    }

    [Fact]
    public void TimestampProgress_is_valid_for_audiobook_only()
    {
        var progress = new TimestampProgress(TimeSpan.FromMinutes(30));

        progress.ValidFor.Should().BeEquivalentTo([BookFormat.Audiobook]);
    }

    [Fact]
    public void PageProgress_rejects_negative_page()
    {
        var act = () => new PageProgress(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void PercentProgress_rejects_values_outside_0_to_100(decimal percent)
    {
        var act = () => new PercentProgress(percent);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TimestampProgress_rejects_negative_position()
    {
        var act = () => new TimestampProgress(TimeSpan.FromSeconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PageProgress_ToFraction_returns_null_without_a_matching_extent()
    {
        var progress = new PageProgress(50);

        progress.ToFraction(null).Should().BeNull();
        progress.ToFraction(EditionExtent.FromDuration(TimeSpan.FromHours(1))).Should().BeNull();
    }

    [Fact]
    public void PageProgress_ToFraction_divides_page_by_page_count()
    {
        var progress = new PageProgress(50);

        var fraction = progress.ToFraction(EditionExtent.FromPageCount(200));

        fraction.Should().Be(0.25m);
    }

    [Fact]
    public void PercentProgress_ToFraction_ignores_the_extent()
    {
        var progress = new PercentProgress(75);

        progress.ToFraction(null).Should().Be(0.75m);
    }

    [Fact]
    public void TimestampProgress_ToFraction_divides_position_by_duration()
    {
        var progress = new TimestampProgress(TimeSpan.FromMinutes(30));

        var fraction = progress.ToFraction(EditionExtent.FromDuration(TimeSpan.FromHours(2)));

        fraction.Should().Be(0.25m);
    }
}
