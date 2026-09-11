using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

// Rates the Work, not an Edition: you're rating the story, not the paper.
// One rating per (UserId, WorkId) — enforced by a unique index at persistence.
public sealed class WorkRating : Entity, IUserOwned
{
    public const int MinScore = 1;
    public const int MaxScore = 10;

    public Guid UserId { get; }
    public Guid WorkId { get; }
    public int Score { get; private set; }
    public DateTimeOffset RatedAt { get; private set; }

    public WorkRating(Guid userId, Guid workId, int score, DateTimeOffset ratedAt)
    {
        ValidateScore(score);

        UserId = userId;
        WorkId = workId;
        Score = score;
        RatedAt = ratedAt;
    }

    public void ChangeScore(int score, DateTimeOffset ratedAt)
    {
        ValidateScore(score);

        Score = score;
        RatedAt = ratedAt;
    }

    private static void ValidateScore(int score)
    {
        if (score < MinScore || score > MaxScore)
            throw new ArgumentOutOfRangeException(nameof(score), $"Score must be between {MinScore} and {MaxScore}.");
    }
}
