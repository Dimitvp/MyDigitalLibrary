namespace MyDigitalLibrary.Domain.Common;

/// <summary>
/// Result of an operation that can fail with a stable, translatable error code
/// (e.g. "isbn.invalid") instead of an exception. Use for expected validation
/// failures on user input; use <see cref="DomainException"/> for invariant violations.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorCode { get; }
    public string? ErrorMessage { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        ErrorCode = string.Empty;
    }

    private Result(string errorCode, string? errorMessage)
    {
        IsSuccess = false;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string errorCode, string? errorMessage = null) => new(errorCode, errorMessage);
}
