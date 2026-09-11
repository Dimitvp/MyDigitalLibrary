namespace MyDigitalLibrary.Domain.Common;

/// <summary>
/// Marks a per-user entity (plan section 3.7's "потребителски" data). Infrastructure
/// uses this to apply the same global query filter (UserId == current user) to every
/// implementing entity type without repeating it in each entity configuration.
/// </summary>
public interface IUserOwned
{
    Guid UserId { get; }
}
