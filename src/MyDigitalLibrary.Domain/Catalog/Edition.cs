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
    public Guid WorkId { get; }
    public BookFormat Format { get; }

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

    public void SetIsbn(Isbn? isbn) => Isbn13 = isbn;

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

    public void SetAudioDetails(string? narrator, AudioDuration? duration)
    {
        if ((narrator is not null || duration is not null) && Format != BookFormat.Audiobook)
            throw new DomainException("edition.audio_details_require_audiobook", "Narrator and duration only apply to audiobook editions.");

        Narrator = narrator;
        Duration = duration;
    }
}
