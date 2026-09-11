using FluentAssertions;
using MyDigitalLibrary.Application.Import;
using MyDigitalLibrary.Domain.ValueObjects;
using NSubstitute;

namespace MyDigitalLibrary.Application.Tests.Import;

public class CompositeBookMetadataProviderTests
{
    private static readonly Isbn Isbn = Isbn.TryCreate("9780441013593").Value;

    [Fact]
    public async Task Queries_every_registered_provider_and_returns_all_non_null_results_as_alternates()
    {
        var openLibrary = Substitute.For<IBookMetadataProvider>();
        openLibrary.ProviderKey.Returns("open-library");
        openLibrary.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>())
            .Returns(new BookMetadataCandidate("open-library", "Dune", null, ["Frank Herbert"], null, null, null, null, null, null, null, null, []));

        var googleBooks = Substitute.For<IBookMetadataProvider>();
        googleBooks.ProviderKey.Returns("google-books");
        googleBooks.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>())
            .Returns(new BookMetadataCandidate("google-books", "Dune", null, ["Frank Herbert"], "Ace Books", null, null, null, null, null, null, null, []));

        var composite = new CompositeBookMetadataProvider([openLibrary, googleBooks]);

        var (merged, alternates) = await composite.LookupByIsbnAsync(Isbn, CancellationToken.None);

        alternates.Should().HaveCount(2);
        merged!.Publisher.Should().Be("Ace Books", "only google-books had it");
    }

    [Fact]
    public async Task A_provider_returning_null_is_skipped_not_treated_as_an_error()
    {
        var openLibrary = Substitute.For<IBookMetadataProvider>();
        openLibrary.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>()).Returns((BookMetadataCandidate?)null);

        var googleBooks = Substitute.For<IBookMetadataProvider>();
        googleBooks.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>())
            .Returns(new BookMetadataCandidate("google-books", "Dune", null, [], null, null, null, null, null, null, null, null, []));

        var composite = new CompositeBookMetadataProvider([openLibrary, googleBooks]);

        var (merged, alternates) = await composite.LookupByIsbnAsync(Isbn, CancellationToken.None);

        alternates.Should().HaveCount(1);
        merged!.ProviderKey.Should().Be("google-books");
    }

    [Fact]
    public async Task No_provider_having_data_returns_a_null_merged_result()
    {
        var provider = Substitute.For<IBookMetadataProvider>();
        provider.LookupByIsbnAsync(Isbn, Arg.Any<CancellationToken>()).Returns((BookMetadataCandidate?)null);

        var composite = new CompositeBookMetadataProvider([provider]);

        var (merged, alternates) = await composite.LookupByIsbnAsync(Isbn, CancellationToken.None);

        merged.Should().BeNull();
        alternates.Should().BeEmpty();
    }
}
