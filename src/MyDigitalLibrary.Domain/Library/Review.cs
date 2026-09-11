using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

// Reviews the Work, not an Edition: rereading in another format must not
// produce a second, disconnected review of the same book. Edition-specific
// complaints (bad translation, bad narrator) belong on LibraryItem.PersonalNote
// or Edition.Translator, not here.
public sealed class Review : Entity
{
    public Guid UserId { get; }
    public Guid WorkId { get; }
    public string Text { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public Review(Guid userId, Guid workId, string text, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        UserId = userId;
        WorkId = workId;
        Text = text;
        CreatedAt = createdAt;
    }

    public void UpdateText(string text, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        Text = text;
        UpdatedAt = updatedAt;
    }
}
