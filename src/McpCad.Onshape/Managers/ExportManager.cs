using System.Text.Json;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// STEP/STL export via translation polling (sync surface).
/// </summary>
public sealed class ExportManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public ExportManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> ExportStep(CadElementAddress address, Dictionary<string, object?>? options = null)
    {
        var wvm = _driver.DefaultWvm;
        var body = options is null || options.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(options);
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioExportStep(
                    address.DocumentId,
                    wvm,
                    address.WorkspaceOrVersionId,
                    address.ElementId),
                body)
            .GetAwaiter().GetResult();
        var root = doc.RootElement;
        var translationId = root.TryGetProperty("id", out var idEl)
            ? idEl.GetString()
            : root.TryGetProperty("translationId", out var tidEl)
                ? tidEl.GetString()
                : null;
        if (string.IsNullOrEmpty(translationId))
        {
            return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(root));
        }

        var polled = _client.PollTranslationAsync(translationId).GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(polled);
    }

    public Dictionary<string, object?> ExportStl(CadElementAddress address, Dictionary<string, object?>? options = null)
    {
        var wvm = _driver.DefaultWvm;
        var body = options is null || options.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(options);
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioExportStl(
                    address.DocumentId,
                    wvm,
                    address.WorkspaceOrVersionId,
                    address.ElementId),
                body)
            .GetAwaiter().GetResult();
        var root = doc.RootElement;
        var translationId = root.TryGetProperty("id", out var idEl)
            ? idEl.GetString()
            : root.TryGetProperty("translationId", out var tidEl)
                ? tidEl.GetString()
                : null;
        if (string.IsNullOrEmpty(translationId))
        {
            return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(root));
        }

        var polled = _client.PollTranslationAsync(translationId).GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(polled);
    }
}