namespace MyDigitalLibrary.Application.Notes;

public sealed record NoteDto(Guid Id, Guid LibraryItemId, string Body, string? LocationInBook, DateTimeOffset CreatedAt);

public sealed record CreateNoteRequest(string Body, string? LocationInBook);

public sealed record UpdateNoteRequest(string Body, string? LocationInBook);
