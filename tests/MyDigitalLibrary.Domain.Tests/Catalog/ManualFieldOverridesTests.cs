using FluentAssertions;
using MyDigitalLibrary.Domain.Catalog;

namespace MyDigitalLibrary.Domain.Tests.Catalog;

public class ManualFieldOverridesTests
{
    [Fact]
    public void None_has_no_overridden_fields()
    {
        ManualFieldOverrides.None.IsOverridden("title").Should().BeFalse();
        ManualFieldOverrides.None.Fields.Should().BeEmpty();
    }

    [Fact]
    public void WithOverridden_adds_the_field_without_mutating_the_original_instance()
    {
        var original = ManualFieldOverrides.None;

        var updated = original.WithOverridden("title");

        updated.IsOverridden("title").Should().BeTrue();
        original.IsOverridden("title").Should().BeFalse("value objects must not mutate in place");
    }

    [Fact]
    public void WithOverridden_is_idempotent_for_the_same_field()
    {
        var overrides = ManualFieldOverrides.None.WithOverridden("title");

        var again = overrides.WithOverridden("title");

        again.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void Instances_with_the_same_fields_are_equal()
    {
        var a = new ManualFieldOverrides(["title", "description"]);
        var b = new ManualFieldOverrides(["description", "title"]);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
