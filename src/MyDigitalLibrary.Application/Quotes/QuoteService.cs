using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.Quotes;

/// <summary>Not in plan section 4's endpoint list, but explicit Stage 7 scope ("цитати") — designed by the same REST conventions as every other resource there.</summary>
public sealed class QuoteService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<QuoteDto>> ListForWorkAsync(Guid workId, Guid userId, CancellationToken ct)
    {
        var workExists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == workId, ct);
        if (!workExists)
            throw new NotFoundException("work.not_found", $"Work '{workId}' was not found.");

        return await db.Quotes.AsNoTracking()
            .Where(q => q.WorkId == workId && q.UserId == userId)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new QuoteDto(q.Id, q.WorkId, q.Text, q.PageOrPosition, q.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<QuoteDto> CreateAsync(Guid workId, CreateQuoteRequest request, Guid userId, CancellationToken ct)
    {
        var workExists = await db.Works.AsNoTracking().AnyAsync(w => w.Id == workId, ct);
        if (!workExists)
            throw new NotFoundException("work.not_found", $"Work '{workId}' was not found.");

        var quote = new Quote(userId, workId, request.Text, DateTimeOffset.UtcNow, request.PageOrPosition);
        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        return new QuoteDto(quote.Id, quote.WorkId, quote.Text, quote.PageOrPosition, quote.CreatedAt);
    }

    public async Task UpdateAsync(Guid id, UpdateQuoteRequest request, Guid userId, CancellationToken ct)
    {
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.UserId == userId, ct)
            ?? throw new NotFoundException("quote.not_found", $"Quote '{id}' was not found.");

        quote.Update(request.Text, request.PageOrPosition);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.UserId == userId, ct)
            ?? throw new NotFoundException("quote.not_found", $"Quote '{id}' was not found.");

        db.Quotes.Remove(quote);
        await db.SaveChangesAsync(ct);
    }
}
