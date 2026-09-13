using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.Bookstores;

/// <summary>
/// CRUD for the "bookstores/libraries I follow" directory — separate from
/// <see cref="Wishlist.WishlistService"/> (specific books) and
/// <see cref="BookstoreListingService"/> (availability tracking for a specific
/// edition). This is just a personal bookmark list.
/// </summary>
public sealed class FollowedBookSourceService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<FollowedBookSourceDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await db.FollowedBookSources.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .Select(s => new FollowedBookSourceDto(s.Id, s.Name, s.Url, s.Category, s.Notes, s.AddedOn))
            .ToListAsync(ct);
    }

    public async Task<FollowedBookSourceDto> CreateAsync(CreateFollowedBookSourceRequest request, Guid userId, CancellationToken ct)
    {
        var alreadyFollowed = await db.FollowedBookSources.AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.Url == request.Url, ct);
        if (alreadyFollowed)
            throw new AppValidationException("followed_book_source.already_exists", "This URL is already in your followed sources.");

        var source = new FollowedBookSource(userId, request.Name, request.Url, DateOnly.FromDateTime(DateTime.UtcNow), request.Category, request.Notes);
        db.FollowedBookSources.Add(source);
        await db.SaveChangesAsync(ct);

        return FollowedBookSourceMapper.ToDto(source);
    }

    public async Task UpdateAsync(Guid id, UpdateFollowedBookSourceRequest request, Guid userId, CancellationToken ct)
    {
        var source = await db.FollowedBookSources.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("followed_book_source.not_found", $"Followed book source '{id}' was not found.");

        source.Update(request.Name, request.Url, request.Category, request.Notes);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var source = await db.FollowedBookSources.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("followed_book_source.not_found", $"Followed book source '{id}' was not found.");

        db.FollowedBookSources.Remove(source);
        await db.SaveChangesAsync(ct);
    }
}
