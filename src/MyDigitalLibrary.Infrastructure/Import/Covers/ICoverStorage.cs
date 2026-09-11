namespace MyDigitalLibrary.Infrastructure.Import.Covers;

public sealed record StoredCover(string FileName);

/// <summary>
/// Plan section 5.6 — stores cover images on disk (a Docker volume), never in
/// the database. No thumb/full resize (v1): SixLabors.ImageSharp's license
/// validation fails `dotnet publish -c Release` without a registered key even
/// though this project qualifies for its free tier by the license text alone —
/// resolving that requires an external account signup, so resizing is deferred
/// rather than adding that dependency now. The original file is stored as-is;
/// callers (list vs. detail views) size it down with CSS. Revisit with
/// SkiaSharp (MIT) or Magick.NET (Apache 2.0) if resizing is ever needed —
/// verify their current licensing before adding either.
/// </summary>
public interface ICoverStorage
{
    /// <summary>Names the file by the content's SHA-256 and writes it. Returns null if <paramref name="contentType"/> isn't an allowed image type or the content exceeds the configured size limit.</summary>
    Task<StoredCover?> SaveAsync(byte[] imageBytes, string contentType, CancellationToken ct);
}
