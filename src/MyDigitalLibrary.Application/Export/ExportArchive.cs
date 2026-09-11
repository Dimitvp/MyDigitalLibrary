namespace MyDigitalLibrary.Application.Export;

// Every row is denormalized (work title/authors spelled out, not just ids) —
// plan section 4's "без vendor lock-in" means the file should be readable on
// its own, without a second export to resolve what a foreign key points to.

public sealed record ExportLibraryItem(
    Guid Id, string WorkTitle, IReadOnlyList<string> AuthorNames, string? Isbn13,
    string Format, string Status, DateOnly AcquiredOn, string AcquisitionMethod,
    decimal? PriceAmount, string? PriceCurrency, string? Source, string? PersonalNote,
    int? MyRating, string? MyReview);

public sealed record ExportWishlistEntry(
    Guid Id, string WorkTitle, IReadOnlyList<string> AuthorNames, string DesiredFormat,
    int Priority, DateOnly AddedOn, bool IsFulfilled);

public sealed record ExportShelf(string Name, IReadOnlyList<string> ItemTitles);

public sealed record ExportNote(string WorkTitle, string Body, string? LocationInBook, DateTimeOffset CreatedAt);

public sealed record ExportQuote(string WorkTitle, string Text, string? PageOrPosition, DateTimeOffset CreatedAt);

public sealed record ExportLoan(string WorkTitle, string BorrowerName, DateOnly LentOn, DateOnly? DueOn, DateOnly? ReturnedOn);

public sealed record ExportReadingSession(string WorkTitle, string Format, DateOnly StartedOn, string Status, DateOnly? EndedOn);

public sealed record ExportReadingGoal(int Year, int? TargetBooks, int? TargetPages);

public sealed record ExportArchive(
    DateTimeOffset ExportedAt,
    IReadOnlyList<ExportLibraryItem> LibraryItems,
    IReadOnlyList<ExportWishlistEntry> Wishlist,
    IReadOnlyList<ExportShelf> Shelves,
    IReadOnlyList<ExportNote> Notes,
    IReadOnlyList<ExportQuote> Quotes,
    IReadOnlyList<ExportLoan> Loans,
    IReadOnlyList<ExportReadingSession> ReadingSessions,
    IReadOnlyList<ExportReadingGoal> ReadingGoals);
