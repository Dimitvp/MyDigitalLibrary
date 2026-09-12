using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDigitalLibrary.Application.Abstractions;

namespace MyDigitalLibrary.Infrastructure.Import.Covers;

public sealed class CoverStorageOptions
{
    /// <summary>
    /// Relative by default (works for local `dotnet run` on any OS); Docker Compose
    /// overrides this to the "covers" volume's mount path (plan section 10) via
    /// the Covers__RootDirectory environment variable — never the same path as app
    /// code, so a container rebuild doesn't wipe covers.
    /// </summary>
    public string RootDirectory { get; set; } = "covers";
    public long MaxSizeBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>Plan section 5.6 — writes covers to a Docker volume, named by content SHA-256, never stored in the database.</summary>
public sealed class LocalFileCoverStorage(IOptions<CoverStorageOptions> options, ILogger<LocalFileCoverStorage> logger) : ICoverStorage
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public async Task<StoredCover?> SaveAsync(byte[] imageBytes, string contentType, CancellationToken ct)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            logger.LogWarning("Rejected cover with disallowed content type {ContentType}.", contentType);
            return null;
        }

        if (imageBytes.Length == 0 || imageBytes.Length > options.Value.MaxSizeBytes)
        {
            logger.LogWarning("Rejected cover of {Size} bytes (limit {Max}).", imageBytes.Length, options.Value.MaxSizeBytes);
            return null;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(imageBytes));
        var fileName = $"{hash}{ExtensionFor(contentType)}";

        Directory.CreateDirectory(options.Value.RootDirectory);
        var path = Path.Combine(options.Value.RootDirectory, fileName);

        if (!File.Exists(path))
            await File.WriteAllBytesAsync(path, imageBytes, ct);

        return new StoredCover(fileName);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };
}
