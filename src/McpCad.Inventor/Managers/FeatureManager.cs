using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Inventor.Helpers;
using InvApp = Inventor.Application;

namespace McpCad.Inventor.Managers;

/// <summary>
/// Manages 3D feature operations: extrude, revolve, fillet, chamfer, circular pattern.
/// Uses InventorDriver for COM connection and delegates profile/axis/edge resolution 
/// to dedicated helpers.
/// </summary>
public class FeatureManager
{
    private readonly InventorDriver _driver;

    // PartFeatureOperationEnum values (from Inventor interop documentation)
    private static readonly Dictionary<string, int> OperationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["new_body"] = 20485,  // kNewBodyOperation
        ["join"] = 20481,       // kJoinOperation
        ["cut"] = 20482,        // kCutOperation
        ["intersect"] = 20483,  // kIntersectOperation
        ["surface"] = 20484,    // kSurfaceOperation
    };

    // PartFeatureExtentDirectionEnum values (verified from interop)
    private static readonly Dictionary<string, int> DirectionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["positive"] = 20993,   // kPositiveExtentDirection
        ["negative"] = 20994,   // kNegativeExtentDirection
        ["symmetric"] = 20995,  // kSymmetricExtentDirection
    };

    public FeatureManager(InventorDriver driver) => _driver = driver;

    // ── Internal guards ───────────────────────────────────────────────

    private InvApp App => _driver.InventorApp;

    private dynamic ActiveDocument()
    {
        var doc = _driver.ActiveDocument
            ?? throw new InventorComException("No active document. Open or create a document first.");
        return doc;
    }

    private dynamic ComponentDefinition()
    {
        var compDef = _driver.ComponentDefinition
            ?? throw new InventorComException("No component definition available.");
        // Wrap in Dispatch to ensure IDispatch support for chained dynamic access
        return ComDispatchHelper.WrapDispatch(compDef);
    }

    private dynamic TransientObjects() => App.TransientObjects;

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Extrude a sketch profile to create a 3D feature.
    /// </summary>
    public Dictionary<string, object?> Extrude(
        string profile, double distance,
        string direction = "positive",
        double taper = 0.0,
        string operation = "new_body")
    {
        try
        {
            var compDef = ComponentDefinition();
            dynamic sketch = GetActiveSketch(compDef);

            // Resolve profile
            dynamic resolvedProfile = ResolveProfile(profile, sketch, compDef);

            // Map operation enum
            if (!OperationMap.TryGetValue(operation, out int opEnum))
                return ErrorResult.Create($"Invalid operation '{operation}'. Use: new_body, join, cut, intersect.");

            if (!DirectionMap.TryGetValue(direction, out int dirEnum))
                return ErrorResult.Create($"Invalid direction '{direction}'. Use: positive, negative, symmetric.");

            // Use early-bound interop types instead of dynamic for COM calls.
            // Cast to concrete types from Autodesk.Inventor.Interop assembly.
            // Use global:: prefix because McpCad.Inventor shadows the Inventor namespace.
            var partCompDef = (global::Inventor.PartComponentDefinition)compDef;
            var extrudeFeatures = partCompDef.Features.ExtrudeFeatures;
            var extrudeDef = extrudeFeatures.CreateExtrudeDefinition(resolvedProfile, (global::Inventor.PartFeatureOperationEnum)opEnum);

            // Set distance extent
            // PartFeatureExtentDirectionEnum value for direction
            extrudeDef.SetDistanceExtent(distance, dirEnum);

            // Apply taper angle if specified
            if (taper != 0.0)
            {
                // TaperAngle expects string with unit suffix for COM
                extrudeDef.TaperAngle = taper.ToString("G") + " deg";
            }

            // Add the feature
            dynamic extrudeFeature = extrudeFeatures.Add(extrudeDef);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "extrude",
                ["distance"] = distance,
                ["direction"] = direction,
                ["taper"] = taper != 0.0 ? taper : null,
                ["operation"] = operation,
                ["feature_name"] = extrudeFeature.Name as string,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to extrude: {ex.Message}", ex); }
    }

    /// <summary>
    /// Revolve a profile around an axis.
    /// </summary>
    public Dictionary<string, object?> Revolve(
        string profile, string axis, double angle = 360.0,
        string direction = "positive",
        string operation = "join")
    {
        try
        {
            var compDef = ComponentDefinition();
            var doc = ActiveDocument();

            // Get the active sketch for axis resolution
            var sketches = compDef.Sketches;
            if (sketches.Count == 0)
                return ErrorResult.Create("No sketches found. Create a sketch with an axis line first.");

            dynamic sketch = sketches.Item(sketches.Count);
            int sketchIndex = compDef.Sketches.Count;

            // Resolve profile
            dynamic resolvedProfile = ResolveProfile(profile, sketch, compDef);

            // Resolve axis — supports @tag and numeric index
            dynamic axisLine = AxisResolver.Resolve(sketch, axis, sketchIndex);

            // Map operation enum
            if (!OperationMap.TryGetValue(operation, out int opEnum))
                return ErrorResult.Create($"Invalid operation '{operation}'. Use: join, cut, intersect, new_body.");

            // Map direction enum for revolve (revolve uses the same direction enums)
            if (!DirectionMap.TryGetValue(direction, out int dirEnum))
                dirEnum = DirectionMap["positive"];

            // Access RevolveFeatures via dynamic (late binding for COM)
            dynamic revolveFeatures = compDef.Features.RevolveFeatures;

            // 360° uses AddFull, other angles use AddByAngle
            dynamic revolveFeature;
            if (Math.Abs(angle - 360.0) < 0.001)
            {
                // Full revolve
                revolveFeature = revolveFeatures.AddFull(resolvedProfile, axisLine, opEnum);
            }
            else
            {
                // Partial revolve
                double angleRad = angle * Math.PI / 180.0;
                revolveFeature = revolveFeatures.AddByAngle(
                    resolvedProfile, axisLine, angleRad, dirEnum, opEnum);
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "revolve",
                ["angle"] = angle,
                ["direction"] = direction,
                ["operation"] = operation,
                ["feature_name"] = revolveFeature.Name as string,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to revolve: {ex.Message}", ex); }
    }

    /// <summary>
    /// Apply a fillet (radius rounding) to the specified edges.
    /// </summary>
    public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant")
    {
        try
        {
            var compDef = ComponentDefinition();
            var partCompDef = (global::Inventor.PartComponentDefinition)compDef;

            // Resolve edge indices to EdgeCollection
            dynamic edgeCollection = EdgeResolver.Resolve(partCompDef, edges);

            // Get FilletFeatures via early-bound COM
            var filletFeatures = partCompDef.Features.FilletFeatures;

            // AddSimple(EdgeCollection, Radius, AutoEdgeChain, TangentProp, PreserveFeat, Optimized, RollAlongEdge, RollWherePossible)
            var filletFeature = filletFeatures.AddSimple(
                edgeCollection, radius,
                true, true, true, true, true, true);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "fillet",
                ["radius"] = radius,
                ["mode"] = "constant",
                ["edges"] = edges,
                ["feature_name"] = filletFeature.Name,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create fillet: {ex.Message}", ex); }
    }

    /// <summary>
    /// Apply a chamfer (beveled edge) to the specified edges.
    /// </summary>
    public Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance")
    {
        try
        {
            var compDef = ComponentDefinition();
            var partCompDef = (global::Inventor.PartComponentDefinition)compDef;

            // Resolve edge indices to EdgeCollection
            dynamic edgeCollection = EdgeResolver.Resolve(partCompDef, edges);

            // Get ChamferFeatures via early-bound COM
            var chamferFeatures = partCompDef.Features.ChamferFeatures;

            // AddUsingDistance(EdgeCollection, Distance, AutoEdgeChain, PreserveFeat, Optimized)
            var chamferFeature = chamferFeatures.AddUsingDistance(
                edgeCollection, distance,
                true, true, true);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "chamfer",
                ["distance"] = distance,
                ["mode"] = mode,
                ["edges"] = edges,
                ["feature_name"] = chamferFeature.Name,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create chamfer: {ex.Message}", ex); }
    }

    /// <summary>
    /// Create a circular pattern of a feature around an axis.
    /// </summary>
    public Dictionary<string, object?> CircularPattern(
        string profile, string axis, int count,
        double angle = 360.0, bool fitWithinAngle = true,
        bool naturalDirection = true)
    {
        try
        {
            var compDef = ComponentDefinition();

            // Resolve feature by name or index
            dynamic feature = ResolveFeature(compDef, profile);

            // Build ObjectCollection with just the feature
            dynamic objectCollection = TransientObjects().CreateObjectCollection();
            objectCollection.Add(feature);

            // Resolve axis — supports work axis name or edge index
            dynamic patternAxis = ResolvePatternAxis(compDef, axis);

            // Calculate sweep angle
            double angleRad = angle * Math.PI / 180.0;

            // Access CircularPatternFeatures via COM
            dynamic cpFeatures = compDef.Features.CircularPatternFeatures;

            // CreateDefinition features, axis, count, angle, fitWithinAngle, naturalDirection
            // Inventor API: CreateDefinition(ObjectCollection, AxisEntity, Count, Angle, NaturalAxisDirection, Associative, FittedRotation)
            dynamic cpDef = cpFeatures.CreateDefinition(
                objectCollection,
                patternAxis,
                count,
                angleRad,
                naturalDirection,
                Type.Missing,
                fitWithinAngle);

            dynamic cpFeature = cpFeatures.Add(cpDef);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "circular_pattern",
                ["count"] = count,
                ["angle"] = angle,
                ["fit_within_angle"] = fitWithinAngle,
                ["feature_name"] = cpFeature.Name as string,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create circular pattern: {ex.Message}", ex); }
    }

    // ── Hole Feature ──────────────────────────────────────────────────

    /// <summary>
    /// Create a hole feature at the specified position.
    /// Uses HoleFeatures via dynamic (late-bound) COM access since
    /// early-bound interop may not expose HoleFeatures.
    /// </summary>
    public Dictionary<string, object?> Hole(
        double x, double y, double diameter, double depth,
        string type = "drilled", string operation = "join")
    {
        try
        {
            var compDef = ComponentDefinition();
            var doc = ActiveDocument();

            // Map operation
            if (!OperationMap.TryGetValue(operation, out int opEnum))
                return ErrorResult.Create($"Invalid operation '{operation}'. Use: new_body, join, cut, intersect.");

            // Map hole type
            // kDrilledHole = 17921, kCounterBoredHole = 17922, kCounterSunkHole = 17923
            int holeTypeEnum = type.ToLowerInvariant() switch
            {
                "drilled" => 17921,
                "counterbore" => 17922,
                "countersink" => 17923,
                _ => 17921 // default to drilled
            };

            // Access HoleFeatures via dynamic (late binding)
            dynamic holeFeatures = compDef.Features.HoleFeatures;

            // Create a point at (x, y) on the sketch plane
            // We need a sketch point for the hole placement
            dynamic sketches = compDef.Sketches;
            dynamic sketch = sketches.Item(sketches.Count > 0 ? sketches.Count : 1);
            dynamic sketchPoints = sketch.SketchPoints;
            dynamic placementPoint = sketchPoints.Add(
                App.TransientGeometry.CreatePoint2d(x, y));

            // Create hole definition via dynamic
            dynamic holeDef = holeFeatures.CreateDefinition(
                placementPoint,
                holeTypeEnum,
                diameter,
                depth);

            dynamic holeFeature = holeFeatures.Add(holeDef);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "hole",
                ["x"] = x,
                ["y"] = y,
                ["diameter"] = diameter,
                ["depth"] = depth,
                ["hole_type"] = type,
                ["feature_name"] = holeFeature.Name as string,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create hole: {ex.Message}", ex); }
    }

    // ── Thread Feature ────────────────────────────────────────────────

    /// <summary>
    /// Create a thread feature on a cylindrical face.
    /// Uses ThreadFeatures via dynamic (late-bound) COM access since
    /// early-bound interop may not expose ThreadFeatures.
    /// </summary>
    public Dictionary<string, object?> Thread(string face, string specification, string direction = "right")
    {
        try
        {
            var compDef = ComponentDefinition();

            // Resolve the face — supports numeric index or name
            dynamic surfaceBody = compDef.SurfaceBodies.Item(1);
            dynamic faces = surfaceBody.Faces;

            // Try face as numeric index
            dynamic resolvedFace;
            if (int.TryParse(face, out int faceIdx))
            {
                resolvedFace = faces.Item(faceIdx);
            }
            else
            {
                // Search by face name/select
                resolvedFace = faces.Item(1); // fallback to first face
            }

            // Access ThreadFeatures via dynamic (late binding)
            dynamic threadFeatures = compDef.Features.ThreadFeatures;

            // Direction: kRightHandThread = 17930, kLeftHandThread = 17931
            int directionEnum = direction.ToLowerInvariant() switch
            {
                "right" => 17930,
                "left" => 17931,
                _ => 17930
            };

            dynamic threadDef = threadFeatures.CreateDefinition(
                resolvedFace,
                specification,
                directionEnum);

            dynamic threadFeature = threadFeatures.Add(threadDef);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "thread",
                ["face"] = face,
                ["specification"] = specification,
                ["direction"] = direction,
                ["feature_name"] = threadFeature.Name as string,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create thread: {ex.Message}", ex); }
    }

    // ── Edge Inspector ────────────────────────────────────────────────

    /// <summary>
    /// List all edges of the active body with geometry information.
    /// Useful for identifying edge indices for fillet, chamfer, etc.
    /// </summary>
    public Dictionary<string, object?> InspectEdges()
    {
        try
        {
            var compDef = ComponentDefinition();
            dynamic surfaceBody = compDef.SurfaceBodies.Item(1);
            dynamic edges = surfaceBody.Edges;

            var edgeList = new List<Dictionary<string, object?>>();
            int count = edges.Count;

            for (int i = 1; i <= Math.Min(count, 200); i++) // Limit to 200 edges
            {
                dynamic edge = edges.Item(i);
                var edgeInfo = new Dictionary<string, object?>
                {
                    ["index"] = i,
                    ["edge_type"] = edge.EdgeType?.ToString(),
                    ["length"] = edge.Length,
                };

                // Try to get geometry info
                try
                {
                    dynamic geometry = edge.Geometry;
                    if (geometry != null)
                    {
                        edgeInfo["geometry_type"] = geometry.GetType().Name;
                    }
                }
                catch { /* geometry may not be available for all edge types */ }

                // Check if edge is circular (cylinder/cone) or linear
                try
                {
                    dynamic startVertex = edge.StartVertex;
                    dynamic endVertex = edge.EndVertex;
                    if (startVertex != null && endVertex != null)
                    {
                        dynamic startPt = startVertex!.Point;
                        dynamic endPt = endVertex!.Point;
                        edgeInfo["start"] = $"({startPt.X:F3}, {startPt.Y:F3}, {startPt.Z:F3})";
                        edgeInfo["end"] = $"({endPt.X:F3}, {endPt.Y:F3}, {endPt.Z:F3})";
                    }
                }
                catch { /* vertices not available */ }

                edgeList.Add(edgeInfo);
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["total_edges"] = count,
                ["edges"] = edgeList,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to inspect edges: {ex.Message}", ex); }
    }

    // ── Private helpers ───────────────────────────────────────────────

    /// <summary>
    /// Get the last active sketch from the component definition.
    /// </summary>
    private dynamic GetActiveSketch(dynamic compDef)
    {
        var sketches = compDef.Sketches;
        if (sketches.Count == 0)
            throw new InventorComException("No sketch exists. Create a sketch first.");
        return sketches.Item(sketches.Count);
    }

    /// <summary>
    /// Resolve a profile reference for feature operations.
    /// Accepts:
    ///   - "1" → profile at that 1-based index in the sketch
    ///   - Anything else → try ProfileResolver with timeout
    /// </summary>
    private dynamic ResolveProfile(string profileRef, dynamic sketch, dynamic compDef)
    {
        // Try numeric index first
        if (int.TryParse(profileRef, out int profileIdx))
        {
            dynamic profiles = sketch.Profiles;
            if (profiles.Count > 0 && profileIdx >= 1 && profileIdx <= profiles.Count)
                return profiles.Item(profileIdx);

            // Fall through to ProfileResolver if index doesn't exist
        }

        // Use ProfileResolver with timeout-based AddForSolid
        return ProfileResolver.Resolve(sketch);
    }

    /// <summary>
    /// Resolve a feature by its name or 1-based index in the features collection.
    /// Accepts feature names (like "Extrusion 1") or numeric strings as indices.
    /// </summary>
    private dynamic ResolveFeature(dynamic compDef, string featureRef)
    {
        // Try as numeric index into the last feature type used
        // Since we can't predict which feature type, try extrude features first,
        // then revolve, then fillet, then chamfer — most common pattern
        if (int.TryParse(featureRef, out int featureIdx))
        {
            // Try each feature collection type
            string[] featureTypes = ["ExtrudeFeatures", "RevolveFeatures", "FilletFeatures", "ChamferFeatures"];
            foreach (var typeName in featureTypes)
            {
                try
                {
                    dynamic features = compDef.Features.Item(typeName);
                    if (featureIdx >= 1 && featureIdx <= features.Count)
                        return features.Item(featureIdx);
                }
                catch { /* try next type */ }
            }
        }

        // Try by name — iterate all features to find it
        try
        {
            // Common approach: search across all feature collections
            // ExtrudeFeatures is most common
            dynamic extrudeFeatures = compDef.Features.ExtrudeFeatures;
            for (int i = 1; i <= extrudeFeatures.Count; i++)
            {
                if (extrudeFeatures.Item(i).Name == featureRef)
                    return extrudeFeatures.Item(i);
            }

            dynamic revolveFeatures = compDef.Features.RevolveFeatures;
            for (int i = 1; i <= revolveFeatures.Count; i++)
            {
                if (revolveFeatures.Item(i).Name == featureRef)
                    return revolveFeatures.Item(i);
            }
        }
        catch { /* fall through to error */ }

        throw new InventorComException($"Feature '{featureRef}' not found. Provide a valid feature index or name.");
    }

    /// <summary>
    /// Resolve a pattern axis. Supports:
    ///   - Work axis name (e.g., "X Axis", "Y Axis", "Z Axis")
    ///   - Work axis index (1-based)
    ///   - Edge index with "e" prefix (e.g., "e1" for edge 1)
    /// </summary>
    private dynamic ResolvePatternAxis(dynamic compDef, string axisRef)
    {
        if (string.IsNullOrWhiteSpace(axisRef))
            throw new InventorComException("Axis reference cannot be empty.");

        // Try as work axis name
        if (axisRef.Equals("X Axis", StringComparison.OrdinalIgnoreCase) ||
            axisRef.Equals("x", StringComparison.OrdinalIgnoreCase))
        {
            return compDef.WorkAxes.Item(1); // X axis
        }
        if (axisRef.Equals("Y Axis", StringComparison.OrdinalIgnoreCase) ||
            axisRef.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            return compDef.WorkAxes.Item(2); // Y axis
        }
        if (axisRef.Equals("Z Axis", StringComparison.OrdinalIgnoreCase) ||
            axisRef.Equals("z", StringComparison.OrdinalIgnoreCase))
        {
            return compDef.WorkAxes.Item(3); // Z axis
        }

        // Try as edge reference with "e" prefix (e.g., "e1")
        if (axisRef.StartsWith("e", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(axisRef[1..], out int edgeIdx))
        {
            dynamic surfaceBody = compDef.SurfaceBodies.Item(1);
            return surfaceBody.Edges.Item(edgeIdx);
        }

        // Try as numeric work axis index
        if (int.TryParse(axisRef, out int axisIdx))
        {
            try
            {
                return compDef.WorkAxes.Item(axisIdx);
            }
            catch
            {
                // Not a valid work axis — try as edge
                try
                {
                    dynamic surfaceBody = compDef.SurfaceBodies.Item(1);
                    return surfaceBody.Edges.Item(axisIdx);
                }
                catch
                {
                    throw new InventorComException($"Axis reference '{axisRef}' could not be resolved as either a work axis or edge.");
                }
            }
        }

        throw new InventorComException($"Invalid axis reference '{axisRef}'. Use 'X Axis', 'Y Axis', 'Z Axis', a work axis index, or 'eN' for edge N.");
    }
}