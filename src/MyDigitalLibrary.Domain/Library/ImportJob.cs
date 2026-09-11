using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Library;

public sealed record ImportJobStats(int TotalRows, int SucceededRows, int FailedRows);

public sealed class ImportJob : Entity, IUserOwned
{
    public Guid UserId { get; }
    public ImportJobKind Kind { get; }
    public ImportJobStatus Status { get; private set; }
    public string? SourceFileName { get; }
    public ImportJobStats Stats { get; private set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public ImportJob(Guid userId, ImportJobKind kind, DateTimeOffset startedAt, string? sourceFileName = null)
    {
        UserId = userId;
        Kind = kind;
        Status = ImportJobStatus.Pending;
        SourceFileName = sourceFileName;
        Stats = new ImportJobStats(0, 0, 0);
        StartedAt = startedAt;
    }

    public void Start()
    {
        if (Status != ImportJobStatus.Pending)
            throw new DomainException("import_job.already_started", "This import job has already been started.");

        Status = ImportJobStatus.Running;
    }

    public void Complete(ImportJobStats stats, DateTimeOffset finishedAt)
    {
        Status = ImportJobStatus.Completed;
        Stats = stats;
        FinishedAt = finishedAt;
    }

    public void Fail(ImportJobStats stats, DateTimeOffset finishedAt)
    {
        Status = ImportJobStatus.Failed;
        Stats = stats;
        FinishedAt = finishedAt;
    }
}
