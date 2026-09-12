using System.Text;

namespace MyDigitalLibrary.Application.Common;

/// <summary>
/// Builds a "start of word" search pattern for free-text queries (titles,
/// author names) — matches "Мате"/"материал" for "мат" but not the middle of
/// "фермата". Used with <see cref="System.Text.RegularExpressions.Regex.IsMatch(string, string)"/>,
/// which Npgsql's EF Core provider translates to Postgres's <c>~</c> operator.
/// </summary>
public static class TextSearch
{
    /// <summary>
    /// A "start of word" Postgres regex pattern — \m anchors to a word
    /// boundary (not just string start); every regex metacharacter in the
    /// input is escaped first so a title/author containing one can't alter
    /// the pattern. Case-insensitivity is applied by passing
    /// <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>
    /// to <c>Regex.IsMatch</c> at the call site, not embedded here — Npgsql's
    /// EF Core translation already prepends its own embedded-option group
    /// (<c>(?p)</c>), and Postgres's regex engine rejects two separate
    /// embedded-option groups back to back ("quantifier operand invalid").
    /// </summary>
    public static string WordPrefixPattern(string value)
    {
        var escaped = new StringBuilder(value.Length * 2);
        foreach (var c in value)
        {
            if ("\\^$.|?*+()[]{}".IndexOf(c) >= 0)
                escaped.Append('\\');
            escaped.Append(c);
        }

        return $"\\m{escaped}";
    }
}
