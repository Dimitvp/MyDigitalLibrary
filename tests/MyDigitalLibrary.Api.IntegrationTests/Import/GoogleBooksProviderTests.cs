using FluentAssertions;
using MyDigitalLibrary.Infrastructure.Import;

namespace MyDigitalLibrary.Api.IntegrationTests.Import;

/// <summary>Tests GoogleBooksProvider.ToCandidate against the real Volume schema verified via the API's own discovery document (2026-09-11) — no network access needed.</summary>
public class GoogleBooksProviderTests
{
    [Fact]
    public void Maps_a_real_google_books_volumeinfo_shape()
    {
        var info = new GoogleVolumeInfo
        {
            Title = "Dune",
            Authors = ["Frank Herbert"],
            Publisher = "Ace Books",
            PublishedDate = "2005-08-02",
            Description = "A desert planet epic.",
            PageCount = 528,
            Language = "en",
            Categories = ["Fiction / Science Fiction"],
            ImageLinks = new GoogleImageLinks { Thumbnail = "http://books.google.com/books/content?id=xyz&printsec=frontcover" },
        };

        var candidate = GoogleBooksProvider.ToCandidate(info);

        candidate.ProviderKey.Should().Be("google-books");
        candidate.Title.Should().Be("Dune");
        candidate.AuthorNames.Should().BeEquivalentTo(["Frank Herbert"]);
        candidate.Publisher.Should().Be("Ace Books");
        candidate.PublicationYear.Should().Be(2005);
        candidate.PageCount.Should().Be(528);
        candidate.Language.Should().Be("en");
        candidate.Genres.Should().BeEquivalentTo(["Fiction / Science Fiction"]);
    }

    [Fact]
    public void Upgrades_an_http_cover_link_to_https_to_avoid_mixed_content()
    {
        var info = new GoogleVolumeInfo { ImageLinks = new GoogleImageLinks { Thumbnail = "http://books.google.com/cover.jpg" } };

        var candidate = GoogleBooksProvider.ToCandidate(info);

        candidate.CoverUrl.Should().Be(new Uri("https://books.google.com/cover.jpg"));
    }

    [Fact]
    public void Falls_back_to_small_thumbnail_when_thumbnail_is_absent()
    {
        var info = new GoogleVolumeInfo { ImageLinks = new GoogleImageLinks { SmallThumbnail = "http://books.google.com/small.jpg" } };

        var candidate = GoogleBooksProvider.ToCandidate(info);

        candidate.CoverUrl.Should().Be(new Uri("https://books.google.com/small.jpg"));
    }

    [Fact]
    public void Missing_fields_map_to_empty_or_null_not_an_exception()
    {
        var candidate = GoogleBooksProvider.ToCandidate(new GoogleVolumeInfo());

        candidate.AuthorNames.Should().BeEmpty();
        candidate.Genres.Should().BeEmpty();
        candidate.CoverUrl.Should().BeNull();
    }
}
