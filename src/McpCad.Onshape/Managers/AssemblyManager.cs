using System.Text.Json;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// Assembly element instances and CRUD stubs.
/// </summary>
public sealed class AssemblyManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public AssemblyManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> ListInstances(CadElementAddress assemblyElement)
    {
        var wvm = _driver.DefaultWvm;
        using var doc = _client.SendJsonAsync(
                HttpMethod.Get,
                OnshapeApiPaths.Assembly(
                    assemblyElement.DocumentId,
                    wvm,
                    assemblyElement.WorkspaceOrVersionId,
                    assemblyElement.ElementId))
            .GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> CreateInstance(
        CadElementAddress assemblyElement,
        string partDocumentId,
        string partElementId)
    {
        var wvm = _driver.DefaultWvm;
        var body = JsonSerializer.Serialize(new
        {
            documentId = partDocumentId,
            elementId = partElementId,
            isWholePartStudio = true,
            includePartTypes = new[] { "PARTS" },
        });
        using var response = _client.SendAsync(
                HttpMethod.Post,
                OnshapeApiPaths.AssemblyInstances(
                    assemblyElement.DocumentId,
                    assemblyElement.WorkspaceOrVersionId,
                    assemblyElement.ElementId),
                body)
            .GetAwaiter().GetResult();
        var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                _driver.UpdateMicroversionFromJson(doc.RootElement);
            }

            return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
        }

        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["documentId"] = assemblyElement.DocumentId,
            ["workspaceId"] = assemblyElement.WorkspaceOrVersionId,
            ["assemblyElementId"] = assemblyElement.ElementId,
            ["sourceDocumentId"] = partDocumentId,
            ["sourceElementId"] = partElementId,
        });
    }

    public Dictionary<string, object?> DeleteInstance(CadElementAddress assemblyElement, string instanceId)
    {
        var path = OnshapeApiPaths.AssemblyInstance(
            assemblyElement.DocumentId,
            assemblyElement.WorkspaceOrVersionId,
            assemblyElement.ElementId,
            instanceId);
        _client.SendAsync(HttpMethod.Delete, path).GetAwaiter().GetResult().Dispose();
        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?> { ["instanceId"] = instanceId });
    }
}
