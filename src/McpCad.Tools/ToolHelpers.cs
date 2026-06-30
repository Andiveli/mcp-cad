namespace McpCad.Tools;

/// <summary>
/// Shared helpers for building standardized result dictionaries
/// across all MCP tool classes.
/// </summary>
public static class ToolHelpers
{
    /// <summary>
    /// Build an error result dictionary with success=false.
    /// Used by the tool layer (D7) to catch COM exceptions and
    /// return structured error responses.
    /// </summary>
    public static Dictionary<string, object?> Error(string message) => new()
    {
        ["success"] = false,
        ["error"] = message,
    };

    /// <summary>
    /// Build a success result dictionary, optionally merging
    /// extra key-value pairs from the provided dictionary.
    /// </summary>
    public static Dictionary<string, object?> Ok(Dictionary<string, object?>? extra = null)
    {
        var result = new Dictionary<string, object?> { ["success"] = true };
        if (extra is not null)
            foreach (var kv in extra)
                result[kv.Key] = kv.Value;
        return result;
    }

    /// <summary>
    /// Build a minimal success result dictionary (success=true only).
    /// Symmetric to Error() for phase status where the phase itself
    /// carries no additional payload (e.g., verify phase success marker).
    /// </summary>
    public static Dictionary<string, object?> Success() => new() { ["success"] = true };

    /// <summary>
    /// Merge a source phase-status dictionary into the target envelope
    /// under the provided key. Used during macro envelope composition
    /// to attach per-phase results (sketch, feature, pattern, etc.).
    /// </summary>
    public static void Merge(Dictionary<string, object?> target, string key, Dictionary<string, object?> source)
    {
        if (target is null) return;
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required", nameof(key));
        target[key] = source;
    }
}