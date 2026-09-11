using FluentAssertions;
using MyDigitalLibrary.Domain.Common;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Domain.Tests.Reading;

public class ReadingSessionTests
{
    [Fact]
    public void RecordProgress_rejects_percent_progress_for_an_audiobook()
    {
        var session = new ReadingSession(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Audiobook, DateOnly.FromDateTime(DateTime.Today));

        var act = () => session.RecordProgress(new PercentProgress(50), DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "reading_session.progress_format_mismatch");
    }

    [Fact]
    public void RecordProgress_accepts_timestamp_progress_for_an_audiobook()
    {
        var session = new ReadingSession(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Audiobook, DateOnly.FromDateTime(DateTime.Today));

        session.RecordProgress(new TimestampProgress(TimeSpan.FromMinutes(10)), DateTimeOffset.UtcNow);

        session.Progress.Should().ContainSingle();
    }

    [Fact]
    public void RecordProgress_rejects_progress_on_a_finished_session()
    {
        var session = new ReadingSession(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Ebook, DateOnly.FromDateTime(DateTime.Today));
        session.Finish(DateOnly.FromDateTime(DateTime.Today));

        var act = () => session.RecordProgress(new PercentProgress(10), DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "reading_session.closed");
    }

    [Fact]
    public void Rereading_creates_a_second_independent_session_rather_than_overwriting_the_first()
    {
        var libraryItemId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var firstRead = new ReadingSession(userId, libraryItemId, BookFormat.Physical, new DateOnly(2025, 1, 1));
        firstRead.RecordProgress(new PageProgress(300), DateTimeOffset.UtcNow);
        firstRead.Finish(new DateOnly(2025, 1, 10));

        var secondRead = new ReadingSession(userId, libraryItemId, BookFormat.Physical, new DateOnly(2026, 1, 1));

        secondRead.Id.Should().NotBe(firstRead.Id);
        secondRead.LibraryItemId.Should().Be(firstRead.LibraryItemId);
        secondRead.Status.Should().Be(ReadingStatus.Reading);
        firstRead.Status.Should().Be(ReadingStatus.Finished);
        firstRead.Progress.Should().ContainSingle();
        secondRead.Progress.Should().BeEmpty();
    }

    [Fact]
    public void Finish_rejects_an_end_date_before_the_start_date()
    {
        var session = new ReadingSession(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Ebook, new DateOnly(2026, 1, 10));

        var act = () => session.Finish(new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Abandon_records_the_status_and_reason()
    {
        var session = new ReadingSession(Guid.NewGuid(), Guid.NewGuid(), BookFormat.Ebook, DateOnly.FromDateTime(DateTime.Today));

        session.Abandon(DateOnly.FromDateTime(DateTime.Today), "too boring");

        session.Status.Should().Be(ReadingStatus.Abandoned);
        session.AbandonReason.Should().Be("too boring");
    }
}
