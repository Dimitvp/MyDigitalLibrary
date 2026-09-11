using System.Globalization;
using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.ValueObjects;

/// <summary>
/// A checksum-validated ISBN, always normalized and stored as ISBN-13.
/// </summary>
public sealed record Isbn
{
    public string Value { get; }

    private Isbn(string value) => Value = value;

    public static Result<Isbn> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result<Isbn>.Failure("isbn.invalid", "ISBN must not be empty.");

        var cleaned = Clean(raw);

        if (cleaned.Length == 10 && IsValidIsbn10(cleaned))
            return Result<Isbn>.Success(new Isbn(ConvertIsbn10ToIsbn13(cleaned)));

        if (cleaned.Length == 13 && IsAllDigits(cleaned) && IsValidIsbn13(cleaned))
            return Result<Isbn>.Success(new Isbn(cleaned));

        return Result<Isbn>.Failure("isbn.invalid", $"'{raw}' is not a valid ISBN-10 or ISBN-13.");
    }

    private static string Clean(string raw)
    {
        Span<char> buffer = stackalloc char[raw.Length];
        var count = 0;
        foreach (var c in raw)
        {
            if (char.IsWhiteSpace(c) || c is '-' or '–' or '—')
                continue;
            buffer[count++] = char.ToUpperInvariant(c);
        }

        return new string(buffer[..count]);
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
        {
            if (c is < '0' or > '9')
                return false;
        }

        return true;
    }

    private static bool IsValidIsbn10(string s)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            if (s[i] is < '0' or > '9')
                return false;
            sum += (10 - i) * (s[i] - '0');
        }

        var last = s[9];
        var lastValue = last switch
        {
            'X' => 10,
            >= '0' and <= '9' => last - '0',
            _ => -1,
        };

        if (lastValue < 0)
            return false;

        sum += lastValue;
        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string s)
    {
        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            var weight = i % 2 == 0 ? 1 : 3;
            sum += weight * (s[i] - '0');
        }

        return sum % 10 == 0;
    }

    private static string ConvertIsbn10ToIsbn13(string isbn10)
    {
        var twelve = "978" + isbn10[..9];
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var weight = i % 2 == 0 ? 1 : 3;
            sum += weight * (twelve[i] - '0');
        }

        var checkDigit = (10 - sum % 10) % 10;
        return twelve + checkDigit.ToString(CultureInfo.InvariantCulture);
    }
}
