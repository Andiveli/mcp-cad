using System.Text;

namespace McpCad.Onshape;

/// <summary>
/// API key authentication per Onshape OpenAPI <c>BasicAuth</c>: access key as username, secret as password.
/// </summary>
internal static class OnshapeBasicAuth
{
    public static string FormatAuthorizationHeader(string accessKey, string secretKey)
    {
        var raw = $"{accessKey}:{secretKey}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }
}