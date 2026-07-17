using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape.Managers;

namespace McpCad.Onshape;

/// <summary>
/// Implements <see cref="ICadWebProvider"/> by delegating to domain managers.
/// </summary>
public sealed class OnshapeProvider : ICadWebProvider
{
    private readonly OnshapeDriver _driver;
    private readonly DocumentManager _documents;
    private readonly PartStudioManager _partStudio;
    private readonly AssemblyManager _assembly;
    private readonly ExportManager _export;
    private readonly FeatureManager _features;

    public OnshapeProvider(
        OnshapeClient client,
        OnshapeDriver driver,
        DocumentManager documents,
        PartStudioManager partStudio,
        AssemblyManager assembly,
        ExportManager export,
        FeatureManager features)
    {
        _driver = driver;
        _documents = documents;
        _partStudio = partStudio;
        _assembly = assembly;
        _export = export;
        _features = features;
    }

    public OnshapeProvider(OnshapeClient client, OnshapeDriver driver)
        : this(
            client,
            driver,
            new DocumentManager(client, driver),
            new PartStudioManager(client, driver),
            new AssemblyManager(client, driver),
            new ExportManager(client, driver),
            new FeatureManager(client, driver))
    {
    }

    public Dictionary<string, object?> Authenticate(string accessKey, string secretKeyId, string secretKey) =>
        _driver.Authenticate(accessKey, secretKeyId, secretKey);

    public Dictionary<string, object?> Connect() =>
        OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?> { ["connected"] = true });

    public Dictionary<string, object?> Disconnect() =>
        OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?> { ["connected"] = false });

    public Dictionary<string, object?> Health() =>
        OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["provider"] = "onshape",
            ["accessKeyConfigured"] = !string.IsNullOrEmpty(_driver.AccessKey),
        });

    public Dictionary<string, object?> DocOpen(string path) => _documents.DocOpen(path);

    public Dictionary<string, object?> DocNewPart(string template = "") =>
        NotSupportedLocalDoc("DocNewPart");

    public Dictionary<string, object?> DocNewAssembly(string template = "") =>
        NotSupportedLocalDoc("DocNewAssembly");

    public Dictionary<string, object?> DocSave() => NotSupportedLocalDoc("DocSave");

    public Dictionary<string, object?> DocSaveAs(string path) => NotSupportedLocalDoc("DocSaveAs");

    public Dictionary<string, object?> DocClose(bool save = true) => NotSupportedLocalDoc("DocClose");

    public Dictionary<string, object?> DocCopy(string documentId, string? name = null) =>
        _documents.DocCopy(documentId, name);

    public Dictionary<string, object?> DocDelete(string documentId, bool confirm)
    {
        if (!confirm)
        {
            throw new CadProviderOperationException(
                "onshape_doc_delete requires confirm=true. Use least-privilege API keys; deletion is irreversible.");
        }

        return _documents.DocDelete(documentId);
    }

    public Dictionary<string, object?> WsList(string? documentId = null) => _documents.WsList(documentId);

    public Dictionary<string, object?> WsCurrent(string documentId) => _documents.WsCurrent(documentId);

    public Dictionary<string, object?> ElementList(string documentId, string wvm, string wvmId) =>
        _partStudio.ElementList(documentId, wvm, wvmId);

    public Dictionary<string, object?> ElementGet(CadElementAddress address) => _partStudio.ElementGet(address);

    public Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType) =>
        _partStudio.ElementCreate(address, name, elementType);

    public Dictionary<string, object?> ElementRename(CadElementAddress address, string name) =>
        _partStudio.ElementRename(address, name);

    public Dictionary<string, object?> VersionCreate(string documentId, string? name = null) =>
        _documents.VersionCreate(documentId, name);

    public Dictionary<string, object?> MicroversionGet(CadElementAddress address) =>
        _partStudio.MicroversionGet(address);

    public Dictionary<string, object?> SketchReplace(
        CadElementAddress address,
        IReadOnlyDictionary<string, object?>? sketchRequest = null) =>
        _partStudio.SketchReplace(address, SerializeSketchRequest(sketchRequest));

    private static string? SerializeSketchRequest(IReadOnlyDictionary<string, object?>? sketchRequest)
    {
        if (sketchRequest is null || sketchRequest.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(sketchRequest);
    }

    public Dictionary<string, object?> FeatureAdd(
        CadElementAddress partStudio,
        string featureType,
        IReadOnlyList<Dictionary<string, object?>>? parameters = null,
        string? featurePostBodyJson = null) =>
        _features.FeatureAdd(partStudio, featureType, parameters, featurePostBodyJson);

    public Dictionary<string, object?> FeatureList(CadElementAddress partStudio) =>
        _features.FeatureList(partStudio);

    public Dictionary<string, object?> AssemblyListInstances(CadElementAddress assemblyElement) =>
        _assembly.ListInstances(assemblyElement);

    public Dictionary<string, object?> AssemblyCreateInstance(
        CadElementAddress assemblyElement,
        string partDocumentId,
        string partElementId) =>
        _assembly.CreateInstance(assemblyElement, partDocumentId, partElementId);

    public Dictionary<string, object?> AssemblyDeleteInstance(CadElementAddress assemblyElement, string instanceId) =>
        _assembly.DeleteInstance(assemblyElement, instanceId);

    public Dictionary<string, object?> ExportStep(CadElementAddress address, Dictionary<string, object?>? options = null) =>
        _export.ExportStep(address, options);

    public Dictionary<string, object?> ExportStl(CadElementAddress address, Dictionary<string, object?>? options = null) =>
        _export.ExportStl(address, options);

    public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null) =>
        NotSupportedPathExport("ExportStep", path);

    public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null) =>
        NotSupportedPathExport("ExportStl", path);

    public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null) =>
        NotSupportedPathExport("ExportPdf", path);

    public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null) =>
        NotSupportedPathExport("ExportDxf", path);

    private static Dictionary<string, object?> NotSupportedLocalDoc(string method) =>
        new()
        {
            ["success"] = false,
            ["error"] = $"{method} is not supported for Onshape; use cloud document APIs (DocOpen with document id).",
        };

    private static Dictionary<string, object?> NotSupportedPathExport(string method, string path) =>
        new()
        {
            ["success"] = false,
            ["error"] = $"{method} with local path is not supported; use {method}(CadElementAddress, options).",
            ["path"] = path,
        };
}