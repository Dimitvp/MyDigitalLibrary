using FluentAssertions;
using MyDigitalLibrary.Application.Import.Csv;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

/// <summary>Column names and the ISBN ="..." quirk verified against real Goodreads export documentation (2026-09-11) — see GoodreadsCsvParser's own doc comment. No network dependency.</summary>
public class GoodreadsCsvParserTests
{
    [Fact]
    public void Parses_title_authors_isbn_and_strips_the_excel_formula_wrapper()
    {
        // Goodreads wraps ISBN/ISBN13 in an Excel formula (="...") to stop
        // spreadsheet apps mangling leading zeros — real RFC 4180 escaping for
        // a field containing quotes wraps the whole field and doubles the
        // inner quotes, so the raw CSV text is "=""0441013597""", not ="0441013597".
        // (A plain C# string, not a raw-string literal: this text contains
        // runs of three embedded double quotes, which collide with a """
        // raw-string delimiter.)
        const string csv =
            "Book Id,Title,Author,Additional Authors,ISBN,ISBN13,My Rating,Average Rating,Publisher,Binding,Number of Pages,Year Published,Original Publication Year,Date Read,Date Added,Bookshelves,Bookshelves with positions,Exclusive Shelf,My Review,Spoiler,Private Notes,Read Count\n"
            + "1,Dune,Frank Herbert,,\"=\"\"0441013597\"\"\",\"=\"\"9780441013593\"\"\",5,4.25,Ace Books,Paperback,412,1990,1965,,2020/01/15,,,read,Great book,,,1";

        var rows = GoodreadsCsvParser.Parse(csv);

        var row = rows.Single();
        row.Title.Should().Be("Dune");
        row.AuthorNames.Should().BeEquivalentTo(["Frank Herbert"]);
        row.Isbn.Should().Be("9780441013593", "ISBN13 is preferred over ISBN, and the =\"...\" Excel wrapper must be stripped");
        row.Publisher.Should().Be("Ace Books");
        row.PageCount.Should().Be(412);
        row.PublicationYear.Should().Be(1990);
        row.Format.Should().Be(BookFormat.Physical);
        row.WantToRead.Should().BeFalse();
        row.Rating.Should().Be(10, "Goodreads 5 stars * 2 = this app's 1..10 scale");
        row.Review.Should().Be("Great book");
        row.AcquiredOn.Should().Be(new DateOnly(2020, 1, 15));
    }

    [Fact]
    public void Falls_back_to_isbn_when_isbn13_is_absent()
    {
        const string csv =
            "Book Id,Title,Author,ISBN,ISBN13,My Rating,Exclusive Shelf\n"
            + "1,Some Book,An Author,\"=\"\"0441013597\"\"\",,0,to-read";

        var row = GoodreadsCsvParser.Parse(csv).Single();

        row.Isbn.Should().Be("0441013597");
    }

    [Fact]
    public void An_unrated_book_has_a_null_rating_not_zero()
    {
        const string csv = """
            Book Id,Title,Author,My Rating,Exclusive Shelf
            1,Unrated Book,An Author,0,read
            """;

        var row = GoodreadsCsvParser.Parse(csv).Single();

        row.Rating.Should().BeNull();
    }

    [Theory]
    [InlineData("Kindle Edition", BookFormat.Ebook)]
    [InlineData("ebook", BookFormat.Ebook)]
    [InlineData("Audio CD", BookFormat.Audiobook)]
    [InlineData("Audiobook", BookFormat.Audiobook)]
    [InlineData("Hardcover", BookFormat.Physical)]
    [InlineData("Paperback", BookFormat.Physical)]
    [InlineData("", BookFormat.Physical)]
    public void Maps_binding_to_format(string binding, BookFormat expected)
    {
        var csv = $"Book Id,Title,Author,Binding,Exclusive Shelf\n1,A Book,An Author,{binding},read";

        var row = GoodreadsCsvParser.Parse(csv).Single();

        row.Format.Should().Be(expected);
    }

    [Fact]
    public void The_to_read_shelf_marks_the_row_as_want_to_read()
    {
        const string csv = """
            Book Id,Title,Author,Exclusive Shelf
            1,A Wishlist Book,An Author,to-read
            """;

        var row = GoodreadsCsvParser.Parse(csv).Single();

        row.WantToRead.Should().BeTrue();
    }

    [Fact]
    public void Additional_authors_are_appended_after_the_primary_author()
    {
        const string csv = """
            Book Id,Title,Author,Additional Authors,Exclusive Shelf
            1,Good Omens,Terry Pratchett,Neil Gaiman,read
            """;

        var row = GoodreadsCsvParser.Parse(csv).Single();

        row.AuthorNames.Should().BeEquivalentTo(["Terry Pratchett", "Neil Gaiman"], options => options.WithStrictOrdering());
    }

    [Fact]
    public void A_row_with_no_title_throws_so_the_caller_counts_it_as_a_failed_row()
    {
        const string csv = """
            Book Id,Title,Author,Exclusive Shelf
            1,,An Author,read
            """;

        var act = () => GoodreadsCsvParser.Parse(csv);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Tolerates_a_file_missing_the_legacy_post_2022_columns()
    {
        const string csv =
            "Book Id,Title,Author,ISBN13,My Rating,Exclusive Shelf\n"
            + "1,Dune,Frank Herbert,\"=\"\"9780441013593\"\"\",4,read";

        var row = GoodreadsCsvParser.Parse(csv).Single();

        row.Title.Should().Be("Dune");
        row.Rating.Should().Be(8);
    }
}
