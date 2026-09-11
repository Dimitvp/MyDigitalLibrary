using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class Series : Entity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }

    public Series(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
        Description = description;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
        Description = description;
    }
}
