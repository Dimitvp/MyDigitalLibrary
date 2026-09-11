using FluentAssertions;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Infrastructure.Bookstores;

namespace MyDigitalLibrary.Api.IntegrationTests.Bookstores;

/// <summary>
/// Fast, network-free tests against real markup captured from helikon.bg and
/// ciela.com while building Stage 8 (both verified robots.txt-permitted —
/// see the Stage 8 history entry) — no Docker/Testcontainers dependency.
/// </summary>
public class SchemaOrgBookstoreAdapterTests
{
    private static readonly DateTimeOffset CheckedAt = DateTimeOffset.UtcNow;
    private static readonly BookstoreAdapterOptions NoFallback = new();

    [Fact]
    public async Task Extracts_in_stock_offer_from_a_real_helikon_book_page()
    {
        // Trimmed from a real product page fetched 2026-09-11: https://www.helikon.bg/243437-Prislujnicata.html
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
                "@context":"https://schema.org",
                "@type":["Book","Product"],
                "@id":"urn:isbn:9786190302537",
                "name" : "Прислужницата",
                "isbn":"9786190302537",
                "offers":{
                    "@type":"Offer",
                    "priceCurrency":"EUR",
                    "availability": "https://schema.org/InStock",
                    "price":"12.99"
                }
            }
            </script>
            </head><body></body></html>
            """;

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, NoFallback, CheckedAt, CancellationToken.None);

        offer!.Availability.Should().Be(MarketAvailability.InStock);
        offer.Price!.Amount.Should().Be(12.99m);
        offer.Price.CurrencyCode.Should().Be("EUR");
    }

    [Fact]
    public async Task Extracts_out_of_stock_offer_from_a_real_ciela_book_page()
    {
        // Trimmed from a real product page fetched 2026-09-11: https://www.ciela.com/nakazatelen-kodeks.html
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
                "@context": "http://schema.org",
                "@type": "Book",
                "name": "Наказателен кодекс",
                "offers": {
                    "@type": "Offer",
                    "priceCurrency": "EUR",
                    "price": "1.27823",
                    "availability": "https://schema.org/OutOfStock"
                }
            }
            </script>
            </head><body></body></html>
            """;

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, NoFallback, CheckedAt, CancellationToken.None);

        offer!.Availability.Should().Be(MarketAvailability.OutOfStock);
        offer.Price!.Amount.Should().Be(1.28m, "rounded to 2 decimal places");
    }

    [Fact]
    public async Task Ignores_json_ld_blocks_with_no_offers_and_falls_through()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">{ "@type": "Organization", "name": "Some Store" }</script>
            </head><body></body></html>
            """;

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, NoFallback, CheckedAt, CancellationToken.None);

        offer.Should().BeNull("no offers node and no fallback selectors configured");
    }

    [Fact]
    public async Task Malformed_json_ld_does_not_throw_and_falls_through()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">{ not valid json </script>
            </head><body></body></html>
            """;

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, NoFallback, CheckedAt, CancellationToken.None);

        offer.Should().BeNull();
    }

    [Fact]
    public async Task Falls_back_to_configured_selectors_when_no_json_ld_is_present()
    {
        const string html = """
            <html><body>
            <div class="availability">В наличност</div>
            <div class="price">25.50 лв.</div>
            </body></html>
            """;

        var options = new BookstoreAdapterOptions
        {
            AvailabilitySelector = ".availability",
            PriceSelector = ".price",
            InStockMarkers = ["в наличност"],
            OutOfStockMarkers = ["изчерпана"],
        };

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, options, CheckedAt, CancellationToken.None);

        offer!.Availability.Should().Be(MarketAvailability.InStock);
        offer.Price!.Amount.Should().Be(25.50m);
        offer.Price.CurrencyCode.Should().Be("BGN");
    }

    [Fact]
    public async Task Falls_back_selector_recognizes_out_of_stock_markers()
    {
        const string html = """<html><body><div class="availability">Изчерпана бройка</div></body></html>""";
        var options = new BookstoreAdapterOptions
        {
            AvailabilitySelector = ".availability",
            InStockMarkers = ["в наличност"],
            OutOfStockMarkers = ["изчерпана"],
        };

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, options, CheckedAt, CancellationToken.None);

        offer!.Availability.Should().Be(MarketAvailability.OutOfStock);
    }

    [Fact]
    public async Task No_json_ld_and_no_fallback_selector_configured_returns_null()
    {
        const string html = "<html><body><p>Just a regular page.</p></body></html>";

        var offer = await SchemaOrgBookstoreAdapter.ExtractOfferAsync(html, NoFallback, CheckedAt, CancellationToken.None);

        offer.Should().BeNull();
    }
}
