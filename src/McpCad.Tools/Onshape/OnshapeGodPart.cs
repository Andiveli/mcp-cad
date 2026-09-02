using System.ComponentModel;
using System.Text.Json;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Builders;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpCad.Tools.Onshape;

public partial class OnshapeSkillTools
{
    [McpServerTool, Description("Create a rectangle or fixed-schema straight-line closed profile, extrude it, optionally apply bounded features, then capture final iso and top verification views. All dimensions are in cm.")]
    public CallToolResult skill_onshape_god_part(
        string address_json, double x1_cm, double y1_cm, double x2_cm, double y2_cm, double depth_cm,
        string? options_json = null, int capture_width = 800, int capture_height = 600, string capture_edges = "show")
    {
        try
        {
            ValidateCaptureOptions(capture_width, capture_height, capture_edges);
            var normalizedCaptureEdges = capture_edges.ToLowerInvariant();
            var modeling = BuildGodPart(address_json, x1_cm, y1_cm, x2_cm, y2_cm, depth_cm, options_json);
            if (!IsSuccessful(modeling)) return ToCallToolResult(ModelingFailureResult(modeling));

            CallToolResult? captureResult = null;
            AppendFinalCaptures(modeling, CadElementAddress.Deserialize(address_json), capture_width, capture_height, normalizedCaptureEdges, ref captureResult);
            return captureResult ?? ToCallToolResult(modeling);
        }
        catch (Exception ex)
        {
            return ToCallToolResult(ValidationResult(ex.Message));
        }
    }

    private Dictionary<string, object?> BuildGodPart(
        string address_json, double x1_cm, double y1_cm, double x2_cm, double y2_cm, double depth_cm,
        string? options_json = null)
    {
        var phases = NewPhases();
        string? latestFeatureId = null;
        var activePhase = "validation";
        try
        {
            var options = GodPartOptions.Parse(options_json);
            ValidateDepth(depth_cm);
                if (options.Profile is null) ValidateRectangleArguments(x1_cm, y1_cm, x2_cm, y2_cm);
            options.Validate();
                            var address = CadElementAddress.Deserialize(address_json);

            activePhase = "validation";
            using var sketchPayload = options.Profile is null
                    ? OnshapeSketchBuilder.BuildRectangle(
                OnshapeUnitConversion.CmToMeters(x1_cm), OnshapeUnitConversion.CmToMeters(y1_cm),
                OnshapeUnitConversion.CmToMeters(x2_cm), OnshapeUnitConversion.CmToMeters(y2_cm), options.Plane)
                    : OnshapeSketchBuilder.BuildClosedProfile(options.Profile, options.Plane);
            activePhase = "sketch";
                var sketchRequest = JsonSerializer.Deserialize<Dictionary<string, object?>>(sketchPayload.RootElement.GetRawText())
                ?? new Dictionary<string, object?>();
            var sketchResult = provider.SketchReplace(address, sketchRequest);
            if (!Complete("sketch", sketchResult, phases, ref latestFeatureId, out var failure))
                return GodPartResult(phases, failure, latestFeatureId);

            activePhase = "extrude";
            using var extrudePayload = OnshapeFeatureTemplates.ExtrudeFromSketch(
                latestFeatureId!, OnshapeUnitConversion.CmToMeters(depth_cm), options.Direction, options.BooleanOp);
            var extrudeResult = provider.FeatureAdd(address, "extrude", null, extrudePayload.RootElement.GetRawText());
            if (!Complete("extrude", extrudeResult, phases, ref latestFeatureId, out failure))
                return GodPartResult(phases, failure, latestFeatureId);

            activePhase = "fillet";
            if (options.Fillet is not null && !Add("fillet", OnshapeFeatureTemplates.Fillet(options.Fillet.EdgeIds, Cm(options.Fillet.ValueCm)), address, phases, ref latestFeatureId, out failure))
                return GodPartResult(phases, failure, latestFeatureId);
            activePhase = "chamfer";
            if (options.Chamfer is not null && !Add("chamfer", OnshapeFeatureTemplates.Chamfer(options.Chamfer.EdgeIds, Cm(options.Chamfer.ValueCm)), address, phases, ref latestFeatureId, out failure))
                return GodPartResult(phases, failure, latestFeatureId);
            activePhase = "shell";
            if (options.Shell is not null && !Add("shell", OnshapeFeatureTemplates.Shell(options.Shell.FaceIds, Cm(options.Shell.ThicknessCm), options.Shell.OppositeDirection), address, phases, ref latestFeatureId, out failure))
                return GodPartResult(phases, failure, latestFeatureId);
            activePhase = "circularPattern";
            if (options.CircularPattern is not null)
            {
                var option = options.CircularPattern;
                if (!Add("circularPattern", OnshapeFeatureTemplates.CircularPatternFromFeature(option.SourceFeatureId ?? latestFeatureId!, option.AxisFeatureId, option.InstanceCount, option.TotalAngleDeg, option.OppositeDirection, option.EqualSpace), address, phases, ref latestFeatureId, out failure))
                    return GodPartResult(phases, failure, latestFeatureId);
            }
            activePhase = "rectangularPattern";
            if (options.RectangularPattern is not null)
            {
                var option = options.RectangularPattern;
                if (!Add("rectangularPattern", OnshapeFeatureTemplates.RectangularPatternFromFeature(option.SourceFeatureId ?? latestFeatureId!, option.DirectionOneFeatureId, Cm(option.DistanceCm), option.InstanceCount, option.DirectionTwoFeatureId, option.DistanceTwoCm is null ? null : Cm(option.DistanceTwoCm.Value), option.InstanceCountTwo, option.OppositeDirection, option.IsCentered, option.OppositeDirectionTwo, option.IsCenteredTwo), address, phases, ref latestFeatureId, out failure))
                    return GodPartResult(phases, failure, latestFeatureId);
            }
            activePhase = "mirror";
            if (options.Mirror is not null && !Add("mirror", OnshapeFeatureTemplates.MirrorFeature(options.Mirror.SourceFeatureId ?? latestFeatureId!, options.Mirror.Plane), address, phases, ref latestFeatureId, out failure))
                return GodPartResult(phases, failure, latestFeatureId);

            return GodPartResult(phases, null, latestFeatureId);
        }
        catch (Exception ex)
        {
            if (activePhase == "validation") return ValidationResult(ex.Message);
            phases[activePhase] = new Dictionary<string, object?>
            {
                ["success"] = false, ["featureId"] = null, ["error"] = ex.Message,
            };
            return GodPartResult(phases, ex.Message, latestFeatureId);
        }
    }

