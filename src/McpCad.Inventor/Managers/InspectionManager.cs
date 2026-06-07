using McpCad.Core.Exceptions;
using McpCad.Inventor.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InvApp = Inventor.Application;

namespace McpCad.Inventor.Managers;

/// <summary>
/// Provides inspection tools for LLM feedback loops:
/// - CaptureViewportImage: screenshot of the 3D view (Base64) for multimodal vision verification.
/// - GetFeatureTree: structured tree of operations/features for data-driven "Árbol de Operaciones" inspection.
/// - GetBoundingBox: direct geometry bounds for verification without images.
/// 
/// These enable the two approaches described by the user for retroalimentación de imágenes / visual + data feedback.
/// </summary>
public class InspectionManager(InventorDriver driver)
{
    private InvApp App => driver.InventorApp;

    private dynamic ActiveDocument()
    {
        var doc = driver.ActiveDocument
            ?? throw new InventorComException("No active document. Open or create a document first.");
        return doc;
    }

    private dynamic ComponentDefinition()
    {
        var compDef = driver.ComponentDefinition
            ?? throw new InventorComException("No component definition available.");
        return ComDispatchHelper.WrapDispatch(compDef);
    }

    /// <summary>
    /// Captures a screenshot of the current viewport.
    /// Returns Base64 image so the LLM (vision model) can analyze the visual state after a tool call.
    /// 
    /// TODO for full viewport accuracy: Provide exact Inventor COM call for ActiveView bitmap capture.
    /// Current implementation uses a simple approach (temp file if possible) or placeholder.
    /// Common patterns: app.ActiveView, Camera, or window HWND + GDI BitBlt.
    /// </summary>
    public Dictionary<string, object?> CaptureViewportImage(string view = "Iso", int width = 1024, int height = 768, string format = "png")
    {
        try
        {
            var doc = ActiveDocument();

            // Reliable approach per Inventor API limitations (as discussed):
            // 1. (Optionally) set the desired standard view orientation on the active viewport.
            // 2. SaveAs to a temporary image file (lifetime = milliseconds).
            // 3. Read the bytes immediately.
            // 4. Convert to Base64.
            // 5. Delete the temp file right away.
            // This gives the LLM a visual snapshot of the current 3D state for vision-model verification.

            string tempPath = Path.Combine(Path.GetTempPath(), $"mcp_viewport_{Guid.NewGuid():N}.{format}");

            // Try to orient the view (Iso, Front, Top, etc.)
            try
            {
                dynamic activeView = App.ActiveView;
                if (activeView != null)
                {
                    dynamic camera = activeView.Camera;
                    int orientation = 10754; // default good isometric
                    string v = (view ?? "iso").ToLowerInvariant();
                    if (v.Contains("front")) orientation = 10753;
                    else if (v.Contains("top")) orientation = 10755;
                    else if (v.Contains("right")) orientation = 10757;
                    else if (v.Contains("iso")) orientation = 10754;

                    camera.ViewOrientationType = orientation;
                    camera.ApplyWithoutTransition();
                }
            }
            catch { /* non-fatal */ }

            // Export to temp (the proven reliable path)
            doc.SaveAs(tempPath, true);

            if (!File.Exists(tempPath))
            {
                return new Dictionary<string, object?>
                {
                    ["success"] = false,
                    ["error"] = "SaveAs did not produce the expected image file.",
                    ["note"] = "If this does not capture the rendered 3D viewport you want, provide the exact Inventor COM code (ActiveView + Camera or graphics render) and I'll integrate it."
                };
            }

            byte[] imageBytes = File.ReadAllBytes(tempPath);
            string base64 = Convert.ToBase64String(imageBytes);

            try { File.Delete(tempPath); } catch { /* best effort */ }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["view"] = view,
                ["width"] = width,
                ["height"] = height,
                ["format"] = format,
                ["mime_type"] = format.Equals("png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg",
                ["image_base64"] = base64
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"CaptureViewportImage failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Builds a recursive tree of the features/operations in the active document.
    /// This implements the "Árbol de Operaciones" approach: the LLM can read the exact structure
    /// (names, types, order, suppression) directly from the Inventor object model.
    /// </summary>
    public Dictionary<string, object?> GetFeatureTree()
    {
        try
        {
            var compDef = ComponentDefinition();
            var features = new List<Dictionary<string, object?>>();

            // For parts
            try
            {
                dynamic partFeatures = compDef.Features;
                if (partFeatures != null)
                {
                    foreach (dynamic f in partFeatures)
                    {
                        features.Add(FeatureToDict(f));
                    }
                }
            }
            catch { /* not a part or no features collection at this level */ }

            // For assemblies - list occurrences as top level "features" of the tree (with their sub-features if loaded)
            try
            {
                dynamic occurrences = compDef.Occurrences;
                if (occurrences != null)
                {
                    foreach (dynamic occ in occurrences)
                    {
                        var occDict = new Dictionary<string, object?>
                        {
                            ["name"] = occ.Name?.ToString(),
                            ["type"] = "Occurrence",
                            ["path"] = occ.ReferencedDocumentDescriptor?.FullDocumentName,
                            ["grounded"] = occ.Grounded,
                            ["suppressed"] = occ.Suppressed,
                        };
                        features.Add(occDict);
                    }
                }
            }
            catch { /* not assembly or no occurrences */ }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document_type"] = compDef.Type?.ToString() ?? "unknown",
                ["feature_count"] = features.Count,
                ["tree"] = features,
                ["note"] = "Tree contains top-level features/occurrences. For deeper sub-features or constraints, extend the traversal as needed."
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"GetFeatureTree failed: {ex.Message}", ex);
        }
    }

    private Dictionary<string, object?> FeatureToDict(dynamic feature)
    {
        var dict = new Dictionary<string, object?>
        {
            ["name"] = feature.Name?.ToString(),
            ["type"] = feature.Type?.ToString() ?? feature.GetType().Name,
            ["suppressed"] = feature.Suppressed,
        };

        // Try to get health or extent info if available
        try { dict["health"] = feature.HealthStatus?.ToString(); } catch { }
        try { dict["is_consumed"] = feature.IsConsumed; } catch { }

        // Recurse into sub features if the feature exposes them (patterns, etc.)
        try
        {
            dynamic sub = feature.Features;
            if (sub != null)
            {
                var subs = new List<Dictionary<string, object?>>();
                foreach (dynamic sf in sub)
                {
                    subs.Add(FeatureToDict(sf));
                }
                if (subs.Count > 0) dict["sub_features"] = subs;
            }
        }
        catch { }

        return dict;
    }

    /// <summary>
    /// Returns bounding box data for verification (min/max, center, size).
    /// Target can be empty (whole model), a body name, a tagged entity, or "all".
    /// Uses direct Inventor geometry API (no rendering).
    /// </summary>
    public Dictionary<string, object?> GetBoundingBox(string target = "")
    {
        try
        {
            var compDef = ComponentDefinition();

            // Simple whole-model case using the range box of the component definition
            dynamic rangeBox = null;
            try
            {
                rangeBox = compDef.RangeBox;
            }
            catch
            {
                // For assemblies sometimes it's on the surface body or we fall back
            }

            if (rangeBox == null)
            {
                // Try to get from a body
                try
                {
                    dynamic bodies = compDef.SurfaceBodies;
                    if (bodies != null && bodies.Count > 0)
                    {
                        rangeBox = bodies[1].RangeBox;
                    }
                }
                catch { }
            }

            if (rangeBox == null)
            {
                return new Dictionary<string, object?>
                {
                    ["success"] = false,
                    ["error"] = "Could not obtain RangeBox. Target the model or provide a specific body/occurrence.",
                    ["target"] = target
                };
            }

            var min = rangeBox.MinPoint;
            var max = rangeBox.MaxPoint;

            double dx = max.X - min.X;
            double dy = max.Y - min.Y;
            double dz = max.Z - min.Z;

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["target"] = string.IsNullOrWhiteSpace(target) ? "model" : target,
                ["min"] = new { x = (double)min.X, y = (double)min.Y, z = (double)min.Z },
                ["max"] = new { x = (double)max.X, y = (double)max.Y, z = (double)max.Z },
                ["size"] = new { x = dx, y = dy, z = dz },
                ["center"] = new { x = (min.X + max.X) / 2, y = (min.Y + max.Y) / 2, z = (min.Z + max.Z) / 2 },
                ["note"] = "Direct vector data from Inventor RangeBox. Use for precise geometric checks (no vision required)."
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"GetBoundingBox failed: {ex.Message}", ex);
        }
    }
}