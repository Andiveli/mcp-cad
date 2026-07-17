using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Builders;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tools.Onshape;

/// <summary>
/// Atomic MCP tools for Onshape (cloud CAD). Consumes <see cref="ICadWebProvider"/>.
/// API keys are loaded at server startup from configuration / environment / user secrets — not from MCP tool arguments.
/// </summary>
[McpServerToolType]
public class OnshapeAtomicTools(ICadWebProvider provider)
{
    [McpServerTool, Description("Connect / mark Onshape session ready.")]
    public Dictionary<string, object?> onshape_connect()
        => Catch(provider.Connect);

    [McpServerTool, Description("Disconnect Onshape session marker.")]
    public Dictionary<string, object?> onshape_disconnect()
        => Catch(provider.Disconnect);

    [McpServerTool, Description("Onshape provider health.")]
    public Dictionary<string, object?> onshape_health()
        => Catch(provider.Health);

    [McpServerTool, Description("Open an Onshape document by document id.")]
    public Dictionary<string, object?> onshape_doc_open(string document_id)
        => Catch(() => provider.DocOpen(document_id));

    [McpServerTool, Description("List workspaces for a document.")]
    public Dictionary<string, object?> onshape_ws_list(string? document_id = null)
        => Catch(() => provider.WsList(document_id));

    [McpServerTool, Description("Get current workspace for a document.")]
    public Dictionary<string, object?> onshape_ws_current(string document_id)
        => Catch(() => provider.WsCurrent(document_id));

    [McpServerTool, Description("List elements in a document workspace/version.")]
    public Dictionary<string, object?> onshape_element_list(string document_id, string wvm, string wvm_id)
        => Catch(() => provider.ElementList(document_id, wvm, wvm_id));

    [McpServerTool, Description("Get element metadata by address JSON (CadElementAddress).")]
    public Dictionary<string, object?> onshape_element_get(string address_json)
        => Catch(() => provider.ElementGet(ParseAddress(address_json)));

    [McpServerTool, Description("Create a Part Studio or other element.")]
    public Dictionary<string, object?> onshape_element_create(
        string address_json, string name, string element_type = "PARTSTUDIO")
        => Catch(() => provider.ElementCreate(ParseAddress(address_json), name, element_type));

    [McpServerTool, Description("Rename an element.")]
    public Dictionary<string, object?> onshape_element_rename(string address_json, string name)
        => Catch(() => provider.ElementRename(ParseAddress(address_json), name));

    [McpServerTool, Description("Create a rectangle sketch in a Part Studio using BTMSketch. Coordinates in cm.")]
    public Dictionary<string, object?> onshape_sketch_rectangle(
        string address_json,
        double x1_cm,
        double y1_cm,
        double x2_cm,
        double y2_cm,
        string? plane = null)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var sketch = OnshapeSketchBuilder.BuildRectangle(
                OnshapeUnitConversion.CmToMeters(x1_cm),
                OnshapeUnitConversion.CmToMeters(y1_cm),
                OnshapeUnitConversion.CmToMeters(x2_cm),
                OnshapeUnitConversion.CmToMeters(y2_cm));
            var request = JsonSerializer.Deserialize<Dictionary<string, object?>>(sketch.RootElement.GetRawText())
                          ?? new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(plane))
            {
                request["plane"] = plane;
            }

            return provider.SketchReplace(address, request);
        });

    [McpServerTool, Description("Extrude a sketch profile by a given depth in cm.")]
    public Dictionary<string, object?> onshape_extrude(
        string address_json,
        string sketch_feature_id,
        double depth_cm,
        string direction = "positive",
        string boolean_op = "new")
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.ExtrudeFromSketch(
                sketch_feature_id,
                OnshapeUnitConversion.CmToMeters(depth_cm),
                direction,
                boolean_op);
            return provider.FeatureAdd(address, "extrude", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("List features in a Part Studio (analogous to Inventor sketch_profiles).")]
    public Dictionary<string, object?> onshape_feature_list(string address_json)
        => Catch(() => provider.FeatureList(ParseAddress(address_json)));

    [McpServerTool, Description("DEPRECATED: use onshape_sketch_rectangle. Creates a default 10cm rectangle sketch when corners omitted.")]
    public Dictionary<string, object?> create_sketch(
        string address_json,
        double? x1_cm = null,
        double? y1_cm = null,
        double? x2_cm = null,
        double? y2_cm = null)
        => onshape_sketch_rectangle(
            address_json,
            x1_cm ?? 0,
            y1_cm ?? 0,
            x2_cm ?? 10,
            y2_cm ?? 10);

    [McpServerTool, Description("DEPRECATED: use onshape_extrude. Extrudes using legacy distance_cm; requires sketch_feature_id.")]
    public Dictionary<string, object?> add_extrude(
        string address_json,
        double distance_cm,
        string direction = "positive",
        string sketch_feature_id = "sketch-1")
        => onshape_extrude(address_json, sketch_feature_id, distance_cm, direction);

    [McpServerTool, Description("Export Part Studio to STEP (polls translation, blocking).")]
    public Dictionary<string, object?> onshape_export_step(string address_json)
        => Catch(() => provider.ExportStep(ParseAddress(address_json)));

    [McpServerTool, Description("Export Part Studio to STL (polls translation, blocking).")]
    public Dictionary<string, object?> onshape_export_stl(string address_json)
        => Catch(() => provider.ExportStl(ParseAddress(address_json)));

    [McpServerTool, Description("Copy an Onshape document.")]
    public Dictionary<string, object?> onshape_doc_copy(string document_id, string? name = null)
        => Catch(() => provider.DocCopy(document_id, name));

    [McpServerTool, Description("Delete an Onshape document (requires confirm=true; irreversible).")]
    public Dictionary<string, object?> onshape_doc_delete(string document_id, bool confirm = false)
        => Catch(() => provider.DocDelete(document_id, confirm));

    [McpServerTool, Description("Create a document version.")]
    public Dictionary<string, object?> onshape_version_create(string document_id, string? name = null)
        => Catch(() => provider.VersionCreate(document_id, name));

    [McpServerTool, Description("Read microversion for an element.")]
    public Dictionary<string, object?> onshape_microversion_get(string address_json)
        => Catch(() => provider.MicroversionGet(ParseAddress(address_json)));

    private static CadElementAddress ParseAddress(string addressJson) =>
        CadElementAddress.Deserialize(addressJson);

    private static Dictionary<string, object?> Catch(Func<Dictionary<string, object?>> action)
    {
        try
        {
            return action();
        }
        catch (CadProviderException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error: {ex.Message}");
        }
    }
}