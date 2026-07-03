using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpCad.Core.Models;

/// <summary>
/// Stable address for a CAD element in a web backend (Onshape did/wvm/wvmid/eid).
/// </summary>
public sealed record CadElementAddress(
    string DocumentId,
    string WorkspaceOrVersionId,
    string? MicroversionId,
    string ElementId)
{
    public static CadElementAddress Deserialize(string json)
    {
        var addr = JsonSerializer.Deserialize<CadElementAddressDto>(json, JsonOptions)
            ?? throw new JsonException("CadElementAddress payload was null.");
        return new CadElementAddress(
            addr.DocumentId,
            addr.WorkspaceOrVersionId,
            addr.MicroversionId,
            addr.ElementId);
    }

    public string Serialize()
    {
        return JsonSerializer.Serialize(
            new CadElementAddressDto(DocumentId, WorkspaceOrVersionId, MicroversionId, ElementId),
            JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record CadElementAddressDto(
        string DocumentId,
        string WorkspaceOrVersionId,
        string? MicroversionId,
        string ElementId);
}