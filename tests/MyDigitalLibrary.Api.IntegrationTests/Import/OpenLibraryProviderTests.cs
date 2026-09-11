using FluentAssertions;
using MyDigitalLibrary.Infrastructure.Import;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

/// <summary>Tests OpenLibraryProvider.ToCandidate against the real response shape verified by hand against the live API (2026-09-11) — no network access needed.</summary>
public class OpenLibraryProviderTests
{
    [Fact]
    public void Maps_a_real_open_library_response_shape()
    {
        var data = new OpenLibraryBookData
        {
            Title = "Dune",
            NumberOfPages = 528,
            Authors = [new OpenLibraryAuthor { Name = "Frank Herbert" }],
            Publishers = [new OpenLibraryPublisher { Name = "Ace Books" }],
            PublishDate = "2005",
            Cover = new OpenLibraryCover { Large = "https://covers.openlibrary.org/b/id/14856017-L.jpg" },
        };

        var candidate = OpenLibraryProvider.ToCandidate(data);

        candidate.ProviderKey.Should().Be("open-library");
        candidate.Title.Should().Be("Dune");
        candidate.AuthorNames.Should().BeEquivalentTo(["Frank Herbert"]);
        candidate.Publisher.Should().Be("Ace Books");
        candidate.PublicationYear.Should().Be(2005);
        candidate.PageCount.Should().Be(528);
        candidate.CoverUrl.Should().Be(new Uri("https://covers.openlibrary.org/b/id/14856017-L.jpg"));
    }

    [Theory]
    [InlineData("2005", 2005)]
    [InlineData("Aug 2005", 2005)]
    [InlineData("2005-08-02", 2005)]
    [InlineData(null, null)]
    [InlineData("undated", null)]
    public void Extracts_a_publication_year_from_open_librarys_free_text_publish_date(string? publishDate, int? expectedYear)
    {
        var candidate = OpenLibraryProvider.ToCandidate(new OpenLibraryBookData { PublishDate = publishDate });

        candidate.PublicationYear.Should().Be(expectedYear);
    }

    [Fact]
    public void Missing_fields_map_to_empty_or_null_not_an_exception()
    {
        var candidate = OpenLibraryProvider.ToCandidate(new OpenLibraryBookData());

        candidate.AuthorNames.Should().BeEmpty();
        candidate.Publisher.Should().BeNull();
        candidate.CoverUrl.Should().BeNull();
    }
}
