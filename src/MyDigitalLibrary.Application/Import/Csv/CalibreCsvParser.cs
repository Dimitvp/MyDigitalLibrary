using System.Globalization;
using CsvHelper;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Application.Import.Csv;

/// <summary>
/// Plan section 9's "CSV импорт от Calibre". Calibre's catalog CSV export
/// lets the user pick which columns to include (Convert -> Catalog -> CSV),
/// so there's no single fixed format the way Goodreads has — this reads by
/// column name against calibredb's own documented field names (verified
/// 2026-09-11): title, authors, isbn, publisher, pubdate, series,
/// series_index, rating, comments, tags. Every field is read defensively —
/// a real export may include only a subset.
///
/// One field is a genuine unverified guess, flagged here rather than
/// silently assumed: Calibre's internal rating is stored 0-10 in half-star
/// steps, but this project could not confirm whether the CSV catalog export
/// writes that raw internal value or a human 0-5 value — no real exported
/// file was available to check against. The raw numeric value is passed
/// through as-is (clamped to 1..10), which is correct if the export uses
/// Calibre's internal scale and wrong (off by 2x) if it doesn't. Verify
/// against a real export before trusting imported ratings.
/// </summary>
public static class CalibreCsvParser
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
            var title = GetField(csv, "title");
            if (string.IsNullOrWhiteSpace(title))
                throw new FormatException("Row has no title.");

            var authorsRaw = GetField(csv, "authors");
            var authorNames = string.IsNullOrWhiteSpace(authorsRaw)
                ? []
                : authorsRaw.Split(['&', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

            var isbn = GetField(csv, "isbn");
            var publisher = GetField(csv, "publisher");
            var year = TryParseYear(GetField(csv, "pubdate"));
            var seriesName = GetField(csv, "series");
            var seriesPosition = TryParseDecimal(GetField(csv, "series_index"));
            var comments = GetField(csv, "comments");

            var rawRating = TryParseInt(GetField(csv, "rating"));
            var rating = rawRating is > 0 ? Math.Clamp(rawRating.Value, 1, 10) : (int?)null;

            // Calibre manages ebooks — no format column maps cleanly to
            // Physical/Ebook/Audiobook, so every imported row is Ebook. The
            // user can correct individual items afterward via PUT /library-items.
            rows.Add(new ParsedBookRow(
                title, authorNames, isbn, publisher, year, null, BookFormat.Ebook, false,
                null, rating, string.IsNullOrWhiteSpace(comments) ? null : comments, seriesName, seriesPosition));
        }

        return rows;
    }

    private static string? GetField(CsvReader csv, string name) => csv.TryGetField<string>(name, out var value) ? value : null;

    private static int? TryParseYear(string? pubdate)
    {
        if (string.IsNullOrWhiteSpace(pubdate))
            return null;

        if (DateOnly.TryParse(pubdate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.Year;

        // pubdate that's just a bare year ("1965"), not a full date.
        return int.TryParse(pubdate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ? year : null;
    }

    private static int? TryParseInt(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static decimal? TryParseDecimal(string? raw)
        => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;
}
