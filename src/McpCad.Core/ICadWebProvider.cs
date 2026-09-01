using McpCad.Core.Models;

namespace McpCad.Core;

/// <summary>
/// Provider contract for cloud-native CAD backends (Onshape, future Fusion 360 / 3DExperience).
/// Sibling of <see cref="IMechanicalCadProvider"/> — extends only <see cref="ICadProvider"/>.
/// </summary>
public interface ICadWebProvider : ICadProvider
{
    Dictionary<string, object?> Authenticate(string accessKey, string secretKeyId, string secretKey);

    Dictionary<string, object?> WsList(string? documentId = null);

    Dictionary<string, object?> WsCurrent(string documentId);

    Dictionary<string, object?> ElementList(string documentId, string wvm, string wvmId);

    Dictionary<string, object?> ElementGet(CadElementAddress address);

    Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType);

    Dictionary<string, object?> ElementRename(CadElementAddress address, string name);

    Dictionary<string, object?> DocCreate(string name);

    Dictionary<string, object?> DocCopy(string documentId, string? name = null);

    Dictionary<string, object?> DocDelete(string documentId, bool confirm);

    Dictionary<string, object?> VersionCreate(string documentId, string? name = null);

    Dictionary<string, object?> MicroversionGet(CadElementAddress address);

    Dictionary<string, object?> ExportStep(CadElementAddress address, Dictionary<string, object?>? options = null);

    Dictionary<string, object?> ExportStl(CadElementAddress address, Dictionary<string, object?>? options = null);

    Dictionary<string, object?> SketchReplace(
        CadElementAddress address,
        IReadOnlyDictionary<string, object?>? sketchRequest = null);

    Dictionary<string, object?> FeatureAdd(
        CadElementAddress partStudio,
        string featureType,
        IReadOnlyList<Dictionary<string, object?>>? parameters = null,
        string? featurePostBodyJson = null);

    Dictionary<string, object?> FeatureList(CadElementAddress partStudio);

    Dictionary<string, object?> InspectEdges(CadElementAddress partStudio, string? bodyId = null);

    Dictionary<string, object?> InspectFaces(CadElementAddress partStudio, string? bodyId = null);

    Dictionary<string, object?> CaptureView(
        CadElementAddress partStudio,
        string view,
        int width,
        int height,
        string edges);

    Dictionary<string, object?> AssemblyListInstances(CadElementAddress assemblyElement);

    Dictionary<string, object?> AssemblyCreateInstance(
        CadElementAddress assemblyElement,
        string partDocumentId,
        string partElementId);

    Dictionary<string, object?> AssemblyDeleteInstance(CadElementAddress assemblyElement, string instanceId);
}