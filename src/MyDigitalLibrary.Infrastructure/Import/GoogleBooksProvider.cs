using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Import;

public sealed class GoogleBooksOptions
{
    /// <summary>Optional — plan section 5.3: Google Books works without a key at low volumes, but an unauthenticated caller can hit a shared rate limit (verified 2026-09-11: 429 RESOURCE_EXHAUSTED from this network). Configure via .env, never commit it.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Plan section 5.3 — Google Books. Verified against the real Volumes schema via
/// the API's discovery document (2026-09-11), since the live endpoint was
/// rate-limited (429, unauthenticated quota exhausted) at verification time —
/// this confirms the plan's own warning that the "no key" path isn't fully
/// reliable in practice; a 429 is treated as "no result" here, same as any
/// other transient failure, not surfaced as an error to the caller.
/// </summary>
public sealed class GoogleBooksProvider(HttpClient http, IOptions<GoogleBooksOptions> options, ILogger<GoogleBooksProvider> logger) : IBookMetadataProvider
{
    public string ProviderKey => "google-books";

    public Task<GoogleBooksVolumes?> FetchAsync(Isbn isbn, CancellationToken ct)
        => FetchVolumesAsync($"isbn:{Uri.EscapeDataString(isbn.Value)}", isbn.Value, ct);

    public async Task<BookMetadataCandidate?> LookupByIsbnAsync(Isbn isbn, CancellationToken ct)
    {
        var volumes = await FetchAsync(isbn, ct);
        var info = volumes?.Items?.FirstOrDefault()?.VolumeInfo;
        return info is null ? null : ToCandidate(info);
    }

    public async Task<BookMetadataCandidate?> SearchAsync(string title, IReadOnlyList<string> authorNames, CancellationToken ct)
    {
        var q = $"intitle:{Uri.EscapeDataString(title)}";
        if (authorNames.Count > 0)
            q += $"+inauthor:{Uri.EscapeDataString(authorNames[0])}";

        var volumes = await FetchVolumesAsync(q, title, ct);
        var info = volumes?.Items?.FirstOrDefault()?.VolumeInfo;
        return info is null ? null : ToCandidate(info);
    }

    private async Task<GoogleBooksVolumes?> FetchVolumesAsync(string q, string logSubject, CancellationToken ct)
    {
        var query = $"/books/v1/volumes?q={q}";
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            query += $"&key={Uri.EscapeDataString(options.Value.ApiKey)}";

        try
        {
            return await http.GetFromJsonAsync(query, GoogleBooksJsonContext.Default.GoogleBooksVolumes, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google Books lookup failed for {Subject} ({StatusCode}).", logSubject, ex.StatusCode);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Google Books returned unparseable JSON for {Subject}.", logSubject);
            return null;
        }
    }

    /// <summary>Pure mapping, separated from the HTTP call so it's unit-testable without a live network dependency.</summary>
    public static BookMetadataCandidate ToCandidate(GoogleVolumeInfo info) => new(
        ProviderKey: "google-books",
        Title: info.Title,
        OriginalTitle: null,
        AuthorNames: info.Authors ?? [],
        Publisher: info.Publisher,
        PublicationYear: ExtractYear(info.PublishedDate),
        Language: info.Language,
        PageCount: info.PageCount,
        Description: info.Description,
        CoverUrl: TryUpgradeToHttps(info.ImageLinks?.Thumbnail ?? info.ImageLinks?.SmallThumbnail),
        SeriesName: null,
        SeriesPosition: null,
        Genres: info.Categories ?? []);

    private static int? ExtractYear(string? publishedDate)
        => publishedDate is { Length: >= 4 } && int.TryParse(publishedDate.AsSpan(0, 4), out var year) ? year : null;

    // Google Books' imageLinks are often http:// — browsers/pages served over https would block a mixed-content image.
    private static Uri? TryUpgradeToHttps(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var upgraded = raw.Replace("http://", "https://", StringComparison.Ordinal);
        return Uri.TryCreate(upgraded, UriKind.Absolute, out var uri) ? uri : null;
    }
}

public sealed class GoogleBooksVolumes
{
    public List<GoogleVolume>? Items { get; set; }
}

public sealed class GoogleVolume
{
    public GoogleVolumeInfo? VolumeInfo { get; set; }
}

public sealed class GoogleVolumeInfo
{
    public string? Title { get; set; }
    public List<string>? Authors { get; set; }
    public string? Publisher { get; set; }
    public string? PublishedDate { get; set; }
    public string? Description { get; set; }
    public int? PageCount { get; set; }
    public string? Language { get; set; }
    public List<string>? Categories { get; set; }
    public GoogleImageLinks? ImageLinks { get; set; }
}

public sealed class GoogleImageLinks
{
    public string? SmallThumbnail { get; set; }
    public string? Thumbnail { get; set; }
}

[JsonSerializable(typeof(GoogleBooksVolumes))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class GoogleBooksJsonContext : JsonSerializerContext;
