using FluentAssertions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Domain.ValueObjects;
using NSubstitute;

namespace MyDigitalLibrary.Application.Tests.Import;

public class ImportLookupServiceTests
{
    private static readonly Isbn Isbn = Isbn.TryCreate("9780441013593").Value;
    private static readonly BookMetadataCandidate Candidate =
        new("open-library", "Dune", null, ["Frank Herbert"], "Ace Books", 2005, "en", 528, null, null, null, null, []);

    [Fact]
    public async Task Given_an_isbn_directly_it_skips_link_resolution_and_looks_up_metadata()
    {
        var metadataProvider = Substitute.For<IBookMetadataProvider>();
        metadataProvider.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>()).Returns(Candidate);
        var linkResolver = Substitute.For<IBookLinkResolver>();

        var service = BuildService(linkResolver, metadataProvider);

        var result = await service.LookupAsync(new ImportLookupRequest(Url: null, Isbn: "9780441013593"), CancellationToken.None);

        result.Isbn13.Should().Be("9780441013593");
        result.Candidate.Title.Should().Be("Dune");
        await linkResolver.DidNotReceive().TryResolveAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_a_url_it_resolves_the_isbn_first_then_looks_up_metadata()
    {
        var linkResolver = Substitute.For<IBookLinkResolver>();
        linkResolver.TryResolveAsync(new Uri("https://example.com/book/dune"), Arg.Any<CancellationToken>()).Returns(Isbn);

        var metadataProvider = Substitute.For<IBookMetadataProvider>();
        metadataProvider.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>()).Returns(Candidate);

        var service = BuildService(linkResolver, metadataProvider);

        var result = await service.LookupAsync(new ImportLookupRequest(Url: "https://example.com/book/dune", Isbn: null), CancellationToken.None);

        result.Isbn13.Should().Be("9780441013593");
    }

    [Fact]
    public async Task Rejects_a_request_with_both_url_and_isbn()
    {
        var service = BuildService(Substitute.For<IBookLinkResolver>(), Substitute.For<IBookMetadataProvider>());

        var act = () => service.LookupAsync(new ImportLookupRequest("https://example.com", "9780441013593"), CancellationToken.None);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.ErrorCode.Should().Be("import.invalid_request");
    }

    [Fact]
    public async Task Rejects_a_request_with_neither_url_nor_isbn()
    {
        var service = BuildService(Substitute.For<IBookLinkResolver>(), Substitute.For<IBookMetadataProvider>());

        var act = () => service.LookupAsync(new ImportLookupRequest(null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.ErrorCode.Should().Be("import.invalid_request");
    }

    [Fact]
    public async Task Rejects_an_invalid_isbn_checksum()
    {
        var service = BuildService(Substitute.For<IBookLinkResolver>(), Substitute.For<IBookMetadataProvider>());

        var act = () => service.LookupAsync(new ImportLookupRequest(Url: null, Isbn: "0132350880"), CancellationToken.None);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.ErrorCode.Should().Be("isbn.invalid");
    }

    [Fact]
    public async Task Throws_not_found_when_the_link_resolver_chain_finds_no_isbn()
    {
        var linkResolver = Substitute.For<IBookLinkResolver>();
        linkResolver.TryResolveAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns((Isbn?)null);

        var service = BuildService(linkResolver, Substitute.For<IBookMetadataProvider>());

        var act = () => service.LookupAsync(new ImportLookupRequest("https://example.com/no-book-here", null), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be("import.isbn_not_found");
    }

    [Fact]
    public async Task Throws_not_found_when_no_provider_has_metadata_for_the_isbn()
    {
        var metadataProvider = Substitute.For<IBookMetadataProvider>();
        metadataProvider.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>()).Returns((BookMetadataCandidate?)null);

        var service = BuildService(Substitute.For<IBookLinkResolver>(), metadataProvider);

        var act = () => service.LookupAsync(new ImportLookupRequest(Url: null, Isbn: "9780441013593"), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be("import.metadata_not_found");
    }

    private static ImportLookupService BuildService(IBookLinkResolver linkResolver, IBookMetadataProvider metadataProvider)
        => new(new CompositeBookLinkResolver([linkResolver]), new CompositeBookMetadataProvider([metadataProvider]));
}
