using System.Net;
using System.Text;
using System.Text.Json;
using McpCad.Core.Exceptions;

namespace McpCad.Onshape;

public sealed class OnshapeClient
{
    public const string DefaultBaseUrl = "https://cad.onshape.com";
    private static readonly TimeSpan ExportPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExportPollTimeout = TimeSpan.FromSeconds(120);

    private readonly HttpClient _http;
    private string _accessKey;
    private string _secretKey;
    private readonly string _baseUrl;

    public OnshapeClient(HttpClient http, string accessKey, string secretKey, string? baseUrl = null)
    {
        _http = http;
        _accessKey = accessKey;
        _secretKey = secretKey;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
    }

    /// <summary>
    /// Updates API key credentials (startup configuration or trusted host integration only — not MCP tool args).
    /// </summary>
    public void SetCredentials(string accessKey, string secretKey)
    {
        _accessKey = accessKey;
        _secretKey = secretKey;
    }

    /// <summary>
    /// Sends an authenticated request (HTTP Basic, OpenAPI BasicAuth). Writes add <c>rejectMicroversionSkew=true</c> to the query string.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default)
    {
        var isWrite = method != HttpMethod.Get && method != HttpMethod.Head;
        var mergedQuery = MergeQuery(query, isWrite);
        var uriPath = path.StartsWith('/') ? path : "/" + path;
        var queryString = BuildQueryString(mergedQuery);

        var retried429 = false;
        while (true)
        {
            var requestUri = string.IsNullOrEmpty(queryString)
                ? $"{_baseUrl}{uriPath}"
                : $"{_baseUrl}{uriPath}?{queryString}";

            using var request = new HttpRequestMessage(method, requestUri);
            var basic = OnshapeBasicAuth.FormatAuthorizationHeader(_accessKey, _secretKey);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);

            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw OnshapeApiException.FromHttpStatus(503, $"transport_error:{ex.Message}");
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if ((int)response.StatusCode == 429 && !retried429)
            {
                TimeSpan? retryAfter = null;
                if (response.Headers.RetryAfter?.Delta is { } delta)
                {
                    retryAfter = delta;
                }

                if (retryAfter is { } wait && wait > TimeSpan.Zero)
                {
                    response.Dispose();
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    retried429 = true;
                    continue;
                }
            }

                using (response)
                {
                    await ThrowForResponseAsync(response, cancellationToken).ConfigureAwait(false);
                }

                return response;
            }
        }

    public async Task<JsonDocument> SendJsonAsync(
        HttpMethod method,
        string path,
        string? body = null,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(method, path, body, query, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls translation status every 2s until DONE or 120s timeout. Transient poll failures are retried until timeout.
    /// </summary>
    public async Task<Dictionary<string, object?>> PollTranslationAsync(
        string translationId,
        CancellationToken cancellationToken = default,
        TimeSpan? pollInterval = null,
        TimeSpan? pollTimeout = null)
    {
        var interval = pollInterval ?? ExportPollInterval;
        var timeout = pollTimeout ?? ExportPollTimeout;
        var path = OnshapeApiPaths.Translation(translationId);
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var doc = await SendJsonAsync(HttpMethod.Get, path, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var root = doc.RootElement;
                var state = root.TryGetProperty("requestState", out var stateEl)
                    ? stateEl.GetString()
                    : null;

                if (string.Equals(state, "DONE", StringComparison.OrdinalIgnoreCase))
                {
                    string? downloadUrl = null;
                    if (root.TryGetProperty("resultExternalDataIds", out var ids) && ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() > 0)
                    {
                        downloadUrl = ids[0].GetString();
                    }
                    else if (root.TryGetProperty("href", out var href))
                    {
                        downloadUrl = href.GetString();
                    }

                    return new Dictionary<string, object?>
                    {
                        ["requestState"] = state,
                        ["downloadUrl"] = downloadUrl,
                        ["translationId"] = translationId,
                    };
                }

                if (!string.Equals(state, "PENDING", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(state, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                    && state is not null)
                {
                    throw OnshapeApiException.FromHttpStatus(500, $"translation_failed:{state}");
                }
            }
            catch (OnshapeApiException ex) when (IsTransientPollFailure(ex))
            {
                // Continue polling until deadline.
            }
            catch (HttpRequestException)
            {
                // Continue polling until deadline.
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }

        throw OnshapeApiException.FromHttpStatus(408, "export_timeout");
    }

    private static bool IsTransientPollFailure(OnshapeApiException ex) =>
        ex.StatusCode >= 500 || ex.Reason.StartsWith("transport_error:", StringComparison.Ordinal);

    /// <summary>Test seam for HMAC canonical string (Onshape signing spec).</summary>
    public static string BuildCanonicalForTests(
        string method,
        string uri,
        string nonce,
        string date,
        string contentType,
        string body) =>
        OnshapeHmac.BuildCanonical(method, uri, nonce, date, contentType, body);

    /// <summary>Test seam for HMAC-SHA256 signature (Base64).</summary>
    public static string SignForTests(string canonical, string secretKey) =>
        OnshapeHmac.Sign(canonical, secretKey);

    /// <summary>Test seam for HTTP Basic credential payload (Base64 of access:secret).</summary>
    public static string FormatBasicAuthForTests(string accessKey, string secretKey) =>
        OnshapeBasicAuth.FormatAuthorizationHeader(accessKey, secretKey);

    private static Dictionary<string, string?> MergeQuery(IReadOnlyDictionary<string, string?>? query, bool isWrite)
    {
        var merged = query is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(query, StringComparer.Ordinal);

        if (isWrite)
        {
            merged["rejectMicroversionSkew"] = "true";
        }

        return merged;
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string?> query)
    {
        if (query.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var pair in query.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair.Value is null)
            {
                continue;
            }

            parts.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        }

        return string.Join('&', parts);
    }

    private static async Task ThrowForResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var reason = response.ReasonPhrase ?? "error";
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        TimeSpan? retryAfter = null;
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            retryAfter = delta;
        }

        string? serverMicroversion = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("microversion", out var mv))
                {
                    serverMicroversion = mv.GetString();
                }
                else if (doc.RootElement.TryGetProperty("message", out var msg))
                {
                    reason = msg.GetString() ?? reason;
                }
            }
            catch (JsonException)
            {
                if (body.Length <= 200)
                {
                    reason = body;
                }
            }
        }

        if (statusCode == 409 && string.IsNullOrEmpty(serverMicroversion))
        {
            reason = "wrongMicroversion";
        }

        throw OnshapeApiException.FromHttpStatus(statusCode, reason, retryAfter, serverMicroversion);
    }
}