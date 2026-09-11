using FluentAssertions;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Domain.Tests.Library;

public class ShelfTests
{
    [Fact]
    public void AddItem_rejects_adding_the_same_item_twice()
    {
        var shelf = new Shelf(Guid.NewGuid(), "Currently Reading");
        var libraryItemId = Guid.NewGuid();
        shelf.AddItem(libraryItemId, DateOnly.FromDateTime(DateTime.Today));

        var act = () => shelf.AddItem(libraryItemId, DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "shelf.item_already_present");
    }

    [Fact]
    public void AddItem_assigns_increasing_sort_order()
    {
        var shelf = new Shelf(Guid.NewGuid(), "Currently Reading");

        shelf.AddItem(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today));
        shelf.AddItem(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today));

        shelf.Items.Select(i => i.SortOrder).Should().BeEquivalentTo([0, 1]);
    }

    [Fact]
    public void Rename_rejects_renaming_a_system_shelf()
    {
        var shelf = new Shelf(Guid.NewGuid(), "Finished", isSystem: true);

        var act = () => shelf.Rename("My Finished Books");

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "shelf.system_shelf_immutable");
    }
}
