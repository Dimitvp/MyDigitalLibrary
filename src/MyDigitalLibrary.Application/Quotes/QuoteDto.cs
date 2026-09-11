namespace MyDigitalLibrary.Application.Quotes;

public sealed record QuoteDto(Guid Id, Guid WorkId, string Text, string? PageOrPosition, DateTimeOffset CreatedAt);

public sealed record CreateQuoteRequest(string Text, string? PageOrPosition);

public sealed record UpdateQuoteRequest(string Text, string? PageOrPosition);
