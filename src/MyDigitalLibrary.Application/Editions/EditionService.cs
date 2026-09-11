using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Editions;

public sealed class EditionService(IApplicationDbContext db)
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

            var duplicateId = await db.Editions.AsNoTracking()
                .Where(e => e.Id != id && e.Isbn13 == isbn)
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(ct);

            if (duplicateId is { } existingId)
                throw new ConflictException(
                    "edition.duplicate",
                    $"An edition with ISBN {isbn.Value} already exists.",
                    new Dictionary<string, object?> { ["existingEditionId"] = existingId });
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

    internal static IQueryable<EditionRow> Project(IQueryable<Edition> source) => source
        .Select(e => new EditionRow(
            e.Id, e.WorkId, e.Format, e.Isbn13, e.Publisher, e.Language, e.Translator,
            e.PublicationYear, e.PageCount, e.CoverType, e.CoverImageUrl, e.Narrator, e.Duration));
}
