using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Library;

public sealed class ReadingGoal : Entity, IUserOwned
{
    public Guid UserId { get; }
    public int Year { get; }
    public int? TargetBooks { get; private set; }
    public int? TargetPages { get; private set; }

    public ReadingGoal(Guid userId, int year, int? targetBooks = null, int? targetPages = null)
    {
        Validate(targetBooks, targetPages);

        UserId = userId;
        Year = year;
        TargetBooks = targetBooks;
        TargetPages = targetPages;
    }

    public void UpdateTargets(int? targetBooks, int? targetPages)
    {
        Validate(targetBooks, targetPages);

        TargetBooks = targetBooks;
        TargetPages = targetPages;
    }

    private static void Validate(int? targetBooks, int? targetPages)
    {
        if (targetBooks is null && targetPages is null)
            throw new ArgumentException("At least one target (books or pages) must be set.");
        if (targetBooks is < 1)
            throw new ArgumentOutOfRangeException(nameof(targetBooks));
        if (targetPages is < 1)
            throw new ArgumentOutOfRangeException(nameof(targetPages));
    }
}
