using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Import;

/// <summary>
/// Plan section 5.3 — Open Library, no API key required. Verified against the
/// real endpoint (2026-09-11): <c>GET /api/books?bibkeys=ISBN:{isbn}&amp;format=json&amp;jscmd=data</c>
/// returns a richer, more directly usable shape than <c>/isbn/{isbn}.json</c>
/// (which redirects and needs a second hop), so that's the one used here.
/// </summary>
public sealed class OpenLibraryProvider(HttpClient http, ILogger<OpenLibraryProvider> logger) : IBookMetadataProvider
{
    public string ProviderKey => "open-library";

    public async Task<BookMetadataCandidate?> LookupByIsbnAsync(Isbn isbn, CancellationToken ct)
    {
        var bibkey = $"ISBN:{isbn.Value}";

        Dictionary<string, OpenLibraryBookData>? payload;
        try
        {
            payload = await http.GetFromJsonAsync(
                $"/api/books?bibkeys={Uri.EscapeDataString(bibkey)}&format=json&jscmd=data",
                OpenLibraryJsonContext.Default.DictionaryStringOpenLibraryBookData, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Open Library lookup failed for ISBN {Isbn}.", isbn.Value);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Open Library returned unparseable JSON for ISBN {Isbn}.", isbn.Value);
            return null;
        }

        if (payload is null || !payload.TryGetValue(bibkey, out var data))
            return null;

        return ToCandidate(data);
    }

    public async Task<BookMetadataCandidate?> SearchAsync(string title, IReadOnlyList<string> authorNames, CancellationToken ct)
    {
        var query = $"/search.json?title={Uri.EscapeDataString(title)}";
        if (authorNames.Count > 0)
            query += $"&author={Uri.EscapeDataString(authorNames[0])}";
        query += "&limit=1&fields=title,author_name,first_publish_year,number_of_pages_median,cover_i,publisher";

        OpenLibrarySearchResult? result;
        try
        {
            result = await http.GetFromJsonAsync(query, OpenLibraryJsonContext.Default.OpenLibrarySearchResult, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Open Library search failed for title {Title}.", title);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Open Library returned unparseable JSON for title {Title}.", title);
            return null;
        }

        var doc = result?.Docs?.FirstOrDefault();
        return doc is null ? null : ToCandidate(doc);
    }

    /// <summary>Pure mapping, separated from the HTTP call so it's unit-testable without a live network dependency.</summary>
    public static BookMetadataCandidate ToCandidate(OpenLibrarySearchDoc doc) => new(
        ProviderKey: "open-library",
        Title: doc.Title,
        OriginalTitle: null,
        AuthorNames: doc.AuthorName ?? [],
        Publisher: doc.Publisher?.FirstOrDefault(),
        PublicationYear: doc.FirstPublishYear,
        Language: null,
        PageCount: doc.NumberOfPagesMedian,
        Description: null,
        CoverUrl: doc.CoverId is { } coverId ? new Uri($"https://covers.openlibrary.org/b/id/{coverId}-L.jpg") : null,
        SeriesName: null,
        SeriesPosition: null,
        Genres: []);

    /// <summary>Pure mapping, separated from the HTTP call so it's unit-testable without a live network dependency.</summary>
    public static BookMetadataCandidate ToCandidate(OpenLibraryBookData data) => new(
        ProviderKey: "open-library",
        Title: data.Title,
        OriginalTitle: null,
        AuthorNames: data.Authors?.Select(a => a.Name).Where(n => n is not null).Select(n => n!).ToList() ?? [],
        Publisher: data.Publishers?.Select(p => p.Name).FirstOrDefault(n => n is not null),
        PublicationYear: ExtractYear(data.PublishDate),
        Language: null,
        PageCount: data.NumberOfPages,
        Description: null,
        CoverUrl: data.Cover?.Large is { Length: > 0 } large ? TryParseUri(large) : null,
        SeriesName: null,
        SeriesPosition: null,
        Genres: []);

    private static int? ExtractYear(string? publishDate)
    {
        if (string.IsNullOrWhiteSpace(publishDate))
            return null;

        // Open Library's publish_date is free text ("2005", "Aug 2005", "2005-08-02") — take the last 4-digit run.
        for (var i = publishDate.Length - 4; i >= 0; i--)
        {
            var candidate = publishDate.Substring(i, 4);
            if (candidate.All(char.IsDigit) && int.Parse(candidate) is > 1000 and < 3000)
                return int.Parse(candidate);
        }

        return null;
    }

    private static Uri? TryParseUri(string raw) => Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
}

public sealed class OpenLibraryBookData
{
    public string? Title { get; set; }
    [JsonPropertyName("number_of_pages")] public int? NumberOfPages { get; set; }
    public List<OpenLibraryAuthor>? Authors { get; set; }
    public List<OpenLibraryPublisher>? Publishers { get; set; }
    [JsonPropertyName("publish_date")] public string? PublishDate { get; set; }
    public OpenLibraryCover? Cover { get; set; }
}

public sealed class OpenLibraryAuthor
{
    public string? Name { get; set; }
}

public sealed class OpenLibraryPublisher
{
    public string? Name { get; set; }
}

public sealed class OpenLibraryCover
{
    public string? Large { get; set; }
}

public sealed class OpenLibrarySearchResult
{
    public List<OpenLibrarySearchDoc>? Docs { get; set; }
}

public sealed class OpenLibrarySearchDoc
{
    public string? Title { get; set; }
    [JsonPropertyName("author_name")] public List<string>? AuthorName { get; set; }
    [JsonPropertyName("first_publish_year")] public int? FirstPublishYear { get; set; }
    [JsonPropertyName("number_of_pages_median")] public int? NumberOfPagesMedian { get; set; }
    [JsonPropertyName("cover_i")] public int? CoverId { get; set; }
    public List<string>? Publisher { get; set; }
}

[JsonSerializable(typeof(Dictionary<string, OpenLibraryBookData>))]
[JsonSerializable(typeof(OpenLibrarySearchResult))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class OpenLibraryJsonContext : JsonSerializerContext;
