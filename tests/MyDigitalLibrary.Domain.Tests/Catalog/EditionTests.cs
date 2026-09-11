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
}
