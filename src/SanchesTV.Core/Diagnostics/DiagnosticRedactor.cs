using System.Text.RegularExpressions;

namespace SanchesTV.Core.Diagnostics;

/// <summary>
/// Removes common credentials and streaming addresses from diagnostic text.
/// This is a text redactor, not a sanitizer for arbitrary structured objects.
/// </summary>
public static class DiagnosticRedactor
{
    private const string Redacted = "[redacted]";
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex AuthorizationHeaders = new(
        "(?<prefix>(?<![\\w/@.-])(?:proxy-authorization|authorization)\\b[\"']?[ \\t]*[:=][ \\t]*)(?<value>\"[^\"\\r\\n]*\"|'[^'\\r\\n]*'|[^\\r\\n]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeout);

    private static readonly Regex Credentials = new(
        "(?<prefix>(?<![\\w/@.-])(?:username|user_name|user|password|passwd|pass|pwd|token|access_token|refresh_token|api_key|apikey|secret)\\b[\"']?\\s*[:=]\\s*)(?<value>\"[^\"]*\"|'[^']*'|[^\\s,;}]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeout);

    private static readonly Regex BearerTokens = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeout);

    private static readonly Regex Addresses = new(
        "https?://[^\\s<>\"']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeout);

    private static readonly Regex Magnets = new(
        "magnet:\\?[^\\s<>\"']*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeout);

    /// <summary>
    /// Redacts before limiting length, so truncation cannot expose part of a credential.
    /// URL scheme, host and non-default port are retained for troubleshooting.
    /// </summary>
    public static string Redact(string? value, int maximumLength = 400)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        if (maximumLength == 0 || string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string sanitized;
        try
        {
            sanitized = AuthorizationHeaders.Replace(value, RedactAssignment);
            sanitized = Magnets.Replace(sanitized, "[redacted-magnet]");
            sanitized = Addresses.Replace(sanitized, match => RedactAddress(match.Value));
            sanitized = BearerTokens.Replace(sanitized, $"Bearer {Redacted}");
            sanitized = Credentials.Replace(sanitized, RedactAssignment);
        }
        catch (RegexMatchTimeoutException)
        {
            // Diagnostics must stay bounded; never fall back to the original text.
            sanitized = "[redacted-diagnostic]";
        }

        return sanitized.Length > maximumLength ? sanitized[..maximumLength] : sanitized;
    }

    private static string RedactAssignment(Match match)
    {
        var originalValue = match.Groups["value"].Value;
        var replacement = originalValue.Length > 0 && (originalValue[0] is '\"' or '\'')
            ? $"{originalValue[0]}{Redacted}{originalValue[0]}"
            : Redacted;
        return match.Groups["prefix"].Value + replacement;
    }

    private static string RedactAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
            return "[redacted-url]";

        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme}://{uri.Host}{port}/{Redacted}";
    }
}
