namespace MyDigitalLibrary.Application.Common;

/// <summary>Maps to 404 Not Found. ErrorCode is a stable string like "work.not_found".</summary>
public sealed class NotFoundException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>Maps to 409 Conflict. ErrorCode is a stable string like "edition.duplicate".</summary>
public sealed class ConflictException(string errorCode, string message, IReadOnlyDictionary<string, object?>? extensions = null)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public IReadOnlyDictionary<string, object?>? Extensions { get; } = extensions;
}

/// <summary>Maps to 400 Bad Request for request-shape problems the domain doesn't own (e.g. "exactly one of X or Y is required").</summary>
public sealed class AppValidationException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
