using FluentAssertions;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.ValueObjects;

public class PhysicalLocationTests
{
    [Fact]
    public void Constructor_rejects_all_fields_being_null()
    {
        var act = () => new PhysicalLocation(null, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_accepts_at_least_one_field_set()
    {
        var location = new PhysicalLocation("Office", null, null);

        location.Room.Should().Be("Office");
    }
}
