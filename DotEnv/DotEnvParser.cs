using System.Text;
using System.Text.RegularExpressions;

namespace DotEnv;

/// <summary>Parses dotenv text using syntax supported by dotenv and dotenvx.</summary>
public static partial class DotEnvParser
{
    /// <summary>Parses dotenv text into key/value pairs.</summary>
    /// <param name="source">The dotenv document.</param>
    /// <param name="existingValues">Values available to variable expansion.</param>
    /// <returns>The parsed values.</returns>
    public static IReadOnlyDictionary<string, string> Parse(string source, IReadOnlyDictionary<string, string?>? existingValues = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ReadEntries(source))
        {
            var value = ParseValue(entry.Value);
            if (entry.Quote != '\'') value = Expand(value, result, existingValues);
            result[entry.Key] = value;
        }
        return result;
    }

    private static IEnumerable<(string Key, string Value, char Quote)> ReadEntries(string source)
    {
        using var reader = new StringReader(source.Replace("\r\n", "\n", StringComparison.Ordinal));
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] == '#') continue;
            if (trimmed.StartsWith("export ", StringComparison.Ordinal)) trimmed = trimmed[7..].TrimStart();
            var separator = trimmed.IndexOf('=');
            if (separator <= 0) continue;
            var key = trimmed[..separator].Trim();
            if (!KeyRegex().IsMatch(key)) continue;
            var raw = trimmed[(separator + 1)..].TrimStart();
            var quote = raw.Length > 0 && raw[0] is '\'' or '"' or '`' ? raw[0] : '\0';
            if (quote != '\0' && !HasClosingQuote(raw, quote))
            {
                var value = new StringBuilder(raw);
                while ((line = reader.ReadLine()) is not null)
                {
                    value.Append('\n').Append(line);
                    if (HasClosingQuote(value.ToString(), quote)) break;
                }
                raw = value.ToString();
            }
            yield return (key, StripComment(raw, quote).Trim(), quote);
        }
    }

    private static string ParseValue(string raw)
    {
        if (raw.Length >= 2 && raw[0] is '\'' or '"' or '`' && raw[^1] == raw[0])
        {
            var quote = raw[0];
            raw = raw[1..^1];
            if (quote == '"') raw = raw.Replace("\\n", "\n", StringComparison.Ordinal).Replace("\\r", "\r", StringComparison.Ordinal);
        }
        return raw;
    }

    private static string Expand(string value, IReadOnlyDictionary<string, string> parsed, IReadOnlyDictionary<string, string?>? existing)
        => ExpansionRegex().Replace(value, match =>
        {
            var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[4].Value;
            var found = parsed.TryGetValue(name, out var parsedValue) ? parsedValue
                : existing is not null && existing.TryGetValue(name, out var existingValue) ? existingValue
                : Environment.GetEnvironmentVariable(name);
            var operation = match.Groups[2].Value;
            var fallback = match.Groups[3].Value;
            return operation switch
            {
                ":-" when string.IsNullOrEmpty(found) => fallback,
                "-" when found is null => fallback,
                ":+" when !string.IsNullOrEmpty(found) => fallback,
                "+" when found is not null => fallback,
                _ => found ?? string.Empty
            };
        });

    private static bool HasClosingQuote(string value, char quote) => value.Length > 1 && value.LastIndexOf(quote) > 0;
    private static string StripComment(string value, char quote)
    {
        if (quote != '\0')
        {
            var closing = value.LastIndexOf(quote);
            return closing > 0 ? value[..(closing + 1)] : value;
        }
        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return comment >= 0 ? value[..comment] : value;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)(?:(:-|-|:\+|\+)([^}]*))?\}|\$([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ExpansionRegex();
}
