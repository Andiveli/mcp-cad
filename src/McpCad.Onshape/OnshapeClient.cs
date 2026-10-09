using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using McpCad.Core.Exceptions;

namespace McpCad.Onshape;

/// <summary>HTTP request boundary for the Onshape API.</summary>
public sealed class OnshapeClient : IDisposable
{
    public const string DefaultBaseUrl = "https://cad.onshape.com";

    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private string _accessKey;
    private string _secretKey;

    public OnshapeClient(string accessKey, string secretKey, string? baseUrl = null)
        : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }), accessKey, secretKey, baseUrl)
    {
    }

    // Test seam: production always owns a nonredirecting transport.
    internal OnshapeClient(HttpMessageHandler handler, string accessKey, string secretKey, string? baseUrl = null)
        : this(new HttpClient(handler, disposeHandler: true), accessKey, secretKey, baseUrl)
    {
    }

    private OnshapeClient(HttpClient http, string accessKey, string secretKey, string? baseUrl)
    {
        var configured = new Uri(baseUrl ?? DefaultBaseUrl, UriKind.Absolute);
        if (configured.Scheme != Uri.UriSchemeHttps || configured.UserInfo.Length != 0
            || configured.AbsolutePath != "/" || configured.Query.Length != 0 || configured.Fragment.Length != 0)
        {
            http.Dispose();
            throw new ArgumentException("The Onshape base URL must be an HTTPS origin.", nameof(baseUrl));
        }

        _http = http;
        _accessKey = accessKey;
        _secretKey = secretKey;
        _baseUri = new Uri(configured.GetLeftPart(UriPartial.Authority));
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Updates credentials from trusted host configuration, never tool arguments.</summary>
    public void SetCredentials(string accessKey, string secretKey)
    {
        _accessKey = accessKey;
        _secretKey = secretKey;
    }

    /// <summary>Sends an authenticated request; writes reject stale microversions.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default,
        bool acceptRedirect = false)
    {
        var mergedQuery = query is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(query, StringComparer.Ordinal);
        if (method != HttpMethod.Get && method != HttpMethod.Head)
        {
            mergedQuery["rejectMicroversionSkew"] = "true";
        }

        var uriPath = path.StartsWith('/') ? path : "/" + path;
        Uri? absoluteUri = null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var candidate)
            && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            if (!string.Equals(candidate.Scheme, _baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(candidate.IdnHost, _baseUri.IdnHost, StringComparison.OrdinalIgnoreCase)
                || candidate.Port != _baseUri.Port
                || candidate.UserInfo.Length != 0)
            {
                throw new ArgumentException("The request URL must use the configured Onshape origin.", nameof(path));
            }

            absoluteUri = candidate;
        }

        var builder = new UriBuilder(absoluteUri ?? new Uri(_baseUri, uriPath));
        var existingQuery = builder.Query.TrimStart('?');
        if (method != HttpMethod.Get && method != HttpMethod.Head
            && existingQuery.Split('&').Any(part =>
                string.Equals(Uri.UnescapeDataString(part.Split('=', 2)[0]), "rejectMicroversionSkew", StringComparison.Ordinal)))
        {
            throw new ArgumentException("The write skew guard must not be set in the request path.", nameof(path));
        }

        var newQuery = string.Join('&', mergedQuery.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        builder.Query = string.Join('&', new[] { existingQuery, newQuery }.Where(part => part.Length != 0));
        var requestUri = builder.Uri;
        if (!string.Equals(requestUri.Scheme, _baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(requestUri.IdnHost, _baseUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            || requestUri.Port != _baseUri.Port
            || requestUri.UserInfo.Length != 0)
        {
            throw new ArgumentException("The request URL must use the configured Onshape origin.", nameof(path));
        }

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", OnshapeBasicAuth.FormatAuthorizationHeader(_accessKey, _secretKey));
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

        if (response.IsSuccessStatusCode || (acceptRedirect && (int)response.StatusCode is >= 300 and < 400))
        {
            return response;
        }

        using (response)
        {
            var reason = response.ReasonPhrase ?? "error";
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (responseBody.Length is > 0 and <= 200)
            {
                reason = responseBody;
            }

            var retryAfter = response.Headers.RetryAfter?.Delta;
            throw OnshapeApiException.FromHttpStatus((int)response.StatusCode, reason, retryAfter);
        }
    }

    /// <summary>Returns a parsed JSON response; the caller owns the document.</summary>
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
}
