using Microsoft.AspNetCore.Identity;

namespace MyDigitalLibrary.Infrastructure.Auth;

/// <summary>
/// The ASP.NET Core Identity user. Deliberately lives in Infrastructure, not
/// Domain — it's a persistence/auth concern, not a domain concept. Domain and
/// Application only ever see the resulting Guid via ICurrentUser.UserId.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
