using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;

namespace McpCad.Tests.Mocks;

/// <summary>
/// Mock <see cref="ICadWebProvider"/> with canned success responses (same pattern as <see cref="MockInventorProvider"/>).
/// </summary>
public sealed class MockOnshapeProvider : ICadWebProvider
{
    public List<(string Method, Dictionary<string, object?> Args)> CallLog { get; } = new();

    private Dictionary<string, object?>? _errorResult;

    public static MockOnshapeProvider WithError(string message) =>
        new() { _throwMessage = message };

    private string? _throwMessage;

    private void ThrowIfError()
    {
        if (_throwMessage is not null)
        {
            throw new CadProviderOperationException(_throwMessage);
        }
    }

    private Dictionary<string, object?> Ok(Dictionary<string, object?>? extra = null)
    {
        if (_errorResult is not null)
        {
            return _errorResult;
        }

        var d = new Dictionary<string, object?> { ["success"] = true };
        if (extra is not null)
        {
            foreach (var pair in extra)
            {
                d[pair.Key] = pair.Value;
            }
        }

        return d;
    }

    public Dictionary<string, object?> Authenticate(string accessKey, string secretKeyId, string secretKey)
    {
        CallLog.Add(("Authenticate", new Dictionary<string, object?> { ["accessKey"] = accessKey }));
        return Ok(new Dictionary<string, object?> { ["accessKey"] = accessKey });
    }

    public Dictionary<string, object?> Connect()
    {
        CallLog.Add(("Connect", new()));
        return Ok(new Dictionary<string, object?> { ["connected"] = true });
    }

    public Dictionary<string, object?> Disconnect()
    {
        CallLog.Add(("Disconnect", new()));
        return Ok(new Dictionary<string, object?> { ["connected"] = false });
    }

    public Dictionary<string, object?> Health()
    {
        CallLog.Add(("Health", new()));
        return Ok(new Dictionary<string, object?> { ["provider"] = "mock-onshape" });
    }

    public Dictionary<string, object?> DocOpen(string path)
    {
        CallLog.Add(("DocOpen", new Dictionary<string, object?> { ["path"] = path }));
        return Ok(new Dictionary<string, object?> { ["documentId"] = path });
    }

    public Dictionary<string, object?> DocCreate(string name)
    {
        ThrowIfError();
        CallLog.Add(("DocCreate", new Dictionary<string, object?> { ["name"] = name }));
        return Ok(new Dictionary<string, object?>
        {
            ["name"] = name,
            ["documentId"] = "mock-created-doc",
            ["workspaceId"] = "mock-workspace",
        });
    }

    public Dictionary<string, object?> DocNewPart(string template = "")
    {
        CallLog.Add(("DocNewPart", new Dictionary<string, object?> { ["template"] = template }));
        return Ok();
    }

    public Dictionary<string, object?> DocNewAssembly(string template = "")
    {
        CallLog.Add(("DocNewAssembly", new Dictionary<string, object?> { ["template"] = template }));
        return Ok();
    }

    public Dictionary<string, object?> DocSave()
    {
        CallLog.Add(("DocSave", new()));
        return Ok();
    }

    public Dictionary<string, object?> DocSaveAs(string path)
    {
        CallLog.Add(("DocSaveAs", new Dictionary<string, object?> { ["path"] = path }));
        return Ok();
    }

    public Dictionary<string, object?> DocClose(bool save = true)
    {
        CallLog.Add(("DocClose", new Dictionary<string, object?> { ["save"] = save }));
        return Ok();
    }

    public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null)
    {
        CallLog.Add(("ExportStep", new Dictionary<string, object?> { ["path"] = path }));
        return Ok(new Dictionary<string, object?> { ["path"] = path });
    }

    public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null)
    {
        CallLog.Add(("ExportStl", new Dictionary<string, object?> { ["path"] = path }));
        return Ok();
    }

    public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null)
    {
        CallLog.Add(("ExportPdf", new Dictionary<string, object?> { ["path"] = path }));
        return Ok();
    }

    public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null)
    {
        CallLog.Add(("ExportDxf", new Dictionary<string, object?> { ["path"] = path }));
        return Ok();
    }

    public Dictionary<string, object?> WsList(string? documentId = null)
    {
        CallLog.Add(("WsList", new Dictionary<string, object?> { ["documentId"] = documentId }));
        return Ok();
    }

    public Dictionary<string, object?> WsCurrent(string documentId)
    {
        CallLog.Add(("WsCurrent", new Dictionary<string, object?> { ["documentId"] = documentId }));
        return Ok(new Dictionary<string, object?> { ["documentId"] = documentId });
    }

    public Dictionary<string, object?> ElementList(string documentId, string wvm, string wvmId)
    {
        CallLog.Add(("ElementList", new Dictionary<string, object?>
        {
            ["documentId"] = documentId,
            ["wvm"] = wvm,
            ["wvmId"] = wvmId,
        }));
        return Ok();
    }

    public Dictionary<string, object?> ElementGet(CadElementAddress address)
    {
        CallLog.Add(("ElementGet", new Dictionary<string, object?> { ["address"] = address.Serialize() }));
        return Ok(new Dictionary<string, object?> { ["elementId"] = address.ElementId });
    }

    public Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType)
    {
        CallLog.Add(("ElementCreate", new Dictionary<string, object?>
        {
            ["name"] = name,
            ["elementType"] = elementType,
        }));
        return Ok(new Dictionary<string, object?> { ["name"] = name, ["elementType"] = elementType });
    }

    public Dictionary<string, object?> ElementRename(CadElementAddress address, string name)
    {
        CallLog.Add(("ElementRename", new Dictionary<string, object?> { ["name"] = name }));
        return Ok(new Dictionary<string, object?> { ["name"] = name });
    }

    public Dictionary<string, object?> DocCopy(string documentId, string? name = null)
    {
        CallLog.Add(("DocCopy", new Dictionary<string, object?> { ["documentId"] = documentId, ["name"] = name }));
        return Ok();
    }

        public Dictionary<string, object?> DocDelete(string documentId, bool confirm)
        {
            if (!confirm)
            {
                throw new CadProviderOperationException(
                    "onshape_doc_delete requires confirm=true. Use least-privilege API keys; deletion is irreversible.");
            }

            ThrowIfError();
            CallLog.Add(("DocDelete", new Dictionary<string, object?> { ["documentId"] = documentId, ["confirm"] = confirm }));
            return Ok();
        }

    public Dictionary<string, object?> VersionCreate(string documentId, string? name = null)
    {
        CallLog.Add(("VersionCreate", new Dictionary<string, object?> { ["documentId"] = documentId }));
        return Ok();
    }

    public Dictionary<string, object?> MicroversionGet(CadElementAddress address)
    {
        CallLog.Add(("MicroversionGet", new Dictionary<string, object?> { ["elementId"] = address.ElementId }));
        return Ok(new Dictionary<string, object?> { ["microversion"] = "mock-mv" });
    }

    public Dictionary<string, object?> ExportStep(CadElementAddress address, Dictionary<string, object?>? options = null)
    {
        CallLog.Add(("ExportStep", new Dictionary<string, object?> { ["elementId"] = address.ElementId }));
        return Ok(new Dictionary<string, object?> { ["downloadUrl"] = "https://mock/step" });
    }

    public Dictionary<string, object?> ExportStl(CadElementAddress address, Dictionary<string, object?>? options = null)
    {
        CallLog.Add(("ExportStl", new Dictionary<string, object?> { ["elementId"] = address.ElementId }));
        return Ok();
    }

    public Dictionary<string, object?> SketchReplace(
        CadElementAddress address,
        IReadOnlyDictionary<string, object?>? sketchRequest = null)
    {
        ThrowIfError();
        CallLog.Add(("SketchReplace", new Dictionary<string, object?>
        {
            ["elementId"] = address.ElementId,
            ["hasSketchRequest"] = sketchRequest is { Count: > 0 },
            ["sketchRequest"] = sketchRequest is null ? null : System.Text.Json.JsonSerializer.SerializeToElement(sketchRequest),
        }));
        return Ok(new Dictionary<string, object?> { ["elementId"] = address.ElementId });
    }

    public Dictionary<string, object?> FeatureAdd(
        CadElementAddress partStudio,
        string featureType,
        IReadOnlyList<Dictionary<string, object?>>? parameters = null,
        string? featurePostBodyJson = null)
    {
        ThrowIfError();
        CallLog.Add(("FeatureAdd", new Dictionary<string, object?>
        {
            ["featureType"] = featureType,
            ["hasEnvelope"] = !string.IsNullOrEmpty(featurePostBodyJson),
        }));
        return Ok(new Dictionary<string, object?> { ["featureType"] = featureType });
    }

    public Dictionary<string, object?> FeatureList(CadElementAddress partStudio)
    {
        ThrowIfError();
        CallLog.Add(("FeatureList", new Dictionary<string, object?> { ["elementId"] = partStudio.ElementId }));
        return Ok(new Dictionary<string, object?> { ["features"] = Array.Empty<object>() });
    }

    public Dictionary<string, object?> InspectEdges(CadElementAddress partStudio, string? bodyId = null)
        {
            ThrowIfError();
            CallLog.Add(("InspectEdges", new Dictionary<string, object?>
            {
                ["address"] = partStudio.Serialize(), ["bodyId"] = bodyId,
            }));
            return Ok(new Dictionary<string, object?> { ["operation"] = "inspect_edges", ["edges"] = Array.Empty<object>() });
        }

        public Dictionary<string, object?> InspectFaces(CadElementAddress partStudio, string? bodyId = null)
        {
            ThrowIfError();
            CallLog.Add(("InspectFaces", new Dictionary<string, object?>
            {
                ["address"] = partStudio.Serialize(), ["bodyId"] = bodyId,
            }));
            return Ok(new Dictionary<string, object?> { ["operation"] = "inspect_faces", ["faces"] = Array.Empty<object>() });
        }

        public Dictionary<string, object?> AssemblyListInstances(CadElementAddress assemblyElement)
    {
        ThrowIfError();
        CallLog.Add(("AssemblyListInstances", new Dictionary<string, object?> { ["elementId"] = assemblyElement.ElementId }));
        return Ok(new Dictionary<string, object?> { ["instances"] = Array.Empty<object>() });
    }

    public Dictionary<string, object?> AssemblyCreateInstance(
        CadElementAddress assemblyElement,
        string partDocumentId,
        string partElementId)
    {
        ThrowIfError();
        CallLog.Add(("AssemblyCreateInstance", new Dictionary<string, object?>
        {
            ["partDocumentId"] = partDocumentId,
            ["partElementId"] = partElementId,
        }));
        return Ok();
    }

    public Dictionary<string, object?> AssemblyDeleteInstance(CadElementAddress assemblyElement, string instanceId)
    {
        ThrowIfError();
        CallLog.Add(("AssemblyDeleteInstance", new Dictionary<string, object?> { ["instanceId"] = instanceId }));
        return Ok();
    }
}
