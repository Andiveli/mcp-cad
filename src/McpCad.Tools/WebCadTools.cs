using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tools;

/// <summary>Web CAD operations (Onshape) dispatched via <see cref="IProviderRouter"/>.</summary>
[McpServerToolType]
public class WebCadTools(IProviderRouter router)
{
    [McpServerTool, Description("List workspaces for a document (web CAD).")]
    public Dictionary<string, object?> ws_list(string? document_id = null)
        => RouterToolDispatch.CatchWeb(router, web => web.WsList(document_id));

    [McpServerTool, Description("List elements in a document workspace/version (web CAD).")]
    public Dictionary<string, object?> element_list(string document_id, string wvm, string wvm_id)
        => RouterToolDispatch.CatchWeb(router, web => web.ElementList(document_id, wvm, wvm_id));

    [McpServerTool, Description("Create or replace sketch payload on a Part Studio (web CAD).")]
    public Dictionary<string, object?> sketch_replace(string address_json)
        => RouterToolDispatch.CatchWeb(router, web => web.SketchReplace(ParseAddress(address_json)));

    [McpServerTool, Description("Add a feature (e.g. extrude) to a Part Studio (web CAD).")]
    public Dictionary<string, object?> feature_add(
        string address_json,
        string feature_type,
        string? parameters_json = null)
    {
        return RouterToolDispatch.CatchWeb(router, web =>
        {
            IReadOnlyList<Dictionary<string, object?>>? parameters = null;
            if (!string.IsNullOrWhiteSpace(parameters_json))
            {
                parameters = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(parameters_json);
            }

            return web.FeatureAdd(ParseAddress(address_json), feature_type, parameters);
        });
    }

    private static CadElementAddress ParseAddress(string addressJson) =>
        CadElementAddress.Deserialize(addressJson);
}