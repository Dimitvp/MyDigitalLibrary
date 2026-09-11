using FluentAssertions;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.Catalog;

public class WorkTests
{
    [Fact]
    public void Constructor_rejects_a_blank_title()
    {
        var act = () => new Work("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddAuthor_rejects_the_same_author_and_role_twice()
    {
        var work = new Work("Dune");
        var authorId = Guid.NewGuid();
        work.AddAuthor(authorId, WorkAuthorRole.Author);

        var act = () => work.AddAuthor(authorId, WorkAuthorRole.Author);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "work.author_already_added");
    }

    [Fact]
    public void AddAuthor_allows_the_same_author_with_a_different_role()
    {
        var work = new Work("Dune");
        var authorId = Guid.NewGuid();
        work.AddAuthor(authorId, WorkAuthorRole.Author);

        work.AddAuthor(authorId, WorkAuthorRole.Illustrator);

        work.Authors.Should().HaveCount(2);
    }

    [Fact]
    public void AssignToSeries_then_RemoveFromSeries_clears_both_fields()
    {
        var work = new Work("Dune Messiah");
        var seriesId = Guid.NewGuid();
        work.AssignToSeries(seriesId, new SeriesPosition(2));

        work.RemoveFromSeries();

        work.SeriesId.Should().BeNull();
        work.PositionInSeries.Should().BeNull();
    }
}
