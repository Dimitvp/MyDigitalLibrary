namespace MyDigitalLibrary.Application.ReadingGoals;

public sealed record ReadingGoalDto(Guid Id, int Year, int? TargetBooks, int? TargetPages, int BooksFinished, int PagesRead);

public sealed record UpsertReadingGoalRequest(int? TargetBooks, int? TargetPages);
