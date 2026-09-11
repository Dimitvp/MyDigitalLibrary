using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDigitalLibrary.Application.Abstractions;

namespace MyDigitalLibrary.Infrastructure.Import.Covers;

/// <summary>
/// Plan section 5.6: cover download runs off the request path. Drains
/// <see cref="CoverDownloadQueue"/>, one item at a time — personal-scale volume,
/// no need for concurrent workers. A fresh DI scope per item, per plan section
/// 6.2's captive-dependency rule (same requirement as the Stage 8 availability
/// refresh service will have).
/// </summary>
public sealed class CoverDownloadBackgroundService(
    CoverDownloadQueue queue,
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<CoverStorageOptions> storageOptions,
    ILogger<CoverDownloadBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(request, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Cover download failed for edition {EditionId} from {SourceUrl}.", request.EditionId, request.SourceUrl);
            }
        }
    }

    private async Task ProcessAsync(CoverDownloadRequest request, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("covers");

        using var response = await http.GetAsync(request.SourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Cover download for edition {EditionId} got {StatusCode} from {SourceUrl}.", request.EditionId, response.StatusCode, request.SourceUrl);
            return;
        }

        if (response.Content.Headers.ContentLength is { } length && length > storageOptions.Value.MaxSizeBytes)
        {
            logger.LogWarning("Cover for edition {EditionId} is {Length} bytes, exceeds the limit — skipped.", request.EditionId, length);
            return;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);

        using var scope = scopeFactory.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<ICoverStorage>();
        var stored = await storage.SaveAsync(bytes, contentType, ct);
        if (stored is null)
            return;

        await SaveCoverUrlAsync(scope.ServiceProvider, request.EditionId, stored, ct);
    }

    // The edition is created and enqueued in the same request, before its own
    // SaveChangesAsync commits — a short retry absorbs that ordering race
    // without forcing every composite-create call site to enqueue post-commit.
    private async Task SaveCoverUrlAsync(IServiceProvider services, Guid editionId, StoredCover stored, CancellationToken ct)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var edition = await db.Editions.FirstOrDefaultAsync(e => e.Id == editionId, ct);
            if (edition is not null)
            {
                edition.SetCoverImage(new Uri($"/covers/{stored.FileName}", UriKind.Relative));
                await db.SaveChangesAsync(ct);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }

        logger.LogWarning("Edition {EditionId} never became visible to save its downloaded cover against.", editionId);
    }
}
