using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class WorkAuthor : Entity
{
    public Guid WorkId { get; }
    public Guid AuthorId { get; }
    public WorkAuthorRole Role { get; }

    internal WorkAuthor(Guid workId, Guid authorId, WorkAuthorRole role)
    {
        WorkId = workId;
        AuthorId = authorId;
        Role = role;
    }
}
