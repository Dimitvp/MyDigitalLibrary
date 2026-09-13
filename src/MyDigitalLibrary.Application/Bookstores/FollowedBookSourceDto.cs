using DomainFollowedBookSource = MyDigitalLibrary.Domain.Library.FollowedBookSource;

namespace MyDigitalLibrary.Application.Bookstores;

public sealed record FollowedBookSourceDto(
    Guid Id,
    string Name,
    string Url,
    string? Category,
    string? Notes,
    DateOnly AddedOn);

public sealed record CreateFollowedBookSourceRequest(string Name, string Url, string? Category, string? Notes);

public sealed record UpdateFollowedBookSourceRequest(string Name, string Url, string? Category, string? Notes);

public static class FollowedBookSourceMapper
{
    public static FollowedBookSourceDto ToDto(DomainFollowedBookSource source) => new(
        source.Id, source.Name, source.Url, source.Category, source.Notes, source.AddedOn);
}
