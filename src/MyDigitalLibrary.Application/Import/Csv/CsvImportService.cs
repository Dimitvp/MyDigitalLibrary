using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import.Csv;

/// <summary>
/// Plan section 4: POST /api/v1/import/csv (multipart) -> ImportJob, GET
/// /api/v1/import/jobs/{id}. Runs synchronously within the request rather
/// than through a background queue (unlike Stage 6's cover downloads) — a
/// personal library's CSV is at most a few hundred rows, and adding queue
/// infrastructure for that would be YAGNI; the ImportJob's Pending -> Running
/// -> Completed/Failed lifecycle still applies, it just all happens before
/// the response is sent. One row failing (bad ISBN, missing series position,
/// etc.) never aborts the rest of the file — each row is its own try/catch,
/// counted into ImportJobStats.
/// </summary>
public sealed class CsvImportService(IApplicationDbContext db, BookCatalogService catalog)
{
    public async Task<ImportJobDto> ImportAsync(Stream csvStream, string? fileName, Guid userId, CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream);
        var csvText = await reader.ReadToEndAsync(ct);

        var format = CsvFormatDetector.Detect(csvText);
        if (format is null)
            throw new AppValidationException(
                "import_job.unrecognized_csv_format",
                "Could not recognize this CSV as a Goodreads or Calibre export (expected columns not found).");

        var job = new ImportJob(userId, ImportJobKind.Csv, DateTimeOffset.UtcNow, fileName);
        db.ImportJobs.Add(job);
        job.Start();
        await db.SaveChangesAsync(ct);

        IReadOnlyList<ParsedBookRow> rows;
        try
        {
            rows = format == CsvSourceFormat.Goodreads ? GoodreadsCsvParser.Parse(csvText) : CalibreCsvParser.Parse(csvText);
        }
        catch (Exception)
        {
            job.Fail(new ImportJobStats(0, 0, 0), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            throw new AppValidationException("import_job.unparseable_csv", "The CSV file could not be parsed.");
        }

        var succeeded = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            try
            {
                await ImportRowAsync(row, userId, ct);
                succeeded++;
            }
            catch (Exception)
            {
                failed++;
            }
        }

        job.Complete(new ImportJobStats(rows.Count, succeeded, failed), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        return ToDto(job);
    }

    public async Task<ImportJobDto> GetJobAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var job = await db.ImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct)
            ?? throw new NotFoundException("import_job.not_found", $"Import job '{id}' was not found.");

        return ToDto(job);
    }

    private async Task ImportRowAsync(ParsedBookRow row, Guid userId, CancellationToken ct)
    {
        Isbn? isbn = null;
        if (!string.IsNullOrWhiteSpace(row.Isbn))
        {
            var isbnResult = Isbn.TryCreate(row.Isbn);
            isbn = isbnResult.IsSuccess ? isbnResult.Value : null;
        }

        Guid workId;
        Guid editionId;

        // Bulk import reuses a matching existing edition by ISBN rather than
        // throwing edition.duplicate the way the single-item composite create
        // does — re-importing the same file, or an overlapping library, is
        // the normal case here, not an error.
        var existingEdition = isbn is null ? null : await db.Editions.FirstOrDefaultAsync(e => e.Isbn13 == isbn, ct);
        if (existingEdition is not null)
        {
            editionId = existingEdition.Id;
            workId = existingEdition.WorkId;
        }
        else
        {
            var work = await catalog.ResolveOrCreateWorkAsync(
                new CreateWorkRequest(row.Title, null, null, row.PublicationYear, row.AuthorNames, row.SeriesName, row.SeriesPosition), ct);

            var edition = new Edition(work.Id, row.Format);
            edition.SetIsbn(isbn);
            edition.SetPublicationDetails(row.Publisher, null, null, row.PublicationYear, row.Format == BookFormat.Audiobook ? null : row.PageCount);
            db.Editions.Add(edition);

            workId = work.Id;
            editionId = edition.Id;
        }

        if (row.WantToRead)
        {
            db.WishlistEntries.Add(new WishlistEntry(userId, workId, row.Format, priority: 3, DateOnly.FromDateTime(DateTime.UtcNow)));
        }
        else
        {
            var acquisition = new Acquisition(row.AcquiredOn ?? DateOnly.FromDateTime(DateTime.UtcNow), AcquisitionMethod.Bought, null, "CSV import");
            db.LibraryItems.Add(new LibraryItem(userId, editionId, row.Format, acquisition));

            if (row.Rating is { } rating && !await db.WorkRatings.AnyAsync(r => r.UserId == userId && r.WorkId == workId, ct))
                db.WorkRatings.Add(new WorkRating(userId, workId, rating, DateTimeOffset.UtcNow));

            if (row.Review is not null && !await db.Reviews.AnyAsync(r => r.UserId == userId && r.WorkId == workId, ct))
                db.Reviews.Add(new Review(userId, workId, row.Review, DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync(ct);
    }

    private static ImportJobDto ToDto(ImportJob job) => new(
        job.Id, job.Kind.ToString(), job.Status.ToString(), job.SourceFileName,
        job.Stats.TotalRows, job.Stats.SucceededRows, job.Stats.FailedRows,
        job.StartedAt, job.FinishedAt);
}
