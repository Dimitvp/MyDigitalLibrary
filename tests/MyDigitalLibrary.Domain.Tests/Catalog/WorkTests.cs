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

    [Fact]
    public void Enrich_fills_fields_from_a_provider_candidate()
    {
        var work = new Work("Dune");

        work.Enrich("Dune", "Dune (original)", "A desert planet epic.", 1965);

        work.OriginalTitle.Should().Be("Dune (original)");
        work.Description.Should().Be("A desert planet epic.");
        work.FirstPublicationYear.Should().Be(1965);
    }

    [Fact]
    public void Enrich_ignores_null_candidate_fields()
    {
        var work = new Work("Dune", description: "My own description");

        work.Enrich(title: null, originalTitle: null, description: null, firstPublicationYear: 1965);

        work.Description.Should().Be("My own description");
        work.FirstPublicationYear.Should().Be(1965);
    }

    [Fact]
    public void Manually_edited_field_survives_a_subsequent_enrichment()
    {
        // Plan section 11: required test before Stage 6 — "manually edited field survives repeat enrichment."
        var work = new Work("Working Title");
        work.Enrich("Dune", null, "First description", 1965);
        work.Title.Should().Be("Dune");

        work.UpdateDetails("My Corrected Title", work.OriginalTitle, work.Description, work.FirstPublicationYear);
        work.MarkFieldsOverridden(Work.Fields.Title);

        work.Enrich("Another Provider's Title", "Dune (alt)", "Second description", 1966);

        work.Title.Should().Be("My Corrected Title", "the manual edit must not be overwritten by re-enrichment");
        work.OriginalTitle.Should().Be("Dune (alt)", "a field that was never manually edited stays enrichable");
        work.Description.Should().Be("Second description");
        work.FirstPublicationYear.Should().Be(1966);
    }
}
