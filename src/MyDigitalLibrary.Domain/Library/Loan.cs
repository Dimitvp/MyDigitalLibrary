using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

public sealed class Loan : Entity
{
    public Guid UserId { get; }
    public Guid LibraryItemId { get; }
    public string BorrowerName { get; }
    public DateOnly LentOn { get; }
    public DateOnly? DueOn { get; }
    public DateOnly? ReturnedOn { get; private set; }

    public bool IsReturned => ReturnedOn.HasValue;

    public Loan(Guid userId, Guid libraryItemId, string borrowerName, DateOnly lentOn, DateOnly? dueOn = null)
    {
        if (string.IsNullOrWhiteSpace(borrowerName))
            throw new ArgumentException("Borrower name is required.", nameof(borrowerName));
        if (dueOn.HasValue && dueOn < lentOn)
            throw new ArgumentException("Due date cannot be before the loan date.", nameof(dueOn));

        UserId = userId;
        LibraryItemId = libraryItemId;
        BorrowerName = borrowerName;
        LentOn = lentOn;
        DueOn = dueOn;
    }

    public void MarkReturned(DateOnly returnedOn)
    {
        if (IsReturned)
            throw new DomainException("loan.already_returned", "This loan has already been returned.");
        if (returnedOn < LentOn)
            throw new ArgumentOutOfRangeException(nameof(returnedOn), "Return date cannot be before the loan date.");

        ReturnedOn = returnedOn;
    }
}
