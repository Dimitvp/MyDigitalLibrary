namespace MyDigitalLibrary.Infrastructure.Bookstores;

/// <summary>Plan section 6.2 — the config-level knobs for the background refresh; Bookstores:Adapters (per-bookstore selectors) is bound separately in Program.cs since each adapter needs its own named HttpClient.</summary>
public sealed class BookstoresOptions
{
    /// <summary>Plan section 6.2's global kill switch.</summary>
    public bool Enabled { get; set; } = true;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromDays(1);
    public TimeSpan MinDelayBetweenRequestsPerHost { get; set; } = TimeSpan.FromSeconds(3);
    public int MaxConsecutiveFailuresBeforeUnknown { get; set; } = 5;
}
