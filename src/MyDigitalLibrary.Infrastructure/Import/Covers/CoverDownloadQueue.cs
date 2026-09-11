using System.Threading.Channels;
using MyDigitalLibrary.Application.Abstractions;

namespace MyDigitalLibrary.Infrastructure.Import.Covers;

public sealed record CoverDownloadRequest(Guid EditionId, Uri SourceUrl);

/// <summary>
/// Registered as a singleton implementing both <see cref="ICoverDownloadQueue"/>
/// (the Application-facing port) and itself (so <see cref="CoverDownloadBackgroundService"/>
/// can read from the same channel) — same object, two registrations.
/// </summary>
public sealed class CoverDownloadQueue : ICoverDownloadQueue
{
    private readonly Channel<CoverDownloadRequest> _channel = Channel.CreateBounded<CoverDownloadRequest>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<CoverDownloadRequest> Reader => _channel.Reader;

    public void Enqueue(Guid editionId, Uri sourceUrl) => _channel.Writer.TryWrite(new CoverDownloadRequest(editionId, sourceUrl));
}
