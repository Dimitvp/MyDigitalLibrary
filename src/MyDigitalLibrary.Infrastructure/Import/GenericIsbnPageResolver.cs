using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Import;

/// <summary>
/// Plan section 5.2 — the v1 (and, for now, only) link in the <see cref="IBookLinkResolver"/>
/// chain: fetches an arbitrary book-listing page and tries, in order, to extract a
/// checksum-valid ISBN from JSON-LD, then meta tags, then microdata, then a regex
/// over the visible text. Stops at the first strategy that yields a valid ISBN.
/// Multiple ISBNs found within one strategy (plan: "ask the user") are narrowed to
/// the first — <c>POST /import/lookup</c> returns a single candidate, not a
/// disambiguation list; a real multi-candidate flow is future work.
/// </summary>
public sealed partial class GenericIsbnPageResolver(HttpClient http, ILogger<GenericIsbnPageResolver> logger) : IBookLinkResolver
{
    private static readonly IConfiguration AngleSharpConfig = Configuration.Default;

    public async Task<Isbn?> TryResolveAsync(Uri pageUrl, CancellationToken ct)
    {
        string html;
        try
        {
            html = await http.GetStringAsync(pageUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch {PageUrl} for ISBN resolution.", pageUrl);
            return null;
        }

        return await TryExtractIsbnAsync(html, ct);
    }

    /// <summary>The four extraction strategies, separated from the HTTP fetch so they're unit-testable against static HTML fixtures without a live network dependency.</summary>
    public static async Task<Isbn?> TryExtractIsbnAsync(string html, CancellationToken ct)
    {
        using var context = BrowsingContext.New(AngleSharpConfig);
        using var document = await new HtmlParser(default, context).ParseDocumentAsync(html, ct);

        return TryFromJsonLd(document)
            ?? TryFromMetaTags(document)
            ?? TryFromMicrodata(document)
            ?? TryFromVisibleText(document);
    }

    private static Isbn? TryFromJsonLd(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var isbn = TryExtractIsbnFromJsonLd(script.TextContent);
            if (isbn is not null)
                return isbn;
        }

        return null;
    }

    private static Isbn? TryExtractIsbnFromJsonLd(string json)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(json);
            return jsonDoc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => jsonDoc.RootElement.EnumerateArray().Select(TryExtractIsbnFromBookNode).FirstOrDefault(v => v is not null),
                JsonValueKind.Object => TryExtractIsbnFromBookNode(jsonDoc.RootElement),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Isbn? TryExtractIsbnFromBookNode(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return null;

        if (node.TryGetProperty("@type", out var type) && !TypeIsBook(type))
            return null;

        if (!node.TryGetProperty("isbn", out var isbnProperty))
            return null;

        var raw = isbnProperty.ValueKind switch
        {
            JsonValueKind.String => isbnProperty.GetString(),
            JsonValueKind.Array => isbnProperty.EnumerateArray().Select(e => e.GetString()).FirstOrDefault(),
            _ => null,
        };

        return raw is null ? null : Isbn.TryCreate(raw) is { IsSuccess: true } result ? result.Value : null;
    }

    private static bool TypeIsBook(JsonElement type) => type.ValueKind switch
    {
        JsonValueKind.String => string.Equals(type.GetString(), "Book", StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Array => type.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String && string.Equals(e.GetString(), "Book", StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };

    private static Isbn? TryFromMetaTags(IDocument document)
    {
        var content = document.QuerySelector("meta[property='books:isbn']")?.GetAttribute("content")
            ?? document.QuerySelector("meta[itemprop='isbn']")?.GetAttribute("content");

        return content is null ? null : Isbn.TryCreate(content) is { IsSuccess: true } result ? result.Value : null;
    }

    private static Isbn? TryFromMicrodata(IDocument document)
    {
        foreach (var element in document.QuerySelectorAll("[itemprop='isbn']"))
        {
            var raw = element.GetAttribute("content") ?? element.TextContent;
            if (Isbn.TryCreate(raw) is { IsSuccess: true } result)
                return result.Value;
        }

        return null;
    }

    private static Isbn? TryFromVisibleText(IDocument document)
    {
        var text = document.Body?.TextContent;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (Match match in IsbnInTextRegex().Matches(text))
        {
            var candidate = match.Groups[1].Value;
            if (Isbn.TryCreate(candidate) is { IsSuccess: true } result)
                return result.Value;
        }

        return null;
    }

    // Plan section 5.2: ISBN(-10/-13)?: followed by 10-20 digits/separators, ending in a
    // digit or check-digit X. Isbn.TryCreate does the real checksum validation — this
    // regex only narrows down candidate substrings.
    [GeneratedRegex(@"ISBN(?:-1[03])?:?\s*([\d\-–\s]{10,20}[\dXx])", RegexOptions.IgnoreCase)]
    private static partial Regex IsbnInTextRegex();
}
