using FluentAssertions;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Domain.ValueObjects;
using NSubstitute;

namespace MyDigitalLibrary.Application.Tests.Import;

public class CompositeBookLinkResolverTests
{
    private static readonly Uri PageUrl = new("https://example.com/book/dune");
    private static readonly Isbn Isbn = Isbn.TryCreate("9780441013593").Value;

    [Fact]
    public async Task Returns_the_first_resolvers_result_without_trying_the_rest()
    {
        var first = Substitute.For<IBookLinkResolver>();
        first.TryResolveAsync(PageUrl, Arg.Any<CancellationToken>()).Returns(Isbn);

        var second = Substitute.For<IBookLinkResolver>();

        var composite = new CompositeBookLinkResolver([first, second]);

        var result = await composite.ResolveAsync(PageUrl, CancellationToken.None);

        result.Should().Be(Isbn);
        await second.DidNotReceive().TryResolveAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_through_to_the_next_resolver_when_the_first_returns_null()
    {
        var first = Substitute.For<IBookLinkResolver>();
        first.TryResolveAsync(PageUrl, Arg.Any<CancellationToken>()).Returns((Isbn?)null);

        var second = Substitute.For<IBookLinkResolver>();
        second.TryResolveAsync(PageUrl, Arg.Any<CancellationToken>()).Returns(Isbn);

        var composite = new CompositeBookLinkResolver([first, second]);

        var result = await composite.ResolveAsync(PageUrl, CancellationToken.None);

        result.Should().Be(Isbn);
    }

    [Fact]
    public async Task Returns_null_when_no_resolver_in_the_chain_can_handle_the_page()
    {
        var resolver = Substitute.For<IBookLinkResolver>();
        resolver.TryResolveAsync(PageUrl, Arg.Any<CancellationToken>()).Returns((Isbn?)null);

        var composite = new CompositeBookLinkResolver([resolver]);

        var result = await composite.ResolveAsync(PageUrl, CancellationToken.None);

        result.Should().BeNull();
    }
}
