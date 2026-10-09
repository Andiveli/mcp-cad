using System.Text;

namespace McpCad.Onshape;

internal static class OnshapeBasicAuth
{
    public static string FormatAuthorizationHeader(string accessKey, string secretKey) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accessKey}:{secretKey}"));
}
