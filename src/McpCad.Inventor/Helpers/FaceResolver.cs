using McpCad.Core.Exceptions;

namespace McpCad.Inventor.Helpers;

/// <summary>
/// Resolves comma-separated face indices (1-based) to an Inventor FaceCollection.
/// Example: "1,3,5" → FaceCollection with SurfaceBody.Faces[1], [3], [5].
/// Returns wrapped object for compatibility with both late-bound and early-bound APIs.
/// </summary>
public static class FaceResolver
{
    /// <summary>
    /// Resolve a comma-separated string of face indices to a wrapped FaceCollection.
    /// Each face is individually wrapped via ComDispatchHelper to ensure proper
    /// COM identity for early-bound APIs (Shell, Draft, Thicken).
    /// </summary>
    public static dynamic ResolveFaces(dynamic compDef, string faceIndices)
    {
        if (string.IsNullOrWhiteSpace(faceIndices))
            throw new InventorComException("Face indices cannot be empty. Provide comma-separated 1-based indices (e.g. '1,3,5') or @name tags (e.g. '@leg1' or '1,@joint').");

        dynamic surfaceBody = compDef.SurfaceBodies.Item(1);
        dynamic faceCollection = compDef.Application.TransientObjects.CreateFaceCollection();

        var tokens = faceIndices.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var t = token.Trim();
            if (t.StartsWith("@", StringComparison.Ordinal))
            {
                // @tag support: lookup face by .Name (basic support for named faces; full TagStore for faces is future)
                string tagName = t.Substring(1).Trim();
                bool found = false;
                int faceCount = 0;
                try { faceCount = surfaceBody.Faces.Count; } catch { }
                for (int j = 1; j <= faceCount; j++)
                {
                    try
                    {
                        dynamic f = surfaceBody.Faces.Item(j);
                        string? fn = null;
                        try { fn = f.Name as string; } catch { }
                        if (fn != null && fn.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                        {
                            object rawFace = surfaceBody.Faces.Item(j);
                            dynamic face = ComDispatchHelper.WrapDispatch(rawFace);
                            faceCollection.Add(face);
                            found = true;
                            break;
                        }
                    }
                    catch { /* skip bad face */ }
                }
                if (!found)
                    throw new InventorComException($"Face tag '@{tagName}' not found by name on primary body (has {faceCount} faces). Use numeric indices or ensure face has matching .Name.");
            }
            else
            {
                if (!int.TryParse(t, out int idx))
                    throw new InventorComException($"Invalid face index '{t}'. Must be a number or @name tag.");

                try
                {
                    object rawFace = surfaceBody.Faces.Item(idx);
                    dynamic face = ComDispatchHelper.WrapDispatch(rawFace);
                    faceCollection.Add(face);
                }
                catch (Exception ex)
                {
                    throw new InventorComException($"Face index {idx} does not exist. The body has {surfaceBody.Faces.Count} faces.", ex);
                }
            }
        }

        return ComDispatchHelper.WrapDispatch(faceCollection);
    }
}
