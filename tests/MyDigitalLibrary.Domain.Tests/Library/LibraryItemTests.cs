using FluentAssertions;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.Library;

public class LibraryItemTests
{
    private static LibraryItem CreateItem(BookFormat format) =>
        new(Guid.NewGuid(), Guid.NewGuid(), format, new Acquisition(DateOnly.FromDateTime(DateTime.Today), AcquisitionMethod.Bought, null, null));

    [Fact]
    public void SetLocation_rejects_a_location_on_a_non_physical_item()
    {
        var item = CreateItem(BookFormat.Ebook);

        var act = () => item.SetLocation(new PhysicalLocation("Office", null, null));

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "library_item.location_requires_physical");
    }

    [Fact]
    public void SetLocation_accepts_a_location_on_a_physical_item()
    {
        var item = CreateItem(BookFormat.Physical);

        item.SetLocation(new PhysicalLocation("Office", "Shelf 3", null));

        item.Location!.Shelf.Should().Be("Shelf 3");
    }

    [Fact]
    public void SetLocation_of_null_is_always_allowed()
    {
        var item = CreateItem(BookFormat.Ebook);

        var act = () => item.SetLocation(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void ChangeFormat_clears_location_when_switching_away_from_physical()
    {
        var item = CreateItem(BookFormat.Physical);
        item.SetLocation(new PhysicalLocation("Office", "Shelf 3", null));

        item.ChangeFormat(BookFormat.Ebook);

        item.Format.Should().Be(BookFormat.Ebook);
        item.Location.Should().BeNull();
    }

    [Fact]
    public void ChangeFormat_to_the_same_format_is_a_no_op()
    {
        var item = CreateItem(BookFormat.Physical);
        item.SetLocation(new PhysicalLocation("Office", "Shelf 3", null));

        item.ChangeFormat(BookFormat.Physical);

        item.Location.Should().NotBeNull("an unchanged format must not clear the location");
    }

    [Fact]
    public void UpdateAcquisition_replaces_the_acquisition()
    {
        var item = CreateItem(BookFormat.Physical);
        var corrected = new Acquisition(new DateOnly(2020, 1, 1), AcquisitionMethod.Gift, null, "Birthday gift");

        item.UpdateAcquisition(corrected);

        item.Acquisition.Should().Be(corrected);
    }
}
