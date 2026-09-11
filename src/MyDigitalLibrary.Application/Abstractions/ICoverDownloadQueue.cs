namespace MyDigitalLibrary.Application.Abstractions;

/// <summary>
/// Port for queueing an async cover download (plan section 5.6: "download is a
/// background job, must not block saving the book"). Infrastructure provides the
/// actual queue + a BackgroundService that drains it.
/// </summary>
public interface ICoverDownloadQueue
{
    void Enqueue(Guid editionId, Uri sourceUrl);
}
