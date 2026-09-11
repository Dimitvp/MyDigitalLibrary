using System.Globalization;
using CsvHelper;

namespace MyDigitalLibrary.Application.Import.Csv;

public enum CsvSourceFormat
{
    Goodreads,
    Calibre,
}

public static class CsvFormatDetector
{
    public static CsvSourceFormat? Detect(string csvText)
    {
        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is not { } headers)
            return null;

        if (Array.Exists(headers, h => h == "Book Id") && Array.Exists(headers, h => h == "Exclusive Shelf"))
            return CsvSourceFormat.Goodreads;

        if (Array.Exists(headers, h => string.Equals(h, "title", StringComparison.OrdinalIgnoreCase))
            && Array.Exists(headers, h => string.Equals(h, "authors", StringComparison.OrdinalIgnoreCase)))
            return CsvSourceFormat.Calibre;

        return null;
    }
}
