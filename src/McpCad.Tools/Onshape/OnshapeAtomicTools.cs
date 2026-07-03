using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
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

    [McpServerTool, Description("Create or replace sketch payload on a Part Studio (v1 stub).")]
    public Dictionary<string, object?> create_sketch(string address_json)
        => Catch(() => provider.SketchReplace(ParseAddress(address_json)));

    [McpServerTool, Description("Add a feature (e.g. extrude) to a Part Studio.")]
    public Dictionary<string, object?> add_extrude(
        string address_json,
        double distance_cm,
        string direction = "positive")
        => Catch(() => provider.FeatureAdd(
            ParseAddress(address_json),
            "extrude",
            new[]
            {
                new Dictionary<string, object?>
                {
                    ["distanceCm"] = distance_cm,
                    ["direction"] = direction,
                },
            }));

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