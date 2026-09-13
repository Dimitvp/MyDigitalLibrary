using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Editions;

public sealed class EditionService(IApplicationDbContext db, ICoverStorage coverStorage)
{
    public async Task<EditionDto> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await Project(db.Editions.AsNoTracking().Where(e => e.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("edition.not_found", $"Edition '{id}' was not found.");

        return row.ToDto();
    }

    public async Task UpdateAsync(Guid id, UpdateEditionRequest request, CancellationToken ct)
    {
        var edition = await db.Editions.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("edition.not_found", $"Edition '{id}' was not found.");

        Isbn? isbn = null;
        if (!string.IsNullOrWhiteSpace(request.Isbn13))
        {
            var isbnResult = Isbn.TryCreate(request.Isbn13);
            if (isbnResult.IsFailure)
                throw new AppValidationException(isbnResult.ErrorCode, $"'{request.Isbn13}' is not a valid ISBN.");

            isbn = isbnResult.Value;

            var duplicate = await db.Editions.AsNoTracking()
                .Where(e => e.Id != id && e.Isbn13 == isbn)
                .Select(e => new { e.Id, e.WorkId })
                .FirstOrDefaultAsync(ct);

            if (duplicate is not null)
            {
                var existingWorkTitle = await db.Works.AsNoTracking()
                    .Where(w => w.Id == duplicate.WorkId)
                    .Select(w => w.Title)
                    .FirstOrDefaultAsync(ct);

                throw new ConflictException(
                    "edition.duplicate",
                    $"An edition with ISBN {isbn.Value} already exists ({existingWorkTitle}).",
                    new Dictionary<string, object?>
                    {
                        ["existingEditionId"] = duplicate.Id,
                        ["existingWorkId"] = duplicate.WorkId,
                        ["existingWorkTitle"] = existingWorkTitle,
                    });
            }
        }

        edition.SetIsbn(isbn);
        edition.SetPublicationDetails(request.Publisher, request.Language, request.Translator, request.PublicationYear, request.PageCount);
        edition.SetCoverType(request.CoverType);

        var duration = request.DurationMinutes is { } minutes ? new AudioDuration(TimeSpan.FromMinutes(minutes)) : null;
        edition.SetAudioDetails(request.Narrator, duration);

        edition.MarkFieldsOverridden(
            Edition.Fields.Publisher, Edition.Fields.Language, Edition.Fields.Translator,
            Edition.Fields.PublicationYear, Edition.Fields.PageCount, Edition.Fields.Narrator, Edition.Fields.Duration);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Saves a user-uploaded cover image and points the edition at it,
    /// replacing whatever cover (if any) it had before. Unlike the import
    /// pipeline's <see cref="Abstractions.ICoverDownloadQueue"/>, this runs
    /// synchronously — the bytes are already local, there's no network fetch
    /// to keep off the request path.
    /// </summary>
    public async Task<string> UploadCoverAsync(Guid id, byte[] imageBytes, string contentType, CancellationToken ct)
    {
        var edition = await db.Editions.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("edition.not_found", $"Edition '{id}' was not found.");

        var stored = await coverStorage.SaveAsync(imageBytes, contentType, ct)
            ?? throw new AppValidationException("edition.cover_rejected", "That image couldn't be used — check the file type (JPEG/PNG/WebP) and size (max 5 MB).");

        var url = new Uri($"/covers/{stored.FileName}", UriKind.Relative);
        edition.SetCoverImage(url);
        edition.MarkFieldsOverridden(Edition.Fields.CoverImageUrl);
        await db.SaveChangesAsync(ct);

        return url.ToString();
    }

    internal static IQueryable<EditionRow> Project(IQueryable<Edition> source) => source
        .Select(e => new EditionRow(
            e.Id, e.WorkId, e.Format, e.Isbn13, e.Publisher, e.Language, e.Translator,
            e.PublicationYear, e.PageCount, e.CoverType, e.CoverImageUrl, e.Narrator, e.Duration));
}
