using FluentAssertions;
using MyDigitalLibrary.Infrastructure.Import;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

/// <summary>
/// Fast, network-free tests of the four ISBN-extraction strategies (plan section
/// 5.2), against static HTML fixtures — no Docker/Testcontainers dependency,
/// despite living in this project (Infrastructure.Import types are public and
/// this is the only project with a reference chain into Infrastructure).
/// </summary>
public class GenericIsbnPageResolverTests
{
    [Fact]
    public async Task Extracts_isbn_from_json_ld_book_markup()
    {
        const string html = """
            <html><body>
            <script type="application/ld+json">
            { "@context": "https://schema.org", "@type": "Book", "name": "Dune", "isbn": "9780441013593" }
            </script>
            </body></html>
            """;

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn!.Value.Should().Be("9780441013593");
    }

    [Fact]
    public async Task Ignores_json_ld_blocks_for_a_different_type()
    {
        const string html = """
            <html><body>
            <script type="application/ld+json">
            { "@context": "https://schema.org", "@type": "Organization", "isbn": "9780441013593" }
            </script>
            </body></html>
            """;

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn.Should().BeNull();
    }

    [Fact]
    public async Task Extracts_isbn_from_a_books_isbn_meta_tag()
    {
        const string html = """<html><head><meta property="books:isbn" content="9780441013593"></head><body></body></html>""";

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn!.Value.Should().Be("9780441013593");
    }

    [Fact]
    public async Task Extracts_isbn_from_an_itemprop_meta_tag()
    {
        const string html = """<html><head><meta itemprop="isbn" content="9780441013593"></head><body></body></html>""";

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn!.Value.Should().Be("9780441013593");
    }

    [Fact]
    public async Task Extracts_isbn_from_microdata()
    {
        const string html = """<html><body><span itemprop="isbn">9780441013593</span></body></html>""";

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn!.Value.Should().Be("9780441013593");
    }

    [Fact]
    public async Task Falls_back_to_a_regex_over_the_visible_text()
    {
        const string html = "<html><body><p>Details: ISBN: 978-0-441-01359-3, paperback.</p></body></html>";

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn!.Value.Should().Be("9780441013593");
    }

    [Fact]
    public async Task Rejects_a_checksum_invalid_isbn_found_in_the_text()
    {
        const string html = "<html><body><p>ISBN: 978-0-441-01359-9 (looks like an ISBN but the checksum is wrong)</p></body></html>";

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn.Should().BeNull();
    }

    [Fact]
    public async Task Returns_null_when_no_strategy_finds_anything()
    {
        const string html = "<html><body><p>Just a regular page with no book metadata at all.</p></body></html>";

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn.Should().BeNull();
    }

    [Fact]
    public async Task Json_ld_takes_priority_over_a_conflicting_meta_tag()
    {
        const string html = """
            <html><head><meta property="books:isbn" content="9780316769488"></head>
            <body>
            <script type="application/ld+json">{ "@type": "Book", "isbn": "9780441013593" }</script>
            </body></html>
            """;

        var isbn = await GenericIsbnPageResolver.TryExtractIsbnAsync(html, CancellationToken.None);

        isbn!.Value.Should().Be("9780441013593", "JSON-LD is tried before meta tags");
    }
}
