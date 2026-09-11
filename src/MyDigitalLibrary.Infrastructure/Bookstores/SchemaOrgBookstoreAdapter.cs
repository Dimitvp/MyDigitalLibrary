using System.Globalization;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using MyDigitalLibrary.Application.Bookstores;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Bookstores;

public sealed class BookstoreAdapterOptions
{
    /// <summary>Fallback only — every bookstore verified so far (helikon, ciela) exposes schema.org Book/Offer JSON-LD, which is tried first and needs no selectors at all. Selectors matter only for a future bookstore that doesn't.</summary>
    public string? PriceSelector { get; set; }
    public string? AvailabilitySelector { get; set; }
    public List<string> InStockMarkers { get; set; } = [];
    public List<string> OutOfStockMarkers { get; set; } = [];
}

/// <summary>
/// Plan section 6.1. Deliberately not a per-bookstore class: every real
/// listing page verified while building this (helikon.bg, ciela.com — see
/// the Stage 8 history entry for why ozone.bg and orangecenter.bg were
/// excluded) already publishes a standard schema.org Book/Offer JSON-LD
/// block, which is tried first and is far more robust than CSS selectors
/// (survives a redesign that only touches markup, not the structured data).
/// The plan's own CSS-selector design is kept as a fallback for a future
/// bookstore that doesn't publish JSON-LD.
/// </summary>
public sealed partial class SchemaOrgBookstoreAdapter(
    string adapterKey, HttpClient http, BookstoreAdapterOptions options, ILogger<SchemaOrgBookstoreAdapter> logger) : IBookstoreAdapter
{
    public string AdapterKey { get; } = adapterKey;

    public async Task<OfferSnapshot?> FetchAsync(Uri listingUrl, CancellationToken ct)
    {
        string html;
        try
        {
            html = await http.GetStringAsync(listingUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch {ListingUrl} for bookstore {AdapterKey}.", listingUrl, AdapterKey);
            return null;
        }

        return await ExtractOfferAsync(html, options, DateTimeOffset.UtcNow, ct);
    }

    /// <summary>The parsing logic, separated from the HTTP fetch so it's unit-testable against static HTML fixtures without a live network dependency.</summary>
    public static async Task<OfferSnapshot?> ExtractOfferAsync(string html, BookstoreAdapterOptions options, DateTimeOffset checkedAt, CancellationToken ct)
    {
        using var context = BrowsingContext.New(Configuration.Default);
        using var document = await new HtmlParser(default, context).ParseDocumentAsync(html, ct);

        return TryFromSchemaOrgOffer(document, checkedAt) ?? TryFromConfiguredSelectors(document, options, checkedAt);
    }

    private static OfferSnapshot? TryFromSchemaOrgOffer(IDocument document, DateTimeOffset checkedAt)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var snapshot = TryParseJsonLd(script.TextContent, checkedAt);
            if (snapshot is not null)
                return snapshot;
        }

        return null;
    }

    private static OfferSnapshot? TryParseJsonLd(string json, DateTimeOffset checkedAt)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(json);
            return jsonDoc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => jsonDoc.RootElement.EnumerateArray().Select(e => TryFromOfferNode(e, checkedAt)).FirstOrDefault(s => s is not null),
                JsonValueKind.Object => TryFromOfferNode(jsonDoc.RootElement, checkedAt),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OfferSnapshot? TryFromOfferNode(JsonElement node, DateTimeOffset checkedAt)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Object)
            return null;

        var availability = MarketAvailability.Unknown;
        if (offers.TryGetProperty("availability", out var availabilityProp) && availabilityProp.ValueKind == JsonValueKind.String)
        {
            var raw = availabilityProp.GetString() ?? "";
            availability = ParseAvailability(raw);
        }

        return new OfferSnapshot(availability, TryReadPrice(offers), checkedAt);
    }

    // schema.org Offer.availability values are URLs like "https://schema.org/InStock".
    private static MarketAvailability ParseAvailability(string raw)
    {
        if (raw.Contains("InStock", StringComparison.OrdinalIgnoreCase) || raw.Contains("LimitedAvailability", StringComparison.OrdinalIgnoreCase))
            return MarketAvailability.InStock;
        if (raw.Contains("OutOfStock", StringComparison.OrdinalIgnoreCase) || raw.Contains("SoldOut", StringComparison.OrdinalIgnoreCase))
            return MarketAvailability.OutOfStock;
        if (raw.Contains("Discontinued", StringComparison.OrdinalIgnoreCase))
            return MarketAvailability.Discontinued;

        return MarketAvailability.Unknown;
    }

    private static Money? TryReadPrice(JsonElement offers)
    {
        if (!offers.TryGetProperty("price", out var priceProp) || !offers.TryGetProperty("priceCurrency", out var currencyProp))
            return null;

        decimal? amount = priceProp.ValueKind switch
        {
            JsonValueKind.Number => priceProp.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(priceProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };

        var currency = currencyProp.GetString();

        // Money validates amount >= 0 and a 3-letter code; a malformed page
        // shouldn't crash the whole fetch, just skip the price.
        return amount is { } value && currency is { Length: 3 } && value >= 0
            ? new Money(Math.Round(value, 2), currency)
            : null;
    }

    private static OfferSnapshot? TryFromConfiguredSelectors(IDocument document, BookstoreAdapterOptions options, DateTimeOffset checkedAt)
    {
        if (string.IsNullOrWhiteSpace(options.AvailabilitySelector))
            return null;

        var availabilityText = document.QuerySelector(options.AvailabilitySelector)?.TextContent ?? "";
        var availability = MarketAvailability.Unknown;

        if (options.InStockMarkers.Any(marker => availabilityText.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            availability = MarketAvailability.InStock;
        else if (options.OutOfStockMarkers.Any(marker => availabilityText.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            availability = MarketAvailability.OutOfStock;

        Money? price = null;
        if (!string.IsNullOrWhiteSpace(options.PriceSelector))
        {
            var priceText = document.QuerySelector(options.PriceSelector)?.TextContent;
            price = TryParsePriceText(priceText);
        }

        return new OfferSnapshot(availability, price, checkedAt);
    }

    private static Money? TryParsePriceText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = PriceAmountRegex().Match(text);
        if (!match.Success || !decimal.TryParse(match.Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return null;

        // No currency selector in the plan's config shape — BGN is the
        // storefront default for every bookstore this targets.
        return new Money(Math.Round(amount, 2), "BGN");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\d+([.,]\d+)?")]
    private static partial System.Text.RegularExpressions.Regex PriceAmountRegex();
}
