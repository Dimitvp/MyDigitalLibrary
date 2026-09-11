using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Bookstores;

namespace MyDigitalLibrary.Infrastructure.Bookstores;

/// <summary>
/// Plan section 6.2. Runs once per RefreshInterval (default daily). A fresh
/// DI scope per listing, not per cycle — a long-running BackgroundService
/// holding one scoped DbContext across a whole day's worth of listings would
/// be exactly the captive-dependency problem the plan warns about, and would
/// let the change tracker grow unbounded across a potentially large run.
/// </summary>
public sealed class AvailabilityRefreshService(
    IServiceScopeFactory scopeFactory,
    IOptions<BookstoresOptions> options,
    ILogger<AvailabilityRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Bookstore availability refresh is disabled (Bookstores:Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(options.Value.RefreshInterval);
        do
        {
            try
            {
                await RefreshAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Bookstore availability refresh cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAllAsync(CancellationToken ct)
    {
        List<Guid> listingIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            listingIds = await db.BookstoreListings.AsNoTracking().Select(l => l.Id).ToListAsync(ct);
        }

        // Per-host politeness delay spans the whole cycle, not just one
        // listing — two listings on the same host must still be spaced out.
        var lastRequestPerHost = new Dictionary<string, DateTimeOffset>();

        foreach (var listingId in listingIds)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await RefreshOneAsync(listingId, lastRequestPerHost, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to refresh bookstore listing {ListingId}.", listingId);
            }
        }
    }

    private async Task RefreshOneAsync(Guid listingId, Dictionary<string, DateTimeOffset> lastRequestPerHost, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var adaptersByKey = scope.ServiceProvider.GetServices<IBookstoreAdapter>().ToDictionary(a => a.AdapterKey);

        var listing = await db.BookstoreListings.FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing is null)
            return; // deleted since the id list was captured

        var bookstore = await db.Bookstores.AsNoTracking().FirstOrDefaultAsync(b => b.Id == listing.BookstoreId, ct);
        if (bookstore is null || !adaptersByKey.TryGetValue(bookstore.AdapterKey, out var adapter))
            return; // plan 6.3 — a manually-added listing with no matching adapter is left alone, not an error

        var host = listing.Url.Host;
        if (lastRequestPerHost.TryGetValue(host, out var lastRequest))
        {
            var wait = options.Value.MinDelayBetweenRequestsPerHost - (DateTimeOffset.UtcNow - lastRequest);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
        }
        lastRequestPerHost[host] = DateTimeOffset.UtcNow;

        var snapshot = await adapter.FetchAsync(listing.Url, ct);
        var checkedAt = DateTimeOffset.UtcNow;

        if (snapshot is null)
        {
            // Never OutOfStock on a failed scrape — RecordFailedCheck leaves Availability untouched.
            listing.RecordFailedCheck(checkedAt);

            if (listing.ConsecutiveFailures >= options.Value.MaxConsecutiveFailuresBeforeUnknown)
            {
                listing.MarkUnknownDueToRepeatedFailures();
                logger.LogWarning(
                    "Listing {ListingId} ({Host}) failed {Count} consecutive checks -- marked Unknown.",
                    listingId, host, listing.ConsecutiveFailures);
            }
        }
        else
        {
            listing.RecordSuccessfulCheck(snapshot.Availability, snapshot.Price, checkedAt);
        }

        await db.SaveChangesAsync(ct);
    }
}