    partial void AppendFinalCaptures(Dictionary<string, object?> result, CadElementAddress address, int width, int height, string edges, ref CallToolResult? captureResult);

    private static CallToolResult ToCallToolResult(Dictionary<string, object?> result, List<ContentBlock>? images = null)
    {
        var structuredContent = JsonSerializer.SerializeToElement(result);
        if (result.TryGetValue("success", out var success) && success is true)
            return new CallToolResult { Content = images ?? [], StructuredContent = structuredContent };

        var content = images ?? [];
        content.Add(new TextContentBlock { Text = JsonSerializer.Serialize(result) });
        return new CallToolResult { IsError = true, Content = content, StructuredContent = structuredContent };
    }

    private static void ValidateCaptureOptions(int width, int height, string edges)
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(width), "capture_width and capture_height must be between 1 and 4096.");
        if (!string.Equals(edges, "show", StringComparison.OrdinalIgnoreCase) && !string.Equals(edges, "hide", StringComparison.OrdinalIgnoreCase)) throw new ArgumentOutOfRangeException(nameof(edges), "capture_edges must be 'show' or 'hide'.");
    }

    private bool Add(string phase, JsonDocument payload, CadElementAddress address, Dictionary<string, object?> phases, ref string? latestFeatureId, out string? failure)
    {
        using (payload)
        {
            var result = provider.FeatureAdd(address, payload.RootElement.GetProperty("feature").GetProperty("featureType").GetString()!, null, payload.RootElement.GetRawText());
            return Complete(phase, result, phases, ref latestFeatureId, out failure);
        }
    }

    private static bool Complete(string phase, Dictionary<string, object?> result, Dictionary<string, object?> phases, ref string? latestFeatureId, out string? failure)
    {
        if (!IsSuccessful(result) || !TryGetFeatureId(result, out var featureId))
        {
            failure = !IsSuccessful(result) ? GetError(result, $"Onshape {phase} failed.") : $"Onshape {phase} did not return a usable feature ID.";
            phases[phase] = new Dictionary<string, object?> { ["success"] = false, ["featureId"] = null, ["result"] = result, ["error"] = failure };
            return false;
        }
        latestFeatureId = featureId;
        phases[phase] = new Dictionary<string, object?> { ["success"] = true, ["featureId"] = featureId, ["result"] = result };
        failure = null;
        return true;
    }

    private static Dictionary<string, object?> ValidationResult(string error) => new()
    {
        ["success"] = false, ["modelingSuccess"] = false, ["captureSuccess"] = false, ["failedPhase"] = "validation",
        ["finalFeatureId"] = null, ["phases"] = NewPhases(), ["captures"] = Array.Empty<Dictionary<string, object?>>(), ["error"] = error,
    };

    private static Dictionary<string, object?> ModelingFailureResult(Dictionary<string, object?> result)
    {
        result["modelingSuccess"] = false;
        result["captureSuccess"] = null;
        result["captures"] = Array.Empty<Dictionary<string, object?>>();
        return result;
    }

    private static Dictionary<string, object?> GodPartResult(Dictionary<string, object?> phases, string? failure, string? finalFeatureId)
    {
        var failedPhase = failure is null ? null : phases.First(pair => pair.Value is Dictionary<string, object?> phase && phase.TryGetValue("error", out _)).Key;
        var result = new Dictionary<string, object?> { ["success"] = failure is null, ["failedPhase"] = failedPhase, ["finalFeatureId"] = finalFeatureId, ["phases"] = phases };
        if (failure is not null) result["error"] = failure;
        return result;
    }

    private static Dictionary<string, object?> NewPhases() => new(StringComparer.Ordinal)
    {
        ["sketch"] = Skipped(), ["extrude"] = Skipped(), ["fillet"] = Skipped(), ["chamfer"] = Skipped(), ["shell"] = Skipped(),
        ["circularPattern"] = Skipped(), ["rectangularPattern"] = Skipped(), ["mirror"] = Skipped(),
    };
    private static Dictionary<string, object?> Skipped() => new() { ["success"] = false, ["featureId"] = null, ["skipped"] = true };
    private static double Cm(double value) => OnshapeUnitConversion.CmToMeters(value);
    private static void ValidateDepth(double depthCm)
        {
            if (!double.IsFinite(depthCm) || depthCm == 0) throw new ArgumentException("depth_cm must be finite and nonzero.");
        }
        private static void ValidateRectangleArguments(double x1Cm, double y1Cm, double x2Cm, double y2Cm)
        {
            if (!double.IsFinite(x1Cm) || !double.IsFinite(y1Cm) || !double.IsFinite(x2Cm) || !double.IsFinite(y2Cm)) throw new ArgumentException("Rectangle coordinates must be finite.");
            if (x1Cm == x2Cm || y1Cm == y2Cm) throw new ArgumentException("Rectangle coordinates must define nonzero width and height.");
        }
        private static void ValidateBaseArguments(params double[] values)
    {
        if (values.Any(value => !double.IsFinite(value)) || values[^1] == 0) throw new ArgumentException("Coordinates must be finite and depth_cm must be nonzero.");
        if (values[0] == values[2] || values[1] == values[3]) throw new ArgumentException("Rectangle coordinates must define nonzero width and height.");
    }

    private sealed record GodPartOptions(string? Plane, string Direction, string BooleanOp, IReadOnlyList<SketchPoint>? Profile, SelectionOption? Fillet, SelectionOption? Chamfer, ShellOption? Shell, CircularPatternOption? CircularPattern, RectangularPatternOption? RectangularPattern, MirrorOption? Mirror)
    {
        public static GodPartOptions Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new(null, "positive", "new", null, null, null, null, null, null, null);
            using var document = JsonDocument.Parse(json);
            var root = RequireObject(document.RootElement, "options_json");
            RejectUnknown(root, "plane", "direction", "booleanOp", "profile", "fillet", "chamfer", "shell", "circularPattern", "rectangularPattern", "mirror");
            return new(GetOptionalString(root, "plane"), GetOptionalString(root, "direction") ?? "positive", GetOptionalString(root, "booleanOp") ?? "new", ParseProfile(root),
                ParseSelection(root, "fillet", "radiusCm"), ParseSelection(root, "chamfer", "distanceCm"), ParseShell(root), ParseCircular(root), ParseRectangular(root), ParseMirror(root));
        }

        public void Validate()
        {
            if (Plane is not null && !CanonicalPlane(Plane)) throw new ArgumentException("plane must be Top, Front, or Right.");
            if (Direction is not ("positive" or "negative")) throw new ArgumentException("direction must be positive or negative.");
            if (BooleanOp is not ("new" or "add" or "join" or "cut" or "remove" or "intersect")) throw new ArgumentException("booleanOp is invalid.");
            Fillet?.Validate("radiusCm", "edgeIds"); Chamfer?.Validate("distanceCm", "edgeIds"); Shell?.Validate(); CircularPattern?.Validate(); RectangularPattern?.Validate(); Mirror?.Validate();
        }
    }

    private sealed record SelectionOption(IReadOnlyList<string> EdgeIds, double ValueCm)
    {
        public void Validate(string dimension, string topology) { if (EdgeIds.Count == 0 || EdgeIds.Any(string.IsNullOrWhiteSpace) || !double.IsFinite(ValueCm) || ValueCm <= 0) throw new ArgumentException($"{topology} and positive finite {dimension} are required."); }
    }
    private sealed record ShellOption(IReadOnlyList<string> FaceIds, double ThicknessCm, bool OppositeDirection)
    {
        public void Validate() { if (FaceIds.Count == 0 || FaceIds.Any(string.IsNullOrWhiteSpace) || !double.IsFinite(ThicknessCm) || ThicknessCm <= 0) throw new ArgumentException("faceIds and positive finite thicknessCm are required."); }
    }
    private sealed record CircularPatternOption(string? SourceFeatureId, string AxisFeatureId, int InstanceCount, double TotalAngleDeg, bool OppositeDirection, bool EqualSpace)
    {
        public void Validate() { RequireId(AxisFeatureId, "axisFeatureId"); OptionalId(SourceFeatureId, "sourceFeatureId"); if (InstanceCount is < 2 or > 100 || !double.IsFinite(TotalAngleDeg) || TotalAngleDeg <= 0 || TotalAngleDeg > 360) throw new ArgumentException("Circular pattern count must be 2-100 and angle must be finite and in (0,360]."); }
    }
    private sealed record RectangularPatternOption(string? SourceFeatureId, string DirectionOneFeatureId, double DistanceCm, int InstanceCount, string? DirectionTwoFeatureId, double? DistanceTwoCm, int? InstanceCountTwo, bool OppositeDirection, bool IsCentered, bool OppositeDirectionTwo, bool IsCenteredTwo)
    {
        public void Validate() { RequireId(DirectionOneFeatureId, "directionOneFeatureId"); OptionalId(SourceFeatureId, "sourceFeatureId"); if (!double.IsFinite(DistanceCm) || DistanceCm <= 0 || InstanceCount is < 2 or > 100) throw new ArgumentException("First rectangular direction requires positive finite distanceCm and count 2-100."); var second = DirectionTwoFeatureId is not null || DistanceTwoCm.HasValue || InstanceCountTwo.HasValue || OppositeDirectionTwo || IsCenteredTwo; if (second && (string.IsNullOrWhiteSpace(DirectionTwoFeatureId) || !DistanceTwoCm.HasValue || !InstanceCountTwo.HasValue || !double.IsFinite(DistanceTwoCm.Value) || DistanceTwoCm <= 0 || InstanceCountTwo is < 2 or > 100)) throw new ArgumentException("Second rectangular direction requires explicit ID, positive finite distance, and count 2-100."); }
    }
    private sealed record MirrorOption(string? SourceFeatureId, string Plane)
    {
        public void Validate() { OptionalId(SourceFeatureId, "sourceFeatureId"); if (!CanonicalPlane(Plane)) throw new ArgumentException("mirror.plane must be Top, Front, or Right."); }
    }

    private static IReadOnlyList<SketchPoint>? ParseProfile(JsonElement root)
        {
            if (!root.TryGetProperty("profile", out var element)) return null;
            if (element.ValueKind != JsonValueKind.Array) throw new ArgumentException("profile must be an array of point objects.");
            return element.EnumerateArray().Select((point, index) =>
            {
                var obj = RequireObject(point, $"profile[{index}]");
                RejectUnknown(obj, "x_cm", "y_cm");
                return new SketchPoint(RequiredNumber(obj, "x_cm"), RequiredNumber(obj, "y_cm"));
            }).ToArray();
        }

        private static SelectionOption? ParseSelection(JsonElement root, string name, string dimension)
    {
        if (!root.TryGetProperty(name, out var element)) return null; var obj = RequireObject(element, name); RejectUnknown(obj, "edgeIds", dimension);
        return new(ReadIds(obj, "edgeIds"), RequiredNumber(obj, dimension));
    }
    private static ShellOption? ParseShell(JsonElement root)
    {
        if (!root.TryGetProperty("shell", out var element)) return null; var obj = RequireObject(element, "shell"); RejectUnknown(obj, "faceIds", "thicknessCm", "oppositeDirection");
        return new(ReadIds(obj, "faceIds"), RequiredNumber(obj, "thicknessCm"), OptionalBool(obj, "oppositeDirection"));
    }
    private static CircularPatternOption? ParseCircular(JsonElement root)
    {
        if (!root.TryGetProperty("circularPattern", out var element)) return null; var obj = RequireObject(element, "circularPattern"); RejectUnknown(obj, "sourceFeatureId", "axisFeatureId", "instanceCount", "totalAngleDeg", "oppositeDirection", "equalSpace");
        return new(GetOptionalString(obj, "sourceFeatureId"), RequiredString(obj, "axisFeatureId"), RequiredInt(obj, "instanceCount"), RequiredNumber(obj, "totalAngleDeg"), OptionalBool(obj, "oppositeDirection"), OptionalBool(obj, "equalSpace", true));
    }
    private static RectangularPatternOption? ParseRectangular(JsonElement root)
    {
        if (!root.TryGetProperty("rectangularPattern", out var element)) return null; var obj = RequireObject(element, "rectangularPattern"); RejectUnknown(obj, "sourceFeatureId", "directionOneFeatureId", "distanceCm", "instanceCount", "directionTwoFeatureId", "distanceTwoCm", "instanceCountTwo", "oppositeDirection", "isCentered", "oppositeDirectionTwo", "isCenteredTwo");
        return new(GetOptionalString(obj, "sourceFeatureId"), RequiredString(obj, "directionOneFeatureId"), RequiredNumber(obj, "distanceCm"), RequiredInt(obj, "instanceCount"), GetOptionalString(obj, "directionTwoFeatureId"), OptionalNumber(obj, "distanceTwoCm"), OptionalInt(obj, "instanceCountTwo"), OptionalBool(obj, "oppositeDirection"), OptionalBool(obj, "isCentered"), OptionalBool(obj, "oppositeDirectionTwo"), OptionalBool(obj, "isCenteredTwo"));
    }
    private static MirrorOption? ParseMirror(JsonElement root)
    {
        if (!root.TryGetProperty("mirror", out var element)) return null; var obj = RequireObject(element, "mirror"); RejectUnknown(obj, "sourceFeatureId", "plane"); return new(GetOptionalString(obj, "sourceFeatureId"), RequiredString(obj, "plane"));
    }

    private static JsonElement RequireObject(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object ? element : throw new ArgumentException($"{name} must be an object.");
    private static void RejectUnknown(JsonElement obj, params string[] allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in obj.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new ArgumentException($"Duplicate option '{property.Name}'.");
            if (!allowed.Contains(property.Name, StringComparer.Ordinal)) throw new ArgumentException($"Unknown option '{property.Name}'.");
        }
    }
    private static string RequiredString(JsonElement obj, string name) => GetOptionalString(obj, name) is { Length: > 0 } value ? value : throw new ArgumentException($"{name} is required.");
    private static string? GetOptionalString(JsonElement obj, string name) => !obj.TryGetProperty(name, out var value) ? null : value.ValueKind == JsonValueKind.String ? value.GetString() : throw new ArgumentException($"{name} must be a string.");
    private static double RequiredNumber(JsonElement obj, string name) => OptionalNumber(obj, name) ?? throw new ArgumentException($"{name} is required.");
    private static double? OptionalNumber(JsonElement obj, string name) => !obj.TryGetProperty(name, out var value) ? null : value.TryGetDouble(out var number) ? number : throw new ArgumentException($"{name} must be a number.");
    private static int RequiredInt(JsonElement obj, string name) => OptionalInt(obj, name) ?? throw new ArgumentException($"{name} is required.");
    private static int? OptionalInt(JsonElement obj, string name) => !obj.TryGetProperty(name, out var value) ? null : value.TryGetInt32(out var number) ? number : throw new ArgumentException($"{name} must be an integer.");
    private static bool OptionalBool(JsonElement obj, string name, bool defaultValue = false) => !obj.TryGetProperty(name, out var value) ? defaultValue : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw new ArgumentException($"{name} must be a boolean.");
    private static IReadOnlyList<string> ReadIds(JsonElement obj, string name) { if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) throw new ArgumentException($"{name} must be an array of explicit Onshape IDs."); return value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : throw new ArgumentException($"{name} must contain strings.")).ToArray(); }
    private static bool CanonicalPlane(string plane) => plane.Equals("Top", StringComparison.OrdinalIgnoreCase) || plane.Equals("Front", StringComparison.OrdinalIgnoreCase) || plane.Equals("Right", StringComparison.OrdinalIgnoreCase);
    private static void RequireId(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} must be an explicit Onshape ID."); }
    private static void OptionalId(string? value, string name) { if (value is not null) RequireId(value, name); }
}
