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
    /// <summary>Field-name constants for <see cref="ManualFieldOverrides"/> — shared between manual-edit marking and <see cref="Enrich"/>.</summary>
    public static class Fields
    {
        public const string Title = nameof(Work.Title);
        public const string OriginalTitle = nameof(Work.OriginalTitle);
        public const string Description = nameof(Work.Description);
        public const string FirstPublicationYear = nameof(Work.FirstPublicationYear);
    }

    public string Title { get; private set; }
    public string? OriginalTitle { get; private set; }
    public string? Description { get; private set; }
    public int? FirstPublicationYear { get; private set; }
    public Guid? SeriesId { get; private set; }
    public SeriesPosition? PositionInSeries { get; private set; }
    public ManualFieldOverrides FieldOverrides { get; private set; } = ManualFieldOverrides.None;

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

    /// <summary>Marks fields as manually set so a later <see cref="Enrich"/> call never overwrites them. Called by the PUT /works/{id} handler, never by composite creation.</summary>
    public void MarkFieldsOverridden(params string[] fields)
    {
        foreach (var field in fields)
            FieldOverrides = FieldOverrides.WithOverridden(field);
    }

    /// <summary>
    /// Applies importer-sourced metadata (plan section 5.5): fills a field only when the
    /// candidate provides a value AND the field hasn't been manually overridden. Never
    /// throws on a "worse" candidate — enrichment is best-effort, unlike <see cref="UpdateDetails"/>.
    /// </summary>
    public void Enrich(string? title, string? originalTitle, string? description, int? firstPublicationYear)
    {
        if (title is not null && !FieldOverrides.IsOverridden(Fields.Title))
            Title = title;

        if (originalTitle is not null && !FieldOverrides.IsOverridden(Fields.OriginalTitle))
            OriginalTitle = originalTitle;

        if (description is not null && !FieldOverrides.IsOverridden(Fields.Description))
            Description = description;

        if (firstPublicationYear is not null && !FieldOverrides.IsOverridden(Fields.FirstPublicationYear))
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
