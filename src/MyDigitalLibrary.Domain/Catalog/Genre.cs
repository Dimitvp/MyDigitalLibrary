using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class Genre : Entity
{
    public string Name { get; private set; }
    public Guid? ParentGenreId { get; private set; }

    public Genre(string name, Guid? parentGenreId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
        ParentGenreId = parentGenreId;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
    }

    public void MoveTo(Guid? parentGenreId) => ParentGenreId = parentGenreId;
}
