using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

public sealed class Note : Entity, IUserOwned
{
    public Guid UserId { get; }
    public Guid LibraryItemId { get; }
    public string Body { get; private set; }
    public string? LocationInBook { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public Note(Guid userId, Guid libraryItemId, string body, DateTimeOffset createdAt, string? locationInBook = null)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        UserId = userId;
        LibraryItemId = libraryItemId;
        Body = body;
        LocationInBook = locationInBook;
        CreatedAt = createdAt;
    }

    public void Update(string body, string? locationInBook)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        Body = body;
        LocationInBook = locationInBook;
    }
}
