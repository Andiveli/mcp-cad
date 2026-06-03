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

            // Access RevolveFeatures via dynamic with Dispatch wrapper
            dynamic revolveFeatures = ComDispatchHelper.WrapDispatch(compDef.Features.RevolveFeatures);

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
    /// Sweep a profile along a path of connected sketch entities.
    /// Path is specified as comma-separated entity indices. By default, path
    /// entities come from the current sketch; use pathSketch to reference a
    /// different sketch (1-based index or "last").
    /// </summary>
    public Dictionary<string, object?> Sweep(
        string profile, string path,
        string sweepType = "path", string operation = "new_body",
        double taper = 0, string pathSketch = "", string profileSketch = "")
    {
        try
        {
            var compDef = ComponentDefinition();
            var sketch = GetActiveSketch(compDef);

            // Resolve profile sketch
            dynamic profileSktch;
            if (string.IsNullOrEmpty(profileSketch))
            {
                profileSktch = sketch;
            }
            else if (profileSketch.Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                var sketches = compDef.Sketches;
                if (sketches.Count < 2)
                    throw new InventorComException("Need at least 2 sketches for profile_sketch='last'.");
                profileSktch = sketches.Item(sketches.Count - 1); // previous
            }
            else if (int.TryParse(profileSketch, out int psIdx) && psIdx >= 1)
            {
                profileSktch = compDef.Sketches.Item(psIdx);
            }
            else
            {
                throw new InventorComException($"Invalid profile_sketch '{profileSketch}'.");
            }

            // Resolve profile — use simple resolution without intersection merging
            dynamic profileObj;
            try { profileObj = ProfileResolver.Resolve(profileSktch); }
            catch (Exception ex) { throw new InventorComException($"Sweep: Profile resolve failed - {ex.Message}", ex); }

            // Resolve path sketch (dynamic)
            dynamic pathSketchObj;
            if (string.IsNullOrEmpty(pathSketch))
            {
                pathSketchObj = sketch;
            }
            else if (pathSketch.Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                var sketches = compDef.Sketches;
                if (sketches.Count < 2)
                    throw new InventorComException("Need at least 2 sketches for path_sketch='last'.");
                pathSketchObj = sketches.Item(sketches.Count - 1);
            }
            else if (int.TryParse(pathSketch, out int skIdx) && skIdx >= 1)
            {
                pathSketchObj = compDef.Sketches.Item(skIdx);
            }
            else
            {
                throw new InventorComException($"Invalid path_sketch '{pathSketch}'. Use a 1-based index or 'last'.");
            }

            // Get the first path entity via dynamic dispatch
            var pathIndices = path.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!int.TryParse(pathIndices[0], out int firstIdx))
                throw new InventorComException($"Invalid path entity index: '{pathIndices[0]}'.");

            dynamic sketchEntity;
            try { sketchEntity = pathSketchObj.SketchLines.Item(firstIdx); }
            catch { sketchEntity = pathSketchObj.SketchEntities.Item(firstIdx); }

            // Diagnostic: verify what we got
            int sketchCount;
            int entityCount;
            try { sketchCount = compDef.Sketches.Count; } catch { sketchCount = -1; }
            try { entityCount = pathSketchObj.SketchEntities.Count; } catch { entityCount = -1; }

            // Create the Path object — need to access Features from raw COM,
            // not from the wrapped compDef which may not expose CreatePath.
            dynamic sweepPath;
            try
            {
                dynamic rawDoc = _driver.InventorApp.ActiveDocument;
                dynamic rawCompDef = rawDoc.ComponentDefinition;
                sweepPath = rawCompDef.Features.CreatePath(sketchEntity);
            }
            catch (Exception ex)
            {
                throw new InventorComException(
                    $"Sweep: CreatePath failed (sketches={sketchCount}, entities={entityCount}, " +
                    $"path_sketch='{pathSketch}', pathIdx={firstIdx}) - {ex.Message}", ex);
            }

            // Map operation
            var opMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["new_body"] = 20485,   // kNewBodyOperation
                ["join"] = 20481,       // kJoinOperation
                ["cut"] = 20482,        // kCutOperation
                ["intersect"] = 20483,  // kIntersectOperation
                ["surface"] = 20484,    // kSurfaceOperation
            };

            if (!opMap.TryGetValue(operation, out int opCode))
                throw new InventorComException($"Unknown operation '{operation}'. Valid: new_body, join, cut, intersect, surface.");

            // Map sweep type
            var swMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = 104449,                // kPathSweepType
                ["path_guide_rail"] = 104450,     // kPathAndGuidRailSweepType
                ["path_guide_surface"] = 104451,  // kPathAndGuidSurfaceSweepType
                ["path_section_twist"] = 104452,  // kPathAndSectionTwistSweepType
            };

            if (!swMap.TryGetValue(sweepType, out int swCode))
                throw new InventorComException($"Unknown sweep type '{sweepType}'. Valid: path, path_guide_rail, path_guide_surface, path_section_twist.");

            // Create sweep definition and add the feature
            dynamic sweepFeatures;
            try { sweepFeatures = compDef.Features.SweepFeatures; }
            catch (Exception ex) { throw new InventorComException($"Sweep: SweepFeatures - {ex.Message}", ex); }

            dynamic sweepDef;
            try { sweepDef = sweepFeatures.CreateSweepDefinition(swCode, profileObj, sweepPath, opCode); }
            catch (Exception ex) { throw new InventorComException($"Sweep: CreateSweepDefinition - {ex.Message}", ex); }

            // Apply taper if specified
            if (Math.Abs(taper) > 1e-9)
                sweepDef.TaperAngle = $"{taper} deg";

            dynamic sweepFeature;
            try { sweepFeature = sweepFeatures.Add(sweepDef); }
            catch (Exception ex) { throw new InventorComException($"Sweep: Add - {ex.Message}", ex); }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "sweep",
                ["feature_name"] = sweepFeature.Name,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to sweep: {ex.Message}", ex); }
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

            // Access CircularPatternFeatures via COM with Dispatch wrapper
            dynamic cpFeatures = ComDispatchHelper.WrapDispatch(compDef.Features.CircularPatternFeatures);

            // Add(ParentFeatures, Axis, NaturalDir, Count, Angle, FitWithinAngle, ComputeType)
            // Use Add directly (no separate CreateDefinition needed)
            dynamic cpFeature = cpFeatures.Add(
                objectCollection,
                patternAxis,
                naturalDirection,
                count,
                angleRad,
                fitWithinAngle,
                Type.Missing);  // ComputeType: default

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

            // Access HoleFeatures via dynamic with Dispatch wrapper
            dynamic holeFeatures = ComDispatchHelper.WrapDispatch(compDef.Features.HoleFeatures);

            // Create a point at (x, y) on the sketch plane for hole placement
            dynamic sketches = compDef.Sketches;
            dynamic sketch = sketches.Item(sketches.Count > 0 ? sketches.Count : 1);
            dynamic sketchPoints = sketch.SketchPoints;
            dynamic placementPoint = sketchPoints.Add(
                App.TransientGeometry.CreatePoint2d(x, y));

            // Inventor API: CreateSketchPlacementDefinition takes ObjectCollection of sketch points
            dynamic pointCollection = TransientObjects().CreateObjectCollection();
            pointCollection.Add(placementPoint);
            dynamic placementDef = holeFeatures.CreateSketchPlacementDefinition(pointCollection);
            dynamic holeFeature = holeFeatures.AddDrilledByDistanceExtent(
                placementDef, diameter, depth,
                (global::Inventor.PartFeatureExtentDirectionEnum)DirectionMap["positive"],
                false, Type.Missing);

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

            // Access SurfaceBodies and ThreadFeatures with Dispatch wrapper
            dynamic surfaceBody = ComDispatchHelper.WrapDispatch(compDef.SurfaceBodies.Item(1));
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

            // Access ThreadFeatures via dynamic with Dispatch wrapper
            dynamic threadFeatures = ComDispatchHelper.WrapDispatch(compDef.Features.ThreadFeatures);

            // Map direction to right/left handed
            bool rightHanded = !direction.Equals("left", StringComparison.OrdinalIgnoreCase);

            // CreateStandardThreadInfo returns StandardThreadInfo (required for cylindrical faces)
            dynamic threadInfo = threadFeatures.CreateStandardThreadInfo(
                false,           // Internal (false = external thread)
                rightHanded,
                "ANSI Metric M Profile",
                specification,   // e.g. "M10"
                "6g");           // tolerance class

            // Get first edge of the face (required by Add)
            dynamic startEdge = ComDispatchHelper.WrapDispatch(resolvedFace.Edges.Item(1));

            // Add(Face, StartEdge, ThreadInfo, DirectionReversed, FullDepth, ThreadDepth, ThreadOffset)
            dynamic threadFeature = threadFeatures.Add(
                resolvedFace, startEdge, threadInfo,
                false, true, Type.Missing, Type.Missing);

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
            dynamic surfaceBody = ComDispatchHelper.WrapDispatch(compDef.SurfaceBodies.Item(1));
            dynamic edges = surfaceBody.Edges;

            var edgeList = new List<Dictionary<string, object?>>();
            int count = edges.Count;

            for (int i = 1; i <= Math.Min(count, 200); i++) // Limit to 200 edges
            {
                dynamic edge = ComDispatchHelper.WrapDispatch(edges.Item(i));
                var edgeInfo = new Dictionary<string, object?>
                {
                    ["index"] = i,
                };

                // Edge properties may fail if edge doesn't expose IDispatch
                try { edgeInfo["edge_type"] = edge.EdgeType?.ToString(); } catch { }
                try { edgeInfo["length"] = edge.Length; } catch { }

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
    ///   - Single numeric index (1-based): returns that profile from sketch.Profiles
    ///   - Comma-separated indices (e.g. "2,4"): creates a compound profile with only
    ///     those regions using AddForSolid(false) + AddRegion(centroid)
    ///   - Non-numeric: delegates to ProfileResolver for auto-detection
    /// Fails with a clear error + profile count when the index is out of range.
    /// </summary>
    private dynamic ResolveProfile(string profileRef, dynamic sketch, dynamic compDef)
    {
        // Merge intersection points so crossing curves create sub-regions
        try
        {
            IntersectionMerger.MergeAll(sketch, App);
        }
        catch { /* Best-effort */ }

        // Check for comma-separated multi-index: "2,4" or "1,3,5"
        if (profileRef.Contains(','))
        {
            var parts = profileRef.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var indices = new HashSet<int>();
            foreach (var p in parts)
            {
                if (!int.TryParse(p, out int idx))
                    throw new InventorComException($"Invalid profile reference '{p}' in multi-index '{profileRef}'. Use numeric indices (e.g. '2,4').");
                indices.Add(idx);
            }

            dynamic profiles = sketch.Profiles;

            // Clean up any existing profiles before recomputing
            while (profiles.Count > 0)
            {
                try { profiles.Item(profiles.Count).Delete(); } catch { break; }
            }

            // Create a fresh Profile to discover paths
            dynamic multiProfile = profiles.AddForSolid(false);

            // Count paths by iterating
            int pathCount = 0;
            try
            {
                foreach (dynamic _ in multiProfile)
                    pathCount++;
            }
            catch { }

            if (pathCount == 0)
                throw new InventorComException("No closed profiles found in sketch. Draw closed regions first.");

            // Validate all indices before building
            foreach (int idx in indices)
            {
                if (idx < 1 || idx > pathCount)
                    throw new InventorComException(
                        $"Profile index {idx} is out of range. " +
                        $"The sketch has {pathCount} profile(s) (valid indices: 1-{pathCount}). " +
                        $"Use sketch_profiles() to inspect available profiles.");
            }

            // Keep only the desired paths, delete the rest
            int pi = 1;
            var pathsToDelete = new List<object>();
            foreach (dynamic path in multiProfile)
            {
                if (!indices.Contains(pi))
                    pathsToDelete.Add(path);
                pi++;
            }
            foreach (dynamic path in pathsToDelete)
                path.Delete();

            return multiProfile;
        }

        // Try single numeric index
        if (int.TryParse(profileRef, out int profileIdx))
        {
            dynamic profiles = sketch.Profiles;

            // If profiles exist but we need a specific index > 1,
            // we need to work with paths, not the Profiles collection.
            // First, count paths.
            while (profiles.Count > 0)
            {
                try { profiles.Item(profiles.Count).Delete(); } catch { break; }
            }

            dynamic fullProfile = profiles.AddForSolid(false);

            int pathCount = 0;
            try
            {
                foreach (dynamic _ in fullProfile)
                    pathCount++;
            }
            catch { }

            // Index 1 with only one profile: return as-is (convenience, full profile)
            if (profileIdx == 1 && pathCount >= 1)
                return fullProfile;

            // Validate range
            if (profileIdx < 1 || profileIdx > pathCount)
            {
                if (pathCount > 0)
                    throw new InventorComException(
                        $"Profile index {profileIdx} is out of range. " +
                        $"The sketch has {pathCount} profile(s) (valid indices: 1-{pathCount}). " +
                        $"Use sketch_profiles() to inspect available profiles before extruding.");

                // No profiles at all — fall through to ProfileResolver
                return ProfileResolver.Resolve(sketch);
            }

            // idx > 1 or explicit single-path request: delete other paths
            int pi = 1;
            var pathsToDelete = new List<object>();
            foreach (dynamic path in fullProfile)
            {
                if (pi != profileIdx)
                    pathsToDelete.Add(path);
                pi++;
            }
            foreach (dynamic path in pathsToDelete)
                path.Delete();

            return fullProfile;
        }

        // Non-numeric reference or zero profiles: delegate to ProfileResolver
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
                    dynamic features = ComDispatchHelper.WrapDispatch(compDef.Features.Item(typeName));
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
            dynamic extrudeFeatures = ComDispatchHelper.WrapDispatch(compDef.Features.ExtrudeFeatures);
            for (int i = 1; i <= extrudeFeatures.Count; i++)
            {
                if (extrudeFeatures.Item(i).Name == featureRef)
                    return extrudeFeatures.Item(i);
            }

            dynamic revolveFeatures = ComDispatchHelper.WrapDispatch(compDef.Features.RevolveFeatures);
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
            dynamic surfaceBody = ComDispatchHelper.WrapDispatch(compDef.SurfaceBodies.Item(1));
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
                    dynamic surfaceBody = ComDispatchHelper.WrapDispatch(compDef.SurfaceBodies.Item(1));
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