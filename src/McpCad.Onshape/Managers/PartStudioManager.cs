using System.Text.Json;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// Part Studio element CRUD and sketch replace (REST).
/// </summary>
public sealed class PartStudioManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public PartStudioManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> ElementList(string documentId, string wvm, string wvmId)
    {
        using var doc = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Elements(documentId, wvm, wvmId))
            .GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> ElementGet(CadElementAddress address)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        using var doc = _client.SendJsonAsync(
                HttpMethod.Get,
                OnshapeApiPaths.Elements(did, wvm, wvmId),
                query: new Dictionary<string, string?> { ["elementId"] = address.ElementId })
            .GetAwaiter().GetResult();

        var element = FindElement(doc.RootElement, address.ElementId);
        if (element is null)
        {
            throw new CadProviderOperationException(
                $"Onshape element '{address.ElementId}' was not found in {wvm} '{wvmId}'.");
        }

        _driver.UpdateMicroversionFromJson(element.Value);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(element.Value));
    }

    public Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType)
    {
        if (!string.Equals(elementType, "PARTSTUDIO", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Onshape Part Studio creation only supports elementType 'PARTSTUDIO'.",
                nameof(elementType));
        }

        var (did, _, workspaceId) = ResolveWvm(address);
        var body = JsonSerializer.Serialize(new { name });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioCreate(did, workspaceId),
                body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> ElementRename(CadElementAddress address, string name)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        var body = JsonSerializer.Serialize(new { name });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.Element(did, wvm, wvmId, address.ElementId),
                body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> MicroversionGet(CadElementAddress address)
    {
        var result = ElementGet(address);
        if (_driver.LastMicroversion is { } mv)
        {
            result["microversion"] = mv;
        }

        return result;
    }

    public Dictionary<string, object?> SketchReplace(CadElementAddress address, string? sketchBodyJson)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        var path = OnshapeApiPaths.PartStudioFeatures(did, wvm, wvmId, address.ElementId);
        var body = string.IsNullOrWhiteSpace(sketchBodyJson)
            ? """{"entities":[],"constraints":[]}"""
            : sketchBodyJson;
        using var doc = _client.SendJsonAsync(HttpMethod.Post, path, body).GetAwaiter().GetResult();
        ThrowIfFeatureFailed(doc.RootElement);
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> InspectEdges(CadElementAddress address, string? bodyId = null) =>
        InspectTopology(address, bodyId, "inspect_edges");

    public Dictionary<string, object?> InspectFaces(CadElementAddress address, string? bodyId = null) =>
        InspectTopology(address, bodyId, "inspect_faces");

    private Dictionary<string, object?> InspectTopology(CadElementAddress address, string? bodyId, string operation)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        using var doc = _client.SendJsonAsync(HttpMethod.Get,
                OnshapeApiPaths.PartStudioBodyDetails(did, wvm, wvmId, address.ElementId))
            .GetAwaiter().GetResult();
        var bodies = ReadBodies(doc.RootElement);
        var selected = bodyId is null ? bodies : bodies.Where(body => body.Id == bodyId).ToList();
        if (bodyId is not null && selected.Count == 0)
        {
            throw new CadProviderOperationException($"Onshape body '{bodyId}' was not found.");
        }

        var edgeRecords = selected.SelectMany(body => ReadArray(body.Element, "edges").Select(edge => EdgeRecord(body, edge)))
            .OrderBy(record => (string?)record["topologyId"], StringComparer.Ordinal).ToList();
        var faceRecords = selected.SelectMany(body => ReadArray(body.Element, "faces").Select(face => FaceRecord(body, face)))
            .OrderBy(record => (string?)record["topologyId"], StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, object?>
        {
            ["operation"] = operation,
            ["source"] = new Dictionary<string, object?>
            {
                ["address"] = address.Serialize(), ["effectiveWvm"] = wvm, ["effectiveWvmId"] = wvmId,
                ["responseMicroversion"] = MicroversionId(doc.RootElement),
            },
            ["units"] = new Dictionary<string, object?> { ["length"] = "cm", ["area"] = "cm2", ["coordinates"] = "cm" },
            ["bodyFilter"] = bodyId,
            ["bodies"] = selected.OrderBy(body => body.Id, StringComparer.Ordinal).Select(BodySummary).ToList(),
            ["edges"] = edgeRecords,
            ["faces"] = faceRecords,
            ["totals"] = new Dictionary<string, object?> { ["bodies"] = selected.Count, ["edges"] = edgeRecords.Count, ["faces"] = faceRecords.Count },
        };
        return OnshapeJsonHelper.SuccessEnvelope(result);
    }

    private static List<(string? Id, JsonElement Element)> ReadBodies(JsonElement root) =>
        ReadArray(root, "bodies").Select(body => (StringValue(body, "id") ?? StringValue(body, "topologyId"), body)).ToList();

    private static Dictionary<string, object?> BodySummary((string? Id, JsonElement Element) body) => new()
    {
        ["topologyId"] = body.Id,
        ["bodyName"] = StringValue(ObjectValue(body.Element, "properties"), "name") ?? StringValue(body.Element, "name"),
        ["bodyType"] = StringValue(body.Element, "type") ?? StringValue(body.Element, "bodyType"),
        ["edgeCount"] = ReadArray(body.Element, "edges").Count,
        ["faceCount"] = ReadArray(body.Element, "faces").Count,
    };

    private static Dictionary<string, object?> EdgeRecord((string? Id, JsonElement Element) body, JsonElement edge)
    {
        var curve = ObjectValue(edge, "curve");
        var geometry = ObjectValue(edge, "geometry");
        var rawType = StringValue(curve, "type") ?? StringValue(geometry, "type") ?? StringValue(edge, "curveType");
        return new()
        {
            ["topologyId"] = StringValue(edge, "id") ?? StringValue(edge, "topologyId"), ["bodyId"] = body.Id,
            ["bodyType"] = StringValue(body.Element, "type") ?? StringValue(body.Element, "bodyType"),
            ["curveType"] = NormalizeType(rawType), ["rawCurveType"] = rawType,
            ["lengthCm"] = Number(geometry, "length", 100) ?? Number(edge, "length", 100),
            ["startCm"] = Vector(geometry, "startPoint", 100) ?? Vector(edge, "startPoint", 100),
            ["endCm"] = Vector(geometry, "endPoint", 100) ?? Vector(edge, "endPoint", 100),
            ["midCm"] = Vector(geometry, "midPoint", 100) ?? Vector(geometry, "midpoint", 100) ?? Vector(edge, "midPoint", 100) ?? Vector(edge, "midpoint", 100),
            ["quarterCm"] = Vector(geometry, "quarterPoint", 100) ?? Vector(edge, "quarterPoint", 100),
            ["centerCm"] = Vector(curve, "origin", 100) ?? Vector(curve, "center", 100) ?? Vector(geometry, "center", 100),
            ["radiusCm"] = Number(curve, "radius", 100) ?? Number(geometry, "radius", 100),
            ["normal"] = Vector(curve, "normal", 1) ?? Vector(geometry, "normal", 1),
            ["provenance"] = new Dictionary<string, object?> { ["bodyTopologyId"] = body.Id },
        };
    }

    private static Dictionary<string, object?> FaceRecord((string? Id, JsonElement Element) body, JsonElement face)
    {
        var surface = ObjectValue(face, "surface");
        var geometry = ObjectValue(face, "geometry");
        var rawType = StringValue(surface, "type") ?? StringValue(geometry, "type") ?? StringValue(face, "surfaceType");
        return new()
        {
            ["topologyId"] = StringValue(face, "id") ?? StringValue(face, "topologyId"), ["bodyId"] = body.Id,
            ["bodyType"] = StringValue(body.Element, "type") ?? StringValue(body.Element, "bodyType"),
            ["surfaceType"] = NormalizeType(rawType), ["rawSurfaceType"] = rawType, ["areaCm2"] = Number(face, "area", 10000),
            ["originCm"] = Vector(surface, "origin", 100) ?? Vector(geometry, "origin", 100),
            ["normal"] = Vector(surface, "normal", 1) ?? Vector(geometry, "normal", 1),
            ["axis"] = Vector(surface, "axis", 1) ?? Vector(geometry, "axis", 1),
            ["radiusCm"] = Number(surface, "radius", 100) ?? Number(geometry, "radius", 100),
            ["boundsCm"] = Bounds(face), ["loops"] = Loops(face),
            ["provenance"] = new Dictionary<string, object?> { ["bodyTopologyId"] = body.Id },
        };
    }

    private static string? NormalizeType(string? raw) => raw?.ToLowerInvariant() switch
    {
        "circle" or "line" or "arc" or "plane" or "cylinder" or "cone" or "sphere" or "torus" => raw.ToLowerInvariant(),
        null => null, _ => "unknown",
    };

    private static List<JsonElement> ReadArray(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [];

    private static JsonElement? ObjectValue(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string? StringValue(JsonElement? element, string name) =>
        element is JsonElement value && value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static double? Number(JsonElement? element, string name, double multiplier) =>
        element is JsonElement value && value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.TryGetDouble(out var number) ? number * multiplier : null;

    private static List<double>? Vector(JsonElement? element, string name, double multiplier)
    {
        if (element is not JsonElement objectElement || objectElement.ValueKind != JsonValueKind.Object || !objectElement.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Array)
        {
            var values = value.EnumerateArray().Where(v => v.TryGetDouble(out _)).Select(v => v.GetDouble() * multiplier).ToList();
            return values.Count == 3 ? values : null;
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("x", out var x) && value.TryGetProperty("y", out var y) && value.TryGetProperty("z", out var z) && x.TryGetDouble(out var xv) && y.TryGetDouble(out var yv) && z.TryGetDouble(out var zv)) return [xv * multiplier, yv * multiplier, zv * multiplier];
        return null;
    }

    private static Dictionary<string, object?>? Bounds(JsonElement face)
    {
        var box = ObjectValue(face, "box");
        return box is not JsonElement value ? null : new Dictionary<string, object?> { ["minCm"] = Vector(value, "minCorner", 100) ?? Vector(value, "min", 100), ["maxCm"] = Vector(value, "maxCorner", 100) ?? Vector(value, "max", 100) };
    }

    private static string? MicroversionId(JsonElement root) =>
        StringValue(ObjectValue(root, "microversionId"), "theId") ?? StringValue(root, "microversionId") ?? StringValue(root, "microversion");

    private static List<Dictionary<string, object?>> Loops(JsonElement face) => ReadArray(face, "loops").Select(loop => new Dictionary<string, object?>
    {
        ["edgeIds"] = ReadArray(loop, "coedges").Select(coedge => StringValue(coedge, "edgeId") ?? StringValue(ObjectValue(coedge, "edge"), "id") ?? StringValue(coedge, "edge")).ToList(),
        ["orientations"] = ReadArray(loop, "coedges").Select(Orientation).ToList(),
    }).ToList();

    private static bool? Orientation(JsonElement coedge) =>
        coedge.TryGetProperty("orientation", out var orientation) && orientation.ValueKind is JsonValueKind.True or JsonValueKind.False ? orientation.GetBoolean() : null;

    private static void ThrowIfFeatureFailed(JsonElement root)
    {
        if (!root.TryGetProperty("featureState", out var state) || !state.TryGetProperty("featureStatus", out var statusElement)) return;
        var status = statusElement.GetString();
        if (status is not ("ERROR" or "UNKNOWN")) return;
        var detail = state.TryGetProperty("errorString", out var error) ? error.GetString() : null;
        throw new CadProviderOperationException($"Onshape sketch creation returned feature status {status}: {detail ?? "No error detail provided."}");
    }

    private static JsonElement? FindElement(JsonElement root, string elementId)
    {
        JsonElement elements;
        if (root.ValueKind == JsonValueKind.Array) elements = root;
        else if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("elements", out elements) || elements.ValueKind != JsonValueKind.Array) return null;

        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("id", out var id) && string.Equals(id.GetString(), elementId, StringComparison.Ordinal)) return element;
        }

        return null;
    }

    private (string Did, string Wvm, string WvmId) ResolveWvm(CadElementAddress address) =>
        (address.DocumentId, _driver.DefaultWvm, address.WorkspaceOrVersionId);
}
