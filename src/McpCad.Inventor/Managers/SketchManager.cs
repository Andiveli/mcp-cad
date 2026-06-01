using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Inventor.Helpers;
using InvApp = Inventor.Application;

namespace McpCad.Inventor.Managers;

/// <summary>
/// Manages 2D sketch operations: create, draw, dimension, pattern, transform.
/// Tracks an active sketch so draw commands target the right object.
/// </summary>
public class SketchManager
{
    private readonly InventorDriver _driver;
    private dynamic? _activeSketch;
    private int _activeSketchIndex;
    private object? _lastEndpoint;
    private object? _firstStartpoint;

    // Work-plane indices in Inventor (1-based COM collection)
    private static readonly Dictionary<string, int> PlaneMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["XY"] = 1,
        ["XZ"] = 2,
        ["YZ"] = 3,
    };

    // Spline fit method enum values
    private static readonly Dictionary<string, int> SplineFitMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smooth"] = 26369,
        ["sweet"] = 26370,
        ["autocad"] = 26371,
    };

    // Dimension orientation enum values
    private static readonly Dictionary<string, int> DimensionOrientations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aligned"] = 19203,
        ["horizontal"] = 19201,
        ["vertical"] = 19202,
    };

    public SketchManager(InventorDriver driver) => _driver = driver;

    // ── Internal guards ───────────────────────────────────────────────

    private InvApp App => _driver.InventorApp;

    private dynamic EnsureActiveSketch()
    {
        if (_activeSketch is null)
            throw new InventorComException("No active sketch. Call SketchCreate() first.");
        return _activeSketch;
    }

    private dynamic TransientGeometry() => App.TransientGeometry;

    private dynamic ActiveDocument()
    {
        var doc = _driver.ActiveDocument
            ?? throw new InventorComException("No active document. Open or create a document first.");
        return doc;
    }

    private dynamic ComponentDefinition() => _driver.ComponentDefinition
        ?? throw new InventorComException("No component definition available.");

    private dynamic BuildEntityCollection(dynamic sketch, string entities)
    {
        var col = App.TransientObjects.CreateObjectCollection();
        foreach (var idxStr in entities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ent = sketch.SketchEntities.Item(int.Parse(idxStr));
            col.Add(ent);
        }
        return col;
    }

    // ── Public API ────────────────────────────────────────────────────

    public Dictionary<string, object?> SketchCreate(string plane = "XY")
    {
        if (!PlaneMap.TryGetValue(plane, out int planeIndex))
            return ErrorResult.Create($"Invalid plane '{plane}'. Must be one of: XY, XZ, YZ");

        try
        {
            var compDef = ComponentDefinition();
            dynamic workPlane = compDef.WorkPlanes.Item(planeIndex);
            dynamic sketch = compDef.Sketches.Add(workPlane);
            _activeSketch = sketch;
            _activeSketchIndex = compDef.Sketches.Count;
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["sketch_name"] = sketch.Name as string,
                ["plane"] = plane.ToUpperInvariant(),
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create sketch on plane '{plane}'", ex); }
    }

    public Dictionary<string, object?> SketchLine(
        double x1, double y1, double x2, double y2,
        string? tag = null, bool connect = false)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            object start, end;

            if (connect && _lastEndpoint is not null)
            {
                start = _lastEndpoint;
                // Auto-close: if end matches first start point, share the SketchPoint
                if (_firstStartpoint is not null)
                {
                    double fpX = ((dynamic)_firstStartpoint).Geometry.X;
                    double fpY = ((dynamic)_firstStartpoint).Geometry.Y;
                    if (Math.Abs(x2 - fpX) < 0.001 && Math.Abs(y2 - fpY) < 0.001)
                    {
                        end = _firstStartpoint;
                        _lastEndpoint = null;
                        _firstStartpoint = null;
                    }
                    else
                    {
                        end = tg.CreatePoint2d(x2, y2);
                    }
                }
                else
                {
                    end = tg.CreatePoint2d(x2, y2);
                }
            }
            else
            {
                start = tg.CreatePoint2d(x1, y1);
                end = tg.CreatePoint2d(x2, y2);
            }

            dynamic line = sketch.SketchLines.AddByTwoPoints(start, end);

            if (_lastEndpoint is null)
                _firstStartpoint = line.StartSketchPoint;
            if (connect)
                _lastEndpoint = line.EndSketchPoint;
            else
            {
                _lastEndpoint = line.EndSketchPoint;
                _firstStartpoint = line.StartSketchPoint;
            }

            if (!string.IsNullOrEmpty(tag))
            {
                int entityIdx = sketch.SketchEntities.Count;
                TagStore.SetTag(_activeSketchIndex, entityIdx, tag);
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "line",
                ["start"] = new[] { x1, y1 },
                ["end"] = new[] { x2, y2 },
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw line: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchLineClose()
    {
        var sketch = EnsureActiveSketch();
        try
        {
            if (_lastEndpoint is null || _firstStartpoint is null)
                return ErrorResult.Create("No open profile to close");

            dynamic line = sketch.SketchLines.AddByTwoPoints(_lastEndpoint, _firstStartpoint);
            _lastEndpoint = null;
            _firstStartpoint = null;
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "line",
                ["operation"] = "close_profile",
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to close profile: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            dynamic center = tg.CreatePoint2d(cx, cy);
            sketch.SketchCircles.AddByCenterRadius(center, radius);
            if (!string.IsNullOrEmpty(tag))
            {
                int entityIdx = sketch.SketchEntities.Count;
                TagStore.SetTag(_activeSketchIndex, entityIdx, tag);
            }
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "circle",
                ["center"] = new[] { cx, cy },
                ["radius"] = radius,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw circle: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchArc(double cx, double cy, double radius, double startAngle, double endAngle)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            dynamic center = tg.CreatePoint2d(cx, cy);
            sketch.SketchArcs.AddByCenterStartEndAngle(center, radius, startAngle, endAngle);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "arc",
                ["center"] = new[] { cx, cy },
                ["radius"] = radius,
                ["start_angle"] = startAngle,
                ["end_angle"] = endAngle,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw arc: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            dynamic corner1 = tg.CreatePoint2d(x1, y1);
            dynamic corner2 = tg.CreatePoint2d(x2, y2);
            sketch.SketchLines.AddAsTwoPointRectangle(corner1, corner2);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "rectangle",
                ["corner1"] = new[] { x1, y1 },
                ["corner2"] = new[] { x2, y2 },
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw rectangle: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchPoint(double x, double y)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            dynamic pt = tg.CreatePoint2d(x, y);
            sketch.SketchPoints.Add(pt);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "point",
                ["x"] = x,
                ["y"] = y,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw point: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet")
    {
        if (!SplineFitMethods.TryGetValue(fitMethod, out int methodEnum))
            return ErrorResult.Create($"Unknown fit method '{fitMethod}'. Use: smooth, sweet, autocad");

        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            dynamic col = App.TransientObjects.CreateObjectCollection();

            var parts = points.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 4 || parts.Length % 2 != 0)
                return ErrorResult.Create("points must be comma-separated x,y pairs with at least 3 points");

            for (int i = 0; i < parts.Length; i += 2)
            {
                double px = double.Parse(parts[i]);
                double py = double.Parse(parts[i + 1]);
                col.Add(tg.CreatePoint2d(px, py));
            }

            sketch.SketchSplines.Add(col, methodEnum);
            int pointCount = parts.Length / 2;
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "spline",
                ["points"] = pointCount,
                ["fit_method"] = fitMethod,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw spline: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchEllipse(
        double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            dynamic center = tg.CreatePoint2d(cx, cy);
            double rad = majorAxisAngle * Math.PI / 180.0;
            dynamic axisVec = tg.CreateUnitVector2d(Math.Cos(rad), Math.Sin(rad));
            sketch.SketchEllipses.Add(center, axisVec, majorRadius, minorRadius);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "ellipse",
                ["cx"] = cx,
                ["cy"] = cy,
                ["major_radius"] = majorRadius,
                ["minor_radius"] = minorRadius,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to draw ellipse: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchCircularPattern(
        string entities, string axis, int count,
        double angle = 360.0, bool fitted = true, bool symmetric = false)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var col = BuildEntityCollection(sketch, entities);

            // Resolve center point from axis (1-based index into SketchPoints)
            int axisIndex = int.Parse(axis.Trim());
            dynamic centerPt = sketch.SketchPoints.Item(axisIndex);

            double angleRad = angle * Math.PI / 180.0;

            var cp = sketch.CircularPatterns;
            // CreateDefinition(Geometries, AxisEntity, NaturalAxisDirection, Count, Angle, Symmetric, Associative, Fitted)
            object? natDir = true; // default natural direction
            dynamic definition = cp.CreateDefinition(col, centerPt, natDir, count, angleRad, symmetric, Type.Missing, fitted);
            cp.Add(definition);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["pattern_type"] = "sketch_circular",
                ["count"] = count,
                ["angle"] = angle,
                ["fitted"] = fitted,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create sketch circular pattern: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchRectangularPattern(
        string entities, string xAxis, int xCount, double xSpacing,
        string yAxis = "", int yCount = 1, double ySpacing = 0.0)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var col = BuildEntityCollection(sketch, entities);
            dynamic xDir = sketch.SketchEntities.Item(int.Parse(xAxis.Trim()));

            var rp = sketch.RectangularPatterns;
            dynamic definition;

            if (!string.IsNullOrEmpty(yAxis))
            {
                dynamic yDir = sketch.SketchEntities.Item(int.Parse(yAxis.Trim()));
                definition = rp.CreateDefinition(
                    col, xDir, xCount,
                    Type.Missing, Type.Missing, xSpacing,
                    yDir, yCount, Type.Missing, Type.Missing, ySpacing);
            }
            else
            {
                definition = rp.CreateDefinition(
                    col, xDir, xCount,
                    Type.Missing, Type.Missing, xSpacing);
            }

            rp.Add(definition);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["pattern_type"] = "sketch_rectangular",
                ["x_count"] = xCount,
                ["y_count"] = yCount,
                ["x_spacing"] = xSpacing,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to create sketch rectangular pattern: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchOffset(
        string entities, double offsetX, double offsetY, bool includeConnected = false)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var col = BuildEntityCollection(sketch, entities);
            var tg = TransientGeometry();
            dynamic offsetPt = tg.CreatePoint2d(offsetX, offsetY);

            sketch.OffsetSketchEntitiesUsingPoint(col, offsetPt, includeConnected, true);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "offset",
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to offset sketch: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var col = BuildEntityCollection(sketch, entities);
            var tg = TransientGeometry();
            dynamic vec = tg.CreateVector2d(dx, dy);
            sketch.MoveSketchObjects(col, vec, copy, false);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "move",
                ["dx"] = dx,
                ["dy"] = dy,
                ["copy"] = copy,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to move sketch: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchRotate(
        string entities, double cx, double cy, double angle, bool copy = false)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var col = BuildEntityCollection(sketch, entities);
            var tg = TransientGeometry();
            dynamic center = tg.CreatePoint2d(cx, cy);
            double angleRad = angle * Math.PI / 180.0;
            sketch.RotateSketchObjects(col, center, angleRad, copy, false);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "rotate",
                ["angle"] = angle,
                ["cx"] = cx,
                ["cy"] = cy,
                ["copy"] = copy,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to rotate sketch: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchDelete()
    {
        var sketch = EnsureActiveSketch();
        try
        {
            sketch.Delete();
            _activeSketch = null;
            _activeSketchIndex = 0;
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "delete_sketch",
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to delete sketch: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchConstraint(
        string mode, string entity1, string entity2 = "",
        string symLine = "", string axis = "major")
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var gc = sketch.GeometricConstraints;
            bool useMajor = !axis.Equals("minor", StringComparison.OrdinalIgnoreCase);

            // Typed collection resolution
            var lineModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "parallel", "perpendicular", "collinear", "horizontal", "vertical", "equal" };
            var circleModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "concentric", "tangent" };
            var pointModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "coincident", "midpoint" };

            dynamic e1, e2 = Type.Missing;

            if (lineModes.Contains(mode))
            {
                e1 = sketch.SketchLines.Item(int.Parse(entity1.Trim()));
                if (!string.IsNullOrEmpty(entity2))
                    e2 = sketch.SketchLines.Item(int.Parse(entity2.Trim()));
            }
            else if (circleModes.Contains(mode))
            {
                e1 = sketch.SketchCircles.Item(int.Parse(entity1.Trim()));
                if (!string.IsNullOrEmpty(entity2))
                    e2 = sketch.SketchCircles.Item(int.Parse(entity2.Trim()));
            }
            else if (pointModes.Contains(mode))
            {
                e1 = sketch.SketchPoints.Item(int.Parse(entity1.Trim()));
                if (!string.IsNullOrEmpty(entity2))
                    e2 = sketch.SketchPoints.Item(int.Parse(entity2.Trim()));
            }
            else
            {
                e1 = sketch.SketchEntities.Item(int.Parse(entity1.Trim()));
                if (!string.IsNullOrEmpty(entity2))
                    e2 = sketch.SketchEntities.Item(int.Parse(entity2.Trim()));
            }

            switch (mode.ToLowerInvariant())
            {
                case "coincident":
                    gc.AddCoincident(e1, e2);
                    break;
                case "collinear":
                    gc.AddCollinear(e1, e2, useMajor, useMajor);
                    break;
                case "concentric":
                    gc.AddConcentric(e1, e2);
                    break;
                case "parallel":
                    gc.AddParallel(e1, e2, useMajor, useMajor);
                    break;
                case "perpendicular":
                    gc.AddPerpendicular(e1, e2, useMajor, useMajor);
                    break;
                case "tangent":
                    gc.AddTangent(e1, e2);
                    break;
                case "equal":
                    gc.AddEqualLength(e1, e2);
                    break;
                case "midpoint":
                    gc.AddMidpoint(e1, e2);
                    break;
                case "symmetric":
                    if (string.IsNullOrEmpty(symLine))
                        return ErrorResult.Create("Symmetric constraint needs sym_line parameter");
                    dynamic symEntity = sketch.SketchEntities.Item(int.Parse(symLine.Trim()));
                    gc.AddSymmetry(e1, e2, symEntity);
                    break;
                case "smooth":
                    gc.AddSmooth(e1, e2);
                    break;
                case "horizontal":
                    gc.AddHorizontal(e1, useMajor);
                    break;
                case "vertical":
                    gc.AddVertical(e1, useMajor);
                    break;
                default:
                    return ErrorResult.Create($"Unknown constraint mode '{mode}'");
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["constraint"] = mode.ToLowerInvariant(),
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to add {mode} constraint: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchDimension(
        string mode, string entity1, string entity2 = "",
        double? value = null, string orientation = "aligned",
        double? positionX = null, double? positionY = null)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var dc = sketch.DimensionConstraints;
            var tg = TransientGeometry();

            // Resolve entity from typed collection
            dynamic e1;
            if (mode.Equals("radius", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("diameter", StringComparison.OrdinalIgnoreCase))
            {
                e1 = sketch.SketchCircles.Item(int.Parse(entity1.Trim()));
            }
            else if (mode.Equals("angle", StringComparison.OrdinalIgnoreCase))
            {
                e1 = sketch.SketchLines.Item(int.Parse(entity1.Trim()));
            }
            else
            {
                e1 = sketch.SketchEntities.Item(int.Parse(entity1.Trim()));
            }

            // Determine text position
            dynamic textPt;
            if (positionX.HasValue && positionY.HasValue)
            {
                textPt = tg.CreatePoint2d(positionX.Value, positionY.Value);
            }
            else
            {
                // Default: offset from entity geometry
                double mx = 5.0, my = 5.0;
                try
                {
                    dynamic g = e1.Geometry;
                    try
                    {
                        // Line-style geometry
                        mx = (g.StartPoint.X + g.EndPoint.X) / 2.0 + 2.0;
                        my = (g.StartPoint.Y + g.EndPoint.Y) / 2.0 + 2.0;
                    }
                    catch
                    {
                        // Circle-style geometry
                        mx = g.Center.X + 2.0;
                        my = g.Center.Y + 2.0;
                    }
                }
                catch { /* fallback defaults */ }
                textPt = tg.CreatePoint2d(mx, my);
            }

            dynamic dim;
            if (mode.Equals("linear", StringComparison.OrdinalIgnoreCase))
            {
                var pt1 = sketch.SketchPoints.Item(int.Parse(entity1.Trim()));
                var pt2 = !string.IsNullOrEmpty(entity2)
                    ? sketch.SketchPoints.Item(int.Parse(entity2.Trim()))
                    : sketch.SketchPoints.Item(int.Parse(entity1.Trim()) + 1);

                int orient = DimensionOrientations.GetValueOrDefault(orientation, 19203); // aligned default
                dim = dc.AddTwoPointDistance(pt1, pt2, orient, textPt);
            }
            else if (mode.Equals("radius", StringComparison.OrdinalIgnoreCase))
            {
                dim = dc.AddRadius(e1, textPt, false);
            }
            else if (mode.Equals("diameter", StringComparison.OrdinalIgnoreCase))
            {
                dim = dc.AddDiameter(e1, textPt, false);
            }
            else if (mode.Equals("angle", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(entity2))
                    return ErrorResult.Create("Angle dimension mode needs entity2 (second line)");
                dynamic e2 = sketch.SketchLines.Item(int.Parse(entity2.Trim()));
                dim = dc.AddTwoLineAngle(e1, e2, textPt);
            }
            else
            {
                return ErrorResult.Create($"Unknown dimension mode '{mode}'");
            }

            if (value.HasValue)
                dim.Parameter.Value = value.Value;

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "dimension",
                ["mode"] = mode,
                ["value"] = value,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to add dimension: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "end")
    {
        var sketch = EnsureActiveSketch();
        try
        {
            dynamic ent = sketch.SketchLines.Item(int.Parse(entity.Trim()));
            dynamic cut = sketch.SketchLines.Item(int.Parse(cuttingEntity.Trim()));

            dynamic geo1 = ent.Geometry;
            dynamic geo2 = cut.Geometry;
            dynamic pts = geo1.Intersect(geo2);

            if (pts is null || pts.Count == 0)
                return ErrorResult.Create("Entities do not intersect");

            dynamic pt = pts!.Item(1);
            var tg = TransientGeometry();
            dynamic target = tg.CreatePoint2d(pt.X, pt.Y);

            if (side.Equals("start", StringComparison.OrdinalIgnoreCase))
                ent.StartSketchPoint.MoveTo(target);
            else
                ent.EndSketchPoint.MoveTo(target);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "trim",
                ["side"] = side,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to trim: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            var tg = TransientGeometry();
            foreach (var idxStr in entities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                dynamic ent = sketch.SketchLines.Item(int.Parse(idxStr));
                dynamic start = ent.StartSketchPoint;
                dynamic end = ent.EndSketchPoint;

                double sgX = start.Geometry.X, sgY = start.Geometry.Y;
                double egX = end.Geometry.X, egY = end.Geometry.Y;

                double nx1 = cx + (sgX - cx) * factor;
                double ny1 = cy + (sgY - cy) * factor;
                double nx2 = cx + (egX - cx) * factor;
                double ny2 = cy + (egY - cy) * factor;

                start.MoveTo(tg.CreatePoint2d(nx1, ny1));
                end.MoveTo(tg.CreatePoint2d(nx2, ny2));
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "scale",
                ["factor"] = factor,
                ["cx"] = cx,
                ["cy"] = cy,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to scale: {ex.Message}", ex); }
    }

    public Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity)
    {
        var sketch = EnsureActiveSketch();
        try
        {
            dynamic mirrorLine = sketch.SketchLines.Item(int.Parse(mirrorEntity.Trim()));
            dynamic mg = mirrorLine.Geometry;

            // Mirror axis line endpoints
            double ax1 = mg.StartPoint.X, ay1 = mg.StartPoint.Y;
            double ax2 = mg.EndPoint.X, ay2 = mg.EndPoint.Y;
            double vx = ax2 - ax1, vy = ay2 - ay1;
            double vlen2 = vx * vx + vy * vy;

            if (vlen2 < 1e-12)
                return ErrorResult.Create("Mirror line has zero length");

            var tg = TransientGeometry();

            foreach (var idxStr in entities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                dynamic ent = sketch.SketchLines.Item(int.Parse(idxStr));
                dynamic start = ent.StartSketchPoint;
                dynamic end = ent.EndSketchPoint;

                double sgX = start.Geometry.X, sgY = start.Geometry.Y;
                double egX = end.Geometry.X, egY = end.Geometry.Y;

                // Reflect start point
                double dx = sgX - ax1, dy = sgY - ay1;
                double t = (dx * vx + dy * vy) / vlen2;
                double rcx = ax1 + t * vx, rcy = ay1 + t * vy;
                double rx1 = 2.0 * rcx - sgX, ry1 = 2.0 * rcy - sgY;

                // Reflect end point
                dx = egX - ax1; dy = egY - ay1;
                t = (dx * vx + dy * vy) / vlen2;
                rcx = ax1 + t * vx; rcy = ay1 + t * vy;
                double rx2 = 2.0 * rcx - egX, ry2 = 2.0 * rcy - egY;

                start.MoveTo(tg.CreatePoint2d(rx1, ry1));
                end.MoveTo(tg.CreatePoint2d(rx2, ry2));
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["operation"] = "mirror",
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to mirror: {ex.Message}", ex); }
    }
}