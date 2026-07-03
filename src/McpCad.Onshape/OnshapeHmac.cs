using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace McpCad.Onshape;

internal static class OnshapeHmac
{
    /// <summary>
    /// Builds the Onshape API canonical string: METHOD, URI, nonce, date, contentType, body (each line-separated).
    /// </summary>
    public static string BuildCanonical(
        string method,
        string uri,
        string nonce,
        string date,
        string contentType,
        string body) =>
        string.Join(
            '\n',
            method.ToUpperInvariant(),
            uri,
            nonce,
            date,
            contentType,
            body);

    public static string Sign(string canonical, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var canonicalBytes = Encoding.UTF8.GetBytes(canonical);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(canonicalBytes));
    }

    /// <summary>RFC 7231 style date for On-Date header.</summary>
    public static string FormatAuthDate(DateTimeOffset utc) =>
        utc.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
}