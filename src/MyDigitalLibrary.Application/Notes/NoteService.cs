using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Common;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Application.Notes;

/// <summary>Not in plan section 4's endpoint list, but explicit Stage 7 scope ("бележки") — designed by the same REST conventions as every other resource there.</summary>
public sealed class NoteService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<NoteDto>> ListForLibraryItemAsync(Guid libraryItemId, Guid userId, CancellationToken ct)
    {
        var itemExists = await db.LibraryItems.AsNoTracking().AnyAsync(li => li.Id == libraryItemId && li.UserId == userId, ct);
        if (!itemExists)
            throw new NotFoundException("library_item.not_found", $"Library item '{libraryItemId}' was not found.");

        return await db.Notes.AsNoTracking()
            .Where(n => n.LibraryItemId == libraryItemId && n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NoteDto(n.Id, n.LibraryItemId, n.Body, n.LocationInBook, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<NoteDto> CreateAsync(Guid libraryItemId, CreateNoteRequest request, Guid userId, CancellationToken ct)
    {
        var itemExists = await db.LibraryItems.AsNoTracking().AnyAsync(li => li.Id == libraryItemId && li.UserId == userId, ct);
        if (!itemExists)
            throw new NotFoundException("library_item.not_found", $"Library item '{libraryItemId}' was not found.");

        var note = new Note(userId, libraryItemId, request.Body, DateTimeOffset.UtcNow, request.LocationInBook);
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);

        return new NoteDto(note.Id, note.LibraryItemId, note.Body, note.LocationInBook, note.CreatedAt);
    }

    public async Task UpdateAsync(Guid id, UpdateNoteRequest request, Guid userId, CancellationToken ct)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct)
            ?? throw new NotFoundException("note.not_found", $"Note '{id}' was not found.");

        note.Update(request.Body, request.LocationInBook);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct)
            ?? throw new NotFoundException("note.not_found", $"Note '{id}' was not found.");

        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);
    }
}
