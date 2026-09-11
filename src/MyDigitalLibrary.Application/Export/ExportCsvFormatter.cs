using System.Globalization;
using System.Text;

namespace MyDigitalLibrary.Application.Export;

/// <summary>
/// CSV covers the library items only (the core catalog) — a single flat CSV
/// can't represent the whole nested archive (shelves, notes, sessions, ...)
/// the way the JSON export does; that's what GET /export?format=json is for.
/// </summary>
public static class ExportCsvFormatter
{
    private static readonly string[] Header =
    [
        "Title", "Authors", "Isbn13", "Format", "Status", "AcquiredOn", "AcquisitionMethod",
        "PriceAmount", "PriceCurrency", "Source", "PersonalNote", "MyRating", "MyReview",
    ];

    public static string ToCsv(IReadOnlyList<ExportLibraryItem> items)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Header.Select(Escape))).Append("\r\n");

        foreach (var item in items)
        {
            string?[] fields =
            [
                item.WorkTitle,
                string.Join("; ", item.AuthorNames),
                item.Isbn13,
                item.Format,
                item.Status,
                item.AcquiredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.AcquisitionMethod,
                item.PriceAmount?.ToString(CultureInfo.InvariantCulture),
                item.PriceCurrency,
                item.Source,
                item.PersonalNote,
                item.MyRating?.ToString(CultureInfo.InvariantCulture),
                item.MyReview,
            ];

            sb.Append(string.Join(',', fields.Select(f => Escape(f ?? "")))).Append("\r\n");
        }

        return sb.ToString();
    }

    // RFC 4180: a field containing a comma, double quote, or line break must be
    // wrapped in double quotes, with any embedded double quote doubled.
    private static string Escape(string field)
        => field.IndexOfAny([',', '"', '\r', '\n']) < 0 ? field : $"\"{field.Replace("\"", "\"\"")}\"";
}
