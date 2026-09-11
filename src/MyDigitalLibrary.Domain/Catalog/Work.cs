using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Catalog;

/// <summary>
/// The book as a work of authorship (e.g. "Dune" by Frank Herbert), independent
/// of any particular printing. See <see cref="Edition"/> for a specific
/// printing/format and LibraryItem for an owned copy of one.
/// </summary>
public sealed class Work : Entity
{
    public string Title { get; private set; }
    public string? OriginalTitle { get; private set; }
    public string? Description { get; private set; }
    public int? FirstPublicationYear { get; private set; }
    public Guid? SeriesId { get; private set; }
    public SeriesPosition? PositionInSeries { get; private set; }

    private readonly List<Guid> _genreIds = [];
    public IReadOnlyList<Guid> GenreIds => _genreIds.AsReadOnly();

    private readonly List<WorkAuthor> _authors = [];
    public IReadOnlyList<WorkAuthor> Authors => _authors.AsReadOnly();

    public Work(string title, string? originalTitle = null, string? description = null, int? firstPublicationYear = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Title = title;
        OriginalTitle = originalTitle;
        Description = description;
        FirstPublicationYear = firstPublicationYear;
    }

    public void UpdateDetails(string title, string? originalTitle, string? description, int? firstPublicationYear)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Title = title;
        OriginalTitle = originalTitle;
        Description = description;
        FirstPublicationYear = firstPublicationYear;
    }

    public void AddAuthor(Guid authorId, WorkAuthorRole role)
    {
        if (_authors.Any(a => a.AuthorId == authorId && a.Role == role))
            throw new DomainException("work.author_already_added", "This author already has this role on the work.");

        _authors.Add(new WorkAuthor(Id, authorId, role));
    }

    public void RemoveAuthor(Guid authorId, WorkAuthorRole role)
        => _authors.RemoveAll(a => a.AuthorId == authorId && a.Role == role);

    public void AssignToSeries(Guid seriesId, SeriesPosition position)
    {
        SeriesId = seriesId;
        PositionInSeries = position;
    }

    public void RemoveFromSeries()
    {
        SeriesId = null;
        PositionInSeries = null;
    }

    public void AddGenre(Guid genreId)
    {
        if (!_genreIds.Contains(genreId))
            _genreIds.Add(genreId);
    }

    public void RemoveGenre(Guid genreId) => _genreIds.Remove(genreId);
}
