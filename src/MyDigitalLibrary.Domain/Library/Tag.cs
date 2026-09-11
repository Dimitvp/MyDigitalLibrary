using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

public sealed class Tag : Entity, IUserOwned
{
    public Guid UserId { get; }
    public string Name { get; private set; }

    public Tag(Guid userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        UserId = userId;
        Name = name;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
    }
}
