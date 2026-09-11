namespace MyDigitalLibrary.Application.Loans;

public sealed record LoanDto(Guid Id, Guid LibraryItemId, string BorrowerName, DateOnly LentOn, DateOnly? DueOn, DateOnly? ReturnedOn, bool IsReturned);

public sealed record CreateLoanRequest(string BorrowerName, DateOnly? LentOn, DateOnly? DueOn);

public sealed record ReturnLoanRequest(DateOnly? ReturnedOn);
