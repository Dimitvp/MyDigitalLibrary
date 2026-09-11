using FluentAssertions;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Domain.Tests.Library;

public class LoanTests
{
    [Fact]
    public void MarkReturned_rejects_being_called_twice()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), "Ivan", new DateOnly(2026, 1, 1));
        loan.MarkReturned(new DateOnly(2026, 2, 1));

        var act = () => loan.MarkReturned(new DateOnly(2026, 3, 1));

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "loan.already_returned");
    }

    [Fact]
    public void Constructor_rejects_a_due_date_before_the_loan_date()
    {
        var act = () => new Loan(Guid.NewGuid(), Guid.NewGuid(), "Ivan", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>();
    }
}
