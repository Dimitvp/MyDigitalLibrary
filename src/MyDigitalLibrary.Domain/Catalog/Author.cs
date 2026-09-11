using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class Author : Entity
{
    public string FullName { get; private set; }
    public string SortName { get; private set; }
    public int? BirthYear { get; private set; }
    public int? DeathYear { get; private set; }
    public string? Bio { get; private set; }

    private readonly Dictionary<string, string> _externalIds = [];
    public IReadOnlyDictionary<string, string> ExternalIds => _externalIds;

    public Author(string fullName, string sortName, int? birthYear = null, int? deathYear = null, string? bio = null)
    {
        ValidateNames(fullName, sortName);
        ValidateYears(birthYear, deathYear);
        FullName = fullName;
        SortName = sortName;
        BirthYear = birthYear;
        DeathYear = deathYear;
        Bio = bio;
    }

    public void UpdateDetails(string fullName, string sortName, int? birthYear, int? deathYear, string? bio)
    {
        ValidateNames(fullName, sortName);
        ValidateYears(birthYear, deathYear);
        FullName = fullName;
        SortName = sortName;
        BirthYear = birthYear;
        DeathYear = deathYear;
        Bio = bio;
    }

    public void SetExternalId(string source, string id)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source is required.", nameof(source));

        _externalIds[source] = id;
    }

    private static void ValidateNames(string fullName, string sortName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(sortName))
            throw new ArgumentException("Sort name is required.", nameof(sortName));
    }

    private static void ValidateYears(int? birthYear, int? deathYear)
    {
        if (birthYear.HasValue && deathYear.HasValue && deathYear < birthYear)
            throw new ArgumentException("Death year cannot be before birth year.");
    }
}
