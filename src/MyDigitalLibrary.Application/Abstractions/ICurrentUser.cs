namespace MyDigitalLibrary.Application.Abstractions;

/// <summary>
/// Who is making the request. Stage 4 implements this from HttpContext.User;
/// until then, Infrastructure provides a fixed development user so per-user
/// data (LibraryItem, WishlistEntry, ...) has somewhere consistent to live.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
