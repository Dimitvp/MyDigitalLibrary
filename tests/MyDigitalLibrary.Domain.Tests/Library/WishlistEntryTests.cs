using FluentAssertions;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.Library;

public class WishlistEntryTests
{
    [Fact]
    public void Fulfill_creates_a_library_item_with_the_given_acquisition_and_closes_the_wish()
    {
        var userId = Guid.NewGuid();
        var workId = Guid.NewGuid();
        var editionId = Guid.NewGuid();
        var entry = new WishlistEntry(userId, workId, BookFormat.Ebook, priority: 3, addedOn: DateOnly.FromDateTime(DateTime.Today));
        var acquisition = new Acquisition(DateOnly.FromDateTime(DateTime.Today), AcquisitionMethod.Bought, new Money(9.99m, "EUR"), "Humble Bundle");

        var item = entry.Fulfill(editionId, acquisition);

        item.UserId.Should().Be(userId);
        item.EditionId.Should().Be(editionId);
        item.Format.Should().Be(BookFormat.Ebook);
        item.Acquisition.Should().Be(acquisition);
        entry.IsFulfilled.Should().BeTrue();
    }

    [Fact]
    public void Fulfill_rejects_being_called_twice()
    {
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Physical, priority: 1, addedOn: DateOnly.FromDateTime(DateTime.Today));
        var acquisition = new Acquisition(DateOnly.FromDateTime(DateTime.Today), AcquisitionMethod.Bought, null, null);
        entry.Fulfill(Guid.NewGuid(), acquisition);

        var act = () => entry.Fulfill(Guid.NewGuid(), acquisition);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "wishlist_entry.already_fulfilled");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_rejects_priority_outside_1_to_5(int priority)
    {
        var act = () => new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Physical, priority, DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
