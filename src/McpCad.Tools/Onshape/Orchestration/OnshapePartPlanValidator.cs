using System.Collections.Immutable;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Builders;

namespace McpCad.Tools.Onshape.Orchestration;

internal static class OnshapePartPlanValidator
{
    internal const string SingleBodyResultAssumption =
        "add and remove use a single-body-result assumption; explicit multi-body targeting is unsupported.";

    public static ValidatedOnshapePartPlan Validate(CadElementAddress address, OnshapePartPlan plan)
    {
        ValidateAddress(address);
        if (plan.SchemaVersion != 1) Fail("UNSUPPORTED_SCHEMA_VERSION", "schemaVersion must be 1");
        if (plan.Stages.Length is < 1 or > 16) Fail("PLAN_LIMIT_EXCEEDED", "stages must contain 1 through 16 entries");
        if (plan.Stages.Count(stage => stage is CheckpointPlanStage) > 3) Fail("PLAN_LIMIT_EXCEEDED", "plans may contain at most 3 checkpoints");

        var symbols = new Dictionary<string, StageSymbol>(StringComparer.Ordinal);
        var validated = ImmutableArray.CreateBuilder<ValidatedOnshapePlanStage>(plan.Stages.Length);
        string? latestModelingId = null;
        for (var index = 0; index < plan.Stages.Length; index++)
        {
            var stage = plan.Stages[index];
            if (string.IsNullOrWhiteSpace(stage.Id) || !symbols.TryAdd(stage.Id, new StageSymbol(index, Kind(stage))))
                Fail("INVALID_REFERENCE", $"stage {index} has a duplicate or empty id");

            switch (stage)
            {
                case SketchPlanStage sketch:
                    validated.Add(new ValidatedSketchPlanStage(sketch.Id, Compile(sketch)));
                    latestModelingId = sketch.Id;
                    break;
                case ExtrudePlanStage extrude:
                    if (!symbols.TryGetValue(extrude.SketchId, out var sketchSymbol) || sketchSymbol.Kind != PlanStageKind.Sketch)
                        Fail("INVALID_REFERENCE", $"extrude '{extrude.Id}' must reference an earlier sketch");
                    if (!double.IsFinite(extrude.DepthCm) || extrude.DepthCm == 0)
                        Fail("INVALID_GEOMETRY", $"extrude '{extrude.Id}' depth_cm must be finite and nonzero");
                    if (!Enum.IsDefined(extrude.Operation) || !Enum.IsDefined(extrude.Direction))
                        Fail("INVALID_PLAN_SCHEMA", $"extrude '{extrude.Id}' has an unsupported operation or direction");
                    // DepthCm remains centimeter-space here; the executor converts it once at the feature-template boundary.
                    validated.Add(new ValidatedExtrudePlanStage(extrude.Id, extrude.SketchId, extrude.Operation, extrude.DepthCm, extrude.Direction));
                    latestModelingId = extrude.Id;
                    break;
                case CheckpointPlanStage checkpoint:
                    if (!Enum.IsDefined(checkpoint.View)) Fail("INVALID_PLAN_SCHEMA", $"checkpoint '{checkpoint.Id}' has an unsupported view");
                    if (latestModelingId is null || checkpoint.AfterStageId != latestModelingId || !symbols.TryGetValue(checkpoint.AfterStageId, out var target)
                        || target.Kind is not (PlanStageKind.Sketch or PlanStageKind.Extrude))
                        Fail("INVALID_REFERENCE", $"checkpoint '{checkpoint.Id}' must target the latest preceding modeling stage");
                    validated.Add(new ValidatedCheckpointPlanStage(checkpoint.Id, checkpoint.View));
                    break;
                default:
                    Fail("INVALID_PLAN_SCHEMA", $"stage '{stage.Id}' has an unsupported kind");
                    break;
            }
        }

        return new ValidatedOnshapePartPlan(address, validated.ToImmutable());
    }

    private static string Compile(SketchPlanStage stage)
    {
        if (!Enum.IsDefined(stage.Plane)) Fail("INVALID_PLAN_SCHEMA", $"sketch '{stage.Id}' has an unsupported plane");
        try
        {
            using var document = stage.Profile switch
            {
                RectangleProfile rectangle => Rectangle(rectangle, stage.Plane),
                CircleProfile circle => Circle(circle, stage.Plane),
                PolygonProfile polygon => OnshapeSketchBuilder.BuildClosedProfile(polygon.PointsCm, stage.Plane.ToString()),
                MixedProfile mixed => OnshapeMixedProfileFacade.BuildMixedClosedProfile(new MixedClosedProfile(mixed.Start, mixed.Segments), stage.Plane.ToString()),
                _ => throw new ArgumentException("Unsupported closed profile."),
            };
            return document.RootElement.GetRawText();
        }
        catch (ArgumentException exception)
        {
            Fail("INVALID_GEOMETRY", $"sketch '{stage.Id}' is invalid: {exception.Message}");
            return string.Empty;
        }
    }

    private static System.Text.Json.JsonDocument Rectangle(RectangleProfile profile, CanonicalSketchPlane plane)
    {
        if (!Finite(profile.X1Cm, profile.Y1Cm, profile.X2Cm, profile.Y2Cm) || profile.X1Cm == profile.X2Cm || profile.Y1Cm == profile.Y2Cm)
            Fail("INVALID_GEOMETRY", "rectangle coordinates must be finite with nonzero width and height");
        return OnshapeSketchBuilder.BuildRectangle(
            OnshapeUnitConversion.CmToMeters(profile.X1Cm), OnshapeUnitConversion.CmToMeters(profile.Y1Cm),
            OnshapeUnitConversion.CmToMeters(profile.X2Cm), OnshapeUnitConversion.CmToMeters(profile.Y2Cm), plane.ToString());
    }

    private static System.Text.Json.JsonDocument Circle(CircleProfile profile, CanonicalSketchPlane plane)
    {
        if (!Finite(profile.CxCm, profile.CyCm, profile.RadiusCm) || profile.RadiusCm <= 0)
            Fail("INVALID_GEOMETRY", "circle coordinates must be finite and radius_cm must be positive");
        return OnshapeSketchBuilder.BuildCircle(
            OnshapeUnitConversion.CmToMeters(profile.CxCm), OnshapeUnitConversion.CmToMeters(profile.CyCm),
            OnshapeUnitConversion.CmToMeters(profile.RadiusCm), plane.ToString());
    }

    private static void ValidateAddress(CadElementAddress address)
    {
        if (address is null || string.IsNullOrWhiteSpace(address.DocumentId) || string.IsNullOrWhiteSpace(address.WorkspaceOrVersionId)
            || string.IsNullOrWhiteSpace(address.ElementId) || (address.MicroversionId is not null && string.IsNullOrWhiteSpace(address.MicroversionId)))
            Fail("INVALID_ADDRESS", "address fields must be non-empty");
    }

    private static PlanStageKind Kind(OnshapePlanStage stage) => stage switch
    {
        SketchPlanStage => PlanStageKind.Sketch,
        ExtrudePlanStage => PlanStageKind.Extrude,
        CheckpointPlanStage => PlanStageKind.Checkpoint,
        _ => throw new ArgumentException("INVALID_PLAN_SCHEMA: unsupported stage kind"),
    };

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
    private static void Fail(string code, string message) => throw new ArgumentException($"{code}: {message}");
}
