using FluentAssertions;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Reading;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.Catalog;

public class EditionTests
{
    [Fact]
    public void SetPublicationDetails_rejects_page_count_on_an_audiobook()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Audiobook);

        var act = () => edition.SetPublicationDetails("Publisher", "en", null, 2020, pageCount: 300);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "edition.page_count_requires_print_or_ebook");
    }

    [Fact]
    public void SetIsbn_rejects_an_isbn_on_an_audiobook()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Audiobook);
        var isbn = Isbn.TryCreate("9780441013593").Value;

        var act = () => edition.SetIsbn(isbn);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "edition.isbn_requires_print_or_ebook");
    }

    [Fact]
    public void SetIsbn_allows_clearing_an_audiobooks_isbn()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Audiobook);

        var act = () => edition.SetIsbn(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetCoverType_rejects_a_real_cover_type_on_a_non_physical_edition()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Ebook);

        var act = () => edition.SetCoverType(CoverType.Paperback);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "edition.cover_type_requires_physical");
    }

    [Fact]
    public void SetAudioDetails_rejects_narrator_on_a_physical_edition()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Physical);

        var act = () => edition.SetAudioDetails("Some Narrator", null);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "edition.audio_details_require_audiobook");
    }

    [Fact]
    public void Extent_reflects_page_count_for_print_editions()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Physical);
        edition.SetPublicationDetails(null, null, null, null, pageCount: 412);

        edition.Extent.Should().Be(EditionExtent.FromPageCount(412));
    }

    [Fact]
    public void Extent_reflects_duration_for_audiobooks()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Audiobook);
        edition.SetAudioDetails("Narrator", new AudioDuration(TimeSpan.FromHours(12)));

        edition.Extent.Should().Be(EditionExtent.FromDuration(TimeSpan.FromHours(12)));
    }

    [Fact]
    public void Enrich_fills_fields_from_a_provider_candidate()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Physical);

        edition.Enrich("Ace Books", "en", null, 2005, 528, new Uri("https://covers.example/dune.jpg"), null, null);

        edition.Publisher.Should().Be("Ace Books");
        edition.Language.Should().Be("en");
        edition.PublicationYear.Should().Be(2005);
        edition.PageCount.Should().Be(528);
        edition.CoverImageUrl.Should().Be(new Uri("https://covers.example/dune.jpg"));
    }

    [Fact]
    public void Enrich_never_sets_page_count_on_an_audiobook()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Audiobook);

        edition.Enrich(null, null, null, null, pageCount: 528, null, "Narrator", new AudioDuration(TimeSpan.FromHours(10)));

        edition.PageCount.Should().BeNull();
        edition.Narrator.Should().Be("Narrator");
    }

    [Fact]
    public void Enrich_never_sets_narrator_or_duration_on_a_physical_edition()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Physical);

        edition.Enrich(null, null, null, null, null, null, "Narrator", new AudioDuration(TimeSpan.FromHours(10)));

        edition.Narrator.Should().BeNull();
        edition.Duration.Should().BeNull();
    }

    [Fact]
    public void Manually_edited_publisher_survives_a_subsequent_enrichment()
    {
        var edition = new Edition(Guid.NewGuid(), BookFormat.Physical);
        edition.Enrich("Ace Books", "en", null, 2005, 528, null, null, null);

        edition.SetPublicationDetails("My Local Publisher", edition.Language, edition.Translator, edition.PublicationYear, edition.PageCount);
        edition.MarkFieldsOverridden(Edition.Fields.Publisher);

        edition.Enrich("A Different Publisher", "bg", null, 2006, 704, null, null, null);

        edition.Publisher.Should().Be("My Local Publisher", "the manual edit must not be overwritten by re-enrichment");
        edition.Language.Should().Be("bg", "a field that was never manually edited stays enrichable");
        edition.PublicationYear.Should().Be(2006);
        edition.PageCount.Should().Be(704);
    }
}
