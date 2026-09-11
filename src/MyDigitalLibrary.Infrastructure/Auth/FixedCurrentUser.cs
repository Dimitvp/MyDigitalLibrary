using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Infrastructure.Persistence.Seed;

namespace MyDigitalLibrary.Infrastructure.Auth;

/// <summary>
/// Stage 3 has no auth yet (that's Stage 4), but LibraryItem/WishlistEntry are
/// already per-user. Every request is treated as the same fixed development
/// user — the same one DevelopmentSeeder uses — so Stage 4 can swap this for
/// an HttpContext.User-backed implementation without touching any use case.
/// </summary>
public sealed class FixedCurrentUser : ICurrentUser
{
    public Guid UserId => DevelopmentSeeder.DevUserId;
}
