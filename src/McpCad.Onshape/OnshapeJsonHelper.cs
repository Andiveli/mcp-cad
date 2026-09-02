using System.Text.Json;

namespace McpCad.Onshape;

internal static class OnshapeJsonHelper
{
    public static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            _ => new Dictionary<string, object?> { ["value"] = JsonElementToObject(element) },
        };
    }

    public static object? JsonElementToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                {
                    return l;
                }

                return element.GetDouble();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(JsonElementToObject).ToList();
            case JsonValueKind.Object:
                return element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value));
            default:
                return element.GetRawText();
        }
    }

    public static Dictionary<string, object?> NormalizeFeatureWriteResponse(JsonElement element)
    {
        var result = JsonElementToDictionary(element);
        if (!result.ContainsKey("featureId") &&
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("feature", out var feature) &&
            feature.ValueKind == JsonValueKind.Object &&
            feature.TryGetProperty("featureId", out var featureId) &&
            featureId.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(featureId.GetString()))
        {
            result["featureId"] = featureId.GetString();
        }

        return result;
    }

    public static Dictionary<string, object?> SuccessEnvelope(Dictionary<string, object?> payload)
    {
        var result = new Dictionary<string, object?> { ["success"] = true };
        foreach (var pair in payload)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }
}