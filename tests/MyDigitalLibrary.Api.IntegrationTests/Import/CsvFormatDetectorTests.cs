using FluentAssertions;
using MyDigitalLibrary.Application.Import.Csv;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

public class CsvFormatDetectorTests
{
    [Fact]
    public void Recognizes_a_goodreads_header_row()
    {
        const string csv = "Book Id,Title,Author,Exclusive Shelf\n1,Dune,Frank Herbert,read";

        CsvFormatDetector.Detect(csv).Should().Be(CsvSourceFormat.Goodreads);
    }

    [Fact]
    public void Recognizes_a_calibre_header_row()
    {
        const string csv = "title,authors,isbn\nDune,Frank Herbert,9780441013593";

        CsvFormatDetector.Detect(csv).Should().Be(CsvSourceFormat.Calibre);
    }

    [Fact]
    public void Returns_null_for_an_unrecognized_csv()
    {
        const string csv = "Name,Value\nfoo,bar";

        CsvFormatDetector.Detect(csv).Should().BeNull();
    }
}
