using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

// Belongs to the Work (not an Edition/LibraryItem): the quoted content doesn't
// change across printings, only its page/position reference does.
public sealed class Quote : Entity
{
    public Guid UserId { get; }
    public Guid WorkId { get; }
    public string Text { get; private set; }
    public string? PageOrPosition { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public Quote(Guid userId, Guid workId, string text, DateTimeOffset createdAt, string? pageOrPosition = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        UserId = userId;
        WorkId = workId;
        Text = text;
        PageOrPosition = pageOrPosition;
        CreatedAt = createdAt;
    }

    public void Update(string text, string? pageOrPosition)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        Text = text;
        PageOrPosition = pageOrPosition;
    }
}
