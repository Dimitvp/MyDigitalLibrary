using FluentAssertions;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Domain.Tests.Library;

public class FollowedBookSourceTests
{
    [Fact]
    public void Constructor_rejects_a_blank_name()
    {
        var act = () => new FollowedBookSource(Guid.NewGuid(), "  ", "https://example.com", DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_a_blank_url()
    {
        var act = () => new FollowedBookSource(Guid.NewGuid(), "Ciela", "  ", DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_changes_the_editable_fields()
    {
        var source = new FollowedBookSource(Guid.NewGuid(), "Ciela", "https://ciela.com", DateOnly.FromDateTime(DateTime.Today));

        source.Update("Ciela.com", "https://www.ciela.com", "bookstore", "Chain of physical stores too");

        source.Name.Should().Be("Ciela.com");
        source.Url.Should().Be("https://www.ciela.com");
        source.Category.Should().Be("bookstore");
        source.Notes.Should().Be("Chain of physical stores too");
    }
}
