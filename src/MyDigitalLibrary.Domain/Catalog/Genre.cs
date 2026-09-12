using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class Genre : Entity
{
    /// <summary>Primary name — the name genres are resolved-or-created by (see BookCatalogService.ResolveOrCreateGenreAsync). Historically Bulgarian, but not enforced.</summary>
    public string Name { get; private set; }

    /// <summary>Optional English display name, set separately via the rename endpoint — never affects resolve-or-create matching, which is always by <see cref="Name"/>.</summary>
    public string? NameEn { get; private set; }

    public Guid? ParentGenreId { get; private set; }

    public Genre(string name, Guid? parentGenreId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
        ParentGenreId = parentGenreId;
    }

    public void Rename(string name, string? nameEn = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
    }

    public void MoveTo(Guid? parentGenreId) => ParentGenreId = parentGenreId;
}
