namespace MyDigitalLibrary.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }

    public Money(decimal amount, string currencyCode)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new ArgumentException("Currency code must be a 3-letter ISO 4217 code.", nameof(currencyCode));

        Amount = amount;
        CurrencyCode = currencyCode.ToUpperInvariant();
    }
}
