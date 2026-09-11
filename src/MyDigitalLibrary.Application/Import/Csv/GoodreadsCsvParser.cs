using System.Globalization;
using CsvHelper;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Application.Import.Csv;

/// <summary>
/// Plan section 9's "CSV импорт от Goodreads". Columns and the ISBN quirk
/// verified against real documentation of Goodreads' export format (2026-09-11):
/// "Book Id, Title, Author, Author l-f, Additional Authors, ISBN, ISBN13, My
/// Rating, Average Rating, Publisher, Binding, Number of Pages, Year
/// Published, Original Publication Year, Date Read, Date Added, Bookshelves,
/// Bookshelves with positions, Exclusive Shelf, My Review, Spoiler, Private
/// Notes, Read Count[, Recommended For, Recommended By, Owned Copies,
/// Original Purchase Date, Original Purchase Location, Condition, Condition
/// Description, BCID]" — the bracketed columns were removed from exports
/// created after mid-2022, so every field is read defensively (missing ->
/// null), never assumed present.
/// </summary>
public static class GoodreadsCsvParser
{
    public static IReadOnlyList<ParsedBookRow> Parse(string csvText)
    {
        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();

        var rows = new List<ParsedBookRow>();

        while (csv.Read())
        {
            var title = GetField(csv, "Title");
            if (string.IsNullOrWhiteSpace(title))
                throw new FormatException("Row has no Title.");

            var authorNames = new List<string>();
            if (GetField(csv, "Author") is { } author && !string.IsNullOrWhiteSpace(author))
                authorNames.Add(author);
            if (GetField(csv, "Additional Authors") is { } additional && !string.IsNullOrWhiteSpace(additional))
                authorNames.AddRange(additional.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

            var isbn = StripExcelFormulaWrapper(GetField(csv, "ISBN13")) ?? StripExcelFormulaWrapper(GetField(csv, "ISBN"));

            var pageCount = TryParseInt(GetField(csv, "Number of Pages"));
            var year = TryParseInt(GetField(csv, "Year Published")) ?? TryParseInt(GetField(csv, "Original Publication Year"));
            var publisher = GetField(csv, "Publisher");
            var format = MapBindingToFormat(GetField(csv, "Binding"));

            var wantToRead = string.Equals(GetField(csv, "Exclusive Shelf"), "to-read", StringComparison.OrdinalIgnoreCase);

            // Goodreads' My Rating is 0 (unrated) to 5 stars; this app's WorkRating is 1..10 (Stage 1) — doubled, and 0 (unrated) maps to null, not a real rating.
            var goodreadsRating = TryParseInt(GetField(csv, "My Rating"));
            int? rating = goodreadsRating is > 0 ? Math.Clamp(goodreadsRating.Value * 2, 1, 10) : null;

            var review = GetField(csv, "My Review");
            var acquiredOn = TryParseDate(GetField(csv, "Date Added"));

            rows.Add(new ParsedBookRow(
                title, authorNames, isbn, publisher, year, pageCount, format, wantToRead,
                acquiredOn, rating, string.IsNullOrWhiteSpace(review) ? null : review, null, null));
        }

        return rows;
    }

    private static string? GetField(CsvReader csv, string name) => csv.TryGetField<string>(name, out var value) ? value : null;

    // Goodreads wraps ISBN/ISBN13 as ="1234567890" (an Excel formula) so
    // spreadsheet apps don't mangle leading zeros or switch to scientific
    // notation — this has to be stripped before Isbn.TryCreate ever sees it.
    private static string? StripExcelFormulaWrapper(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("=\"", StringComparison.Ordinal) && trimmed.EndsWith('"'))
            trimmed = trimmed[2..^1];

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static BookFormat MapBindingToFormat(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return BookFormat.Physical;

        var lower = binding.ToLowerInvariant();
        if (lower.Contains("kindle") || lower.Contains("ebook") || lower.Contains("nook"))
            return BookFormat.Ebook;
        if (lower.Contains("audio"))
            return BookFormat.Audiobook;

        return BookFormat.Physical;
    }

    private static int? TryParseInt(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static DateOnly? TryParseDate(string? raw)
        => DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
}
