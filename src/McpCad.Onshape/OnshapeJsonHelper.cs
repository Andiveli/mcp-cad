using System.Text.Json;

namespace McpCad.Onshape;

internal static class OnshapeJsonHelper
{
    public static Dictionary<string, object?> JsonElementToDictionary(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().ToDictionary(property => property.Name, property => JsonElementToObject(property.Value))
            : new Dictionary<string, object?> { ["value"] = JsonElementToObject(element) };

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var number) => number,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.Object => JsonElementToDictionary(element),
        _ => element.GetRawText(),
    };

    public static Dictionary<string, object?> SuccessEnvelope(Dictionary<string, object?> payload)
    {
        var result = new Dictionary<string, object?>(payload)
        {
            ["success"] = true,
        };
        return result;
    }
}
