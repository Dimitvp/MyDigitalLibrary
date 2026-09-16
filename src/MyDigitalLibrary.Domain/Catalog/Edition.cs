using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Reading;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Catalog;

/// <summary>
/// A specific printing/release of a <see cref="Work"/> — e.g. the 2005 Bulgarian
/// paperback vs. the Audible narration. Format-specific fields (page count vs.
/// narrator/duration) are validated against <see cref="Format"/>.
/// </summary>
public sealed class Edition : Entity
{
    /// <summary>Field-name constants for <see cref="ManualFieldOverrides"/> — shared between manual-edit marking and <see cref="Enrich"/>.</summary>
    public static class Fields
    {
        public const string Publisher = nameof(Edition.Publisher);
        public const string Language = nameof(Edition.Language);
        public const string Translator = nameof(Edition.Translator);
        public const string PublicationYear = nameof(Edition.PublicationYear);
        public const string PageCount = nameof(Edition.PageCount);
        public const string CoverImageUrl = nameof(Edition.CoverImageUrl);
        public const string Narrator = nameof(Edition.Narrator);
        public const string Duration = nameof(Edition.Duration);
    }

    public Guid WorkId { get; }
    public BookFormat Format { get; private set; }

    public Isbn? Isbn13 { get; private set; }
    public string? Publisher { get; private set; }
    public string? Language { get; private set; }
    public string? Translator { get; private set; }
    public int? PublicationYear { get; private set; }
    public int? PageCount { get; private set; }
    public CoverType CoverType { get; private set; }
    public Uri? CoverImageUrl { get; private set; }
    public string? Narrator { get; private set; }
    public AudioDuration? Duration { get; private set; }
    public ManualFieldOverrides FieldOverrides { get; private set; } = ManualFieldOverrides.None;

    public Edition(Guid workId, BookFormat format)
    {
        WorkId = workId;
        Format = format;
        CoverType = CoverType.Unknown;
    }

    public EditionExtent? Extent
        => PageCount is int pages ? EditionExtent.FromPageCount(pages)
         : Duration is { } duration ? EditionExtent.FromDuration(duration.Value)
         : null;

    public void SetIsbn(Isbn? isbn)
    {
        if (isbn is not null && Format == BookFormat.Audiobook)
            throw new DomainException("edition.isbn_requires_print_or_ebook", "ISBN only applies to physical or ebook editions.");

        Isbn13 = isbn;
    }

    public void SetPublicationDetails(string? publisher, string? language, string? translator, int? publicationYear, int? pageCount)
    {
        if (publicationYear is < 1000)
            throw new ArgumentOutOfRangeException(nameof(publicationYear), "Publication year looks invalid.");

        if (pageCount is not null)
        {
            if (Format == BookFormat.Audiobook)
                throw new DomainException("edition.page_count_requires_print_or_ebook", "Page count only applies to physical or ebook editions.");
            if (pageCount < 1)
                throw new ArgumentOutOfRangeException(nameof(pageCount));
        }

        Publisher = publisher;
        Language = language;
        Translator = translator;
        PublicationYear = publicationYear;
        PageCount = pageCount;
    }

    public void SetCoverType(CoverType coverType)
    {
        if (coverType != CoverType.Unknown && Format != BookFormat.Physical)
            throw new DomainException("edition.cover_type_requires_physical", "Cover type only applies to physical editions.");

        CoverType = coverType;
    }

    public void SetCoverImage(Uri? url) => CoverImageUrl = url;

    /// <summary>
    /// Corrects a wrongly-recorded format (e.g. a Goodreads import that
    /// guessed from a blank/unrecognized "Binding" column) after the fact.
    /// Clears whatever fields no longer apply to the new format rather than
    /// leaving stale data the throwing setters above would now reject —
    /// ISBN/page count are print-or-ebook-only, cover type is physical-only,
    /// narrator/duration are audiobook-only.
    /// </summary>
    public void ChangeFormat(BookFormat format)
    {
        if (format == Format)
            return;

        Format = format;

        if (format != BookFormat.Physical)
            CoverType = CoverType.Unknown;

        if (format == BookFormat.Audiobook)
        {
            Isbn13 = null;
            PageCount = null;
        }
        else
        {
            Narrator = null;
            Duration = null;
        }
    }

    public void SetAudioDetails(string? narrator, AudioDuration? duration)
    {
        if ((narrator is not null || duration is not null) && Format != BookFormat.Audiobook)
            throw new DomainException("edition.audio_details_require_audiobook", "Narrator and duration only apply to audiobook editions.");

        Narrator = narrator;
        Duration = duration;
    }

    /// <summary>Marks fields as manually set so a later <see cref="Enrich"/> call never overwrites them. Called by the PUT /editions/{id} handler, never by composite creation.</summary>
    public void MarkFieldsOverridden(params string[] fields)
    {
        foreach (var field in fields)
            FieldOverrides = FieldOverrides.WithOverridden(field);
    }

    /// <summary>
    /// Applies importer-sourced metadata (plan section 5.5): fills a field only when the
    /// candidate provides a value AND the field hasn't been manually overridden, and only
    /// where the field is valid for this edition's <see cref="Format"/> (page count for
    /// physical/ebook, narrator/duration for audiobooks) — never throws on a mismatch,
    /// it simply skips the field, unlike the throwing setters used for manual edits.
    /// </summary>
    public void Enrich(
        string? publisher, string? language, string? translator, int? publicationYear, int? pageCount,
        Uri? coverImageUrl, string? narrator, AudioDuration? duration)
    {
        if (publisher is not null && !FieldOverrides.IsOverridden(Fields.Publisher))
            Publisher = publisher;

        if (language is not null && !FieldOverrides.IsOverridden(Fields.Language))
            Language = language;

        if (translator is not null && !FieldOverrides.IsOverridden(Fields.Translator))
            Translator = translator;

        if (publicationYear is not null and >= 1000 && !FieldOverrides.IsOverridden(Fields.PublicationYear))
            PublicationYear = publicationYear;

        if (pageCount is > 0 && Format != BookFormat.Audiobook && !FieldOverrides.IsOverridden(Fields.PageCount))
            PageCount = pageCount;

        if (coverImageUrl is not null && !FieldOverrides.IsOverridden(Fields.CoverImageUrl))
            CoverImageUrl = coverImageUrl;

        if (Format != BookFormat.Audiobook)
            return;

        if (narrator is not null && !FieldOverrides.IsOverridden(Fields.Narrator))
            Narrator = narrator;

        if (duration is not null && !FieldOverrides.IsOverridden(Fields.Duration))
            Duration = duration;
    }
}
