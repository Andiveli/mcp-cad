using System.Text.Json;
using McpCad.Core.Exceptions;

namespace McpCad.Onshape;

/// <summary>
/// Session state for Onshape: credentials, default wvm, last known microversion.
/// </summary>
public sealed class OnshapeDriver
{
    private readonly OnshapeClient _client;

    public OnshapeDriver(OnshapeClient client)
    {
        _client = client;
    }

    /// <summary>Applies config-file credentials without a network round-trip.</summary>
    public void ApplyStartupCredentials(string accessKey, string secretKeyId, string secretKey)
    {
        AccessKey = accessKey;
        SecretKeyId = string.IsNullOrWhiteSpace(secretKeyId) ? null : secretKeyId;
        _client.SetCredentials(accessKey, secretKey);
    }

    public string? AccessKey { get; private set; }

    public string? SecretKeyId { get; private set; }

    /// <summary>Default workspace/version mode: <c>w</c> or <c>m</c>.</summary>
    public string DefaultWvm { get; set; } = "w";

    public string? LastMicroversion { get; set; }

    public async Task<Dictionary<string, object?>> AuthenticateAsync(
        string accessKey,
        string secretKeyId,
        string secretKey,
        CancellationToken cancellationToken = default)
    {
        AccessKey = accessKey;
        SecretKeyId = secretKeyId;
        _client.SetCredentials(accessKey, secretKey);

        const string path = "/api/v6/oauth/keys";
        using var doc = await _client.SendJsonAsync(
            HttpMethod.Post,
            path,
            body: "{}",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var root = doc.RootElement;
        var success = root.TryGetProperty("success", out var successEl) && successEl.GetBoolean();
        if (!success)
        {
            throw OnshapeApiException.FromHttpStatus(401, "oauth_keys_failed");
        }

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["accessKey"] = accessKey,
            ["secretKeyId"] = secretKeyId,
        };
    }

    public Dictionary<string, object?> Authenticate(string accessKey, string secretKeyId, string secretKey) =>
        AuthenticateAsync(accessKey, secretKeyId, secretKey).GetAwaiter().GetResult();

    public void UpdateMicroversionFromJson(JsonElement element)
    {
        if (element.TryGetProperty("microversion", out var mv))
        {
            LastMicroversion = mv.GetString();
        }
    }
}