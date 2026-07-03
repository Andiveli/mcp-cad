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
                OnshapeApiPaths.AssemblyInstances(
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
        });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.AssemblyInstances(
                    assemblyElement.DocumentId,
                    wvm,
                    assemblyElement.WorkspaceOrVersionId,
                    assemblyElement.ElementId),
                body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> DeleteInstance(CadElementAddress assemblyElement, string instanceId)
    {
        var wvm = _driver.DefaultWvm;
        var path =
            $"{OnshapeApiPaths.AssemblyInstances(assemblyElement.DocumentId, wvm, assemblyElement.WorkspaceOrVersionId, assemblyElement.ElementId)}/{Uri.EscapeDataString(instanceId)}";
        _client.SendAsync(HttpMethod.Delete, path).GetAwaiter().GetResult().Dispose();
        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?> { ["instanceId"] = instanceId });
    }
}