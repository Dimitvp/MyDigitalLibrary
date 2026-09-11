namespace MyDigitalLibrary.Application.Import.Csv;

public sealed record ImportJobDto(
    Guid Id, string Kind, string Status, string? SourceFileName,
    int TotalRows, int SucceededRows, int FailedRows,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);
