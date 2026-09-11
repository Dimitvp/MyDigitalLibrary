namespace MyDigitalLibrary.Domain.Common;

/// <summary>
/// Thrown when an operation would violate a domain invariant (as opposed to
/// rejecting bad user input, which uses <see cref="Result{T}"/>).
/// </summary>
public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
