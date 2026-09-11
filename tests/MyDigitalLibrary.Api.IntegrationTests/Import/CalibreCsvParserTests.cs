using FluentAssertions;
using MyDigitalLibrary.Application.Import.Csv;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

/// <summary>Column names verified against calibredb's real documented catalog fields (2026-09-11) — see CalibreCsvParser's own doc comment, including the flagged-as-unverified rating scale. No network dependency.</summary>
public class CalibreCsvParserTests
{
    [Fact]
    public void Parses_title_authors_isbn_series_and_comments()
    {
        const string csv = """
            title,authors,isbn,publisher,pubdate,series,series_index,rating,comments,tags
            Dune,Frank Herbert,9780441013593,Ace Books,1990-01-01,Dune Chronicles,1,8,A desert planet epic.,sci-fi
            """;

        var row = CalibreCsvParser.Parse(csv).Single();

        row.Title.Should().Be("Dune");
        row.AuthorNames.Should().BeEquivalentTo(["Frank Herbert"]);
        row.Isbn.Should().Be("9780441013593");
        row.Publisher.Should().Be("Ace Books");
        row.PublicationYear.Should().Be(1990);
        row.SeriesName.Should().Be("Dune Chronicles");
        row.SeriesPosition.Should().Be(1);
        row.Rating.Should().Be(8);
        row.Review.Should().Be("A desert planet epic.");
        row.Format.Should().Be(BookFormat.Ebook, "Calibre manages ebooks; there is no format column to map from");
    }

    [Fact]
    public void Multiple_authors_are_split_on_ampersand()
    {
        const string csv = """
            title,authors
            Good Omens,Terry Pratchett & Neil Gaiman
            """;

        var row = CalibreCsvParser.Parse(csv).Single();

        row.AuthorNames.Should().BeEquivalentTo(["Terry Pratchett", "Neil Gaiman"]);
    }

    [Fact]
    public void A_bare_year_pubdate_is_accepted_as_well_as_a_full_date()
    {
        const string csv = """
            title,authors,pubdate
            A Book,An Author,1965
            """;

        var row = CalibreCsvParser.Parse(csv).Single();

        row.PublicationYear.Should().Be(1965);
    }

    [Fact]
    public void A_zero_series_index_is_treated_as_absent()
    {
        const string csv = """
            title,authors,series,series_index
            A Standalone Book,An Author,,0
            """;

        var row = CalibreCsvParser.Parse(csv).Single();

        row.SeriesPosition.Should().BeNull();
    }

    [Fact]
    public void Missing_columns_are_tolerated()
    {
        const string csv = """
            title,authors
            Minimal Book,An Author
            """;

        var row = CalibreCsvParser.Parse(csv).Single();

        row.Title.Should().Be("Minimal Book");
        row.Isbn.Should().BeNull();
        row.Rating.Should().BeNull();
    }

    [Fact]
    public void A_row_with_no_title_throws_so_the_caller_counts_it_as_a_failed_row()
    {
        const string csv = """
            title,authors
            ,An Author
            """;

        var act = () => CalibreCsvParser.Parse(csv);

        act.Should().Throw<FormatException>();
    }
}
