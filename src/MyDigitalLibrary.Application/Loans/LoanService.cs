using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.Loans;

/// <summary>Not in plan section 4's endpoint list, but explicit Stage 9 scope ("заемане на книги") — designed by the same REST conventions as every other resource there.</summary>
public sealed class LoanService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<LoanDto>> ListForLibraryItemAsync(Guid libraryItemId, Guid userId, CancellationToken ct)
    {
        var itemExists = await db.LibraryItems.AsNoTracking().AnyAsync(li => li.Id == libraryItemId && li.UserId == userId, ct);
        if (!itemExists)
            throw new NotFoundException("library_item.not_found", $"Library item '{libraryItemId}' was not found.");

        return await db.Loans.AsNoTracking()
            .Where(l => l.LibraryItemId == libraryItemId && l.UserId == userId)
            .OrderByDescending(l => l.LentOn)
            .Select(l => new LoanDto(l.Id, l.LibraryItemId, l.BorrowerName, l.LentOn, l.DueOn, l.ReturnedOn, l.ReturnedOn != null))
            .ToListAsync(ct);
    }

    /// <summary>Also moves the LibraryItem to OwnershipStatus.LentOut — a loan and the item's own status are two different concerns that naturally move together here.</summary>
    public async Task<LoanDto> CreateAsync(Guid libraryItemId, CreateLoanRequest request, Guid userId, CancellationToken ct)
    {
        var item = await db.LibraryItems.FirstOrDefaultAsync(li => li.Id == libraryItemId && li.UserId == userId, ct)
            ?? throw new NotFoundException("library_item.not_found", $"Library item '{libraryItemId}' was not found.");

        var hasActiveLoan = await db.Loans.AsNoTracking()
            .AnyAsync(l => l.LibraryItemId == libraryItemId && l.UserId == userId && l.ReturnedOn == null, ct);
        if (hasActiveLoan)
            throw new ConflictException("loan.already_lent_out", "This item already has an active, unreturned loan.");

        var loan = new Loan(userId, libraryItemId, request.BorrowerName, request.LentOn ?? Today(), request.DueOn);
        db.Loans.Add(loan);
        item.ChangeStatus(OwnershipStatus.LentOut);

        await db.SaveChangesAsync(ct);

        return new LoanDto(loan.Id, loan.LibraryItemId, loan.BorrowerName, loan.LentOn, loan.DueOn, loan.ReturnedOn, loan.IsReturned);
    }

    /// <summary>Also moves the LibraryItem back to OwnershipStatus.Owned, if that's still its status (a user could have manually changed it, e.g. to Sold, while the book was out).</summary>
    public async Task ReturnAsync(Guid loanId, ReturnLoanRequest request, Guid userId, CancellationToken ct)
    {
        var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == loanId && l.UserId == userId, ct)
            ?? throw new NotFoundException("loan.not_found", $"Loan '{loanId}' was not found.");

        loan.MarkReturned(request.ReturnedOn ?? Today());

        var item = await db.LibraryItems.FirstOrDefaultAsync(li => li.Id == loan.LibraryItemId && li.UserId == userId, ct);
        if (item is { Status: OwnershipStatus.LentOut })
            item.ChangeStatus(OwnershipStatus.Owned);

        await db.SaveChangesAsync(ct);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
