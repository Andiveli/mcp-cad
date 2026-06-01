using McpCad.Core.Exceptions;
using McpCad.Inventor.Helpers;

namespace McpCad.Inventor.Helpers;

/// <summary>
/// Resolves an axis reference for revolve operations.
/// Supports numeric indices (1-based SketchLine index) and @tag references 
/// (e.g., "@eje" resolved through TagStore).
/// </summary>
public static class AxisResolver
{
    /// <summary>
    /// Resolve an axis reference to a SketchLine in the given sketch.
    /// </summary>
    /// <param name="sketch">The active planar sketch.</param>
    /// <param name="axisRef">Axis reference: "@tag" for TagStore lookup, or numeric string for SketchLines index.</param>
    /// <param name="sketchIndex">The 1-based index of the sketch in the component definition (used for tag resolution).</param>
    /// <returns>The resolved SketchLine COM object.</returns>
    /// <exception cref="InventorComException">Thrown when the axis reference cannot be resolved.</exception>
    public static dynamic Resolve(dynamic sketch, string axisRef, int sketchIndex)
    {
        if (string.IsNullOrWhiteSpace(axisRef))
            throw new InventorComException("Axis reference cannot be empty. Provide a SketchLine index or @tag.");

        // @tag resolution: look up in TagStore
        if (axisRef.StartsWith("@", StringComparison.OrdinalIgnoreCase))
        {
            string tag = axisRef[1..]; // strip @
            int? entityIdx = TagStore.Resolve(sketchIndex, tag);
            if (entityIdx is null)
                throw new InventorComException($"Tag '@{tag}' not found in sketch {sketchIndex}. Draw a line with tag='{tag}' first.");

            // For tagged lines, we know they're SketchLines
            try
            {
                return sketch.SketchLines.Item(entityIdx.Value);
            }
            catch (Exception ex)
            {
                throw new InventorComException($"Tag '@{tag}' resolved to entity index {entityIdx.Value} but that SketchLine does not exist.", ex);
            }
        }

        // Numeric index: direct 1-based lookup into SketchLines
        if (int.TryParse(axisRef, out int lineIdx))
        {
            try
            {
                return sketch.SketchLines.Item(lineIdx);
            }
            catch (Exception ex)
            {
                throw new InventorComException($"SketchLine index {lineIdx} does not exist in the active sketch.", ex);
            }
        }

        throw new InventorComException($"Invalid axis reference '{axisRef}'. Use a numeric index or @tag reference.");
    }
}