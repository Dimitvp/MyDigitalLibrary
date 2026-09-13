using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

/// <summary>
/// A bookstore, publisher, or library the user browses for new books — as
/// opposed to <see cref="WishlistEntry"/>, which is a specific book. Kept
/// deliberately free-text (no adapter/availability tracking like
/// <c>BookstoreListing</c>) since the point is just "remember to check this
/// site again", not scrape it.
/// </summary>
public sealed class FollowedBookSource : Entity, IUserOwned
{
    public Guid UserId { get; }
    public string Name { get; private set; }
    public string Url { get; private set; }
    public string? Category { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly AddedOn { get; }

    public FollowedBookSource(Guid userId, string name, string url, DateOnly addedOn, string? category = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url is required.", nameof(url));

        UserId = userId;
        Name = name;
        Url = url;
        AddedOn = addedOn;
        Category = category;
        Notes = notes;
    }

    public void Update(string name, string url, string? category, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url is required.", nameof(url));

        Name = name;
        Url = url;
        Category = category;
        Notes = notes;
    }
}
