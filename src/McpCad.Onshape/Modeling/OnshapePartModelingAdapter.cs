using System.Text.Json;
using McpCad.Core.Modeling;
using McpCad.Core.Models;
using McpCad.Onshape.Builders;

namespace McpCad.Onshape.Modeling;

internal sealed class OnshapePartModelingAdapter
{
    private readonly OnshapeClient _client;

    public OnshapePartModelingAdapter(OnshapeClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public IPartModelingContext Bind(CadElementAddress target) =>
        new OnshapePartModelingContext(_client, target ?? throw new ArgumentNullException(nameof(target)));
}

internal sealed class OnshapePartModelingContext : IPartModelingContext
{
    private readonly OnshapeClient _client;
    private readonly CadElementAddress _target;
    private readonly OnshapePartModelingPreflightAdapter _preflight;
    private readonly Dictionary<SketchReference, string> _sketchIds = [];
    private PartModelingResult _result = new(
        new OperationResult<SketchReference>(OperationStatus.NotAttempted, MutationDisposition.None, null, null),
        new OperationResult<FeatureReference>(OperationStatus.NotAttempted, MutationDisposition.None, null, null));

    public OnshapePartModelingContext(OnshapeClient client, CadElementAddress target)
    {
        _client = client;
        _target = target;
        _preflight = new OnshapePartModelingPreflightAdapter(client, target);
    }

    public PartModelingCapabilities Capabilities { get; } = new(
        OptionalCapabilityStatus.Unsupported,
        OptionalCapabilityStatus.Unsupported);

    public PartModelingResult Result => _result;

    public async ValueTask<OperationResult<SketchReference>> CreateSketch(
        SketchDefinition definition,
        CancellationToken cancellationToken)
    {
        if (_result.Sketch.Status != OperationStatus.NotAttempted)
        {
            return _result.Sketch;
        }

        OnshapePartModelingGeometry geometry;
        try
        {
            geometry = await _preflight.PrepareAsync(definition, PlaceholderExtrusion(), cancellationToken).ConfigureAwait(false);
        }
        catch (OnshapePartModelingPreflightException ex)
        {
            return SetSketchFailure(ex.ValidationError, MutationDisposition.None);
        }
        catch (Exception ex) when (ex is OnshapePartModelingFrameReadException or OnshapePartModelingGeometryMapException)
        {
            return SetSketchFailure(AdapterError(ex.Message), MutationDisposition.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SetSketchFailure(AdapterError(ex.Message), MutationDisposition.None);
        }

        JsonDocument payload;
        try
        {
            payload = BuildSketch(geometry.Profile, definition.Plane);
        }
        catch (Exception ex)
        {
            return SetSketchFailure(AdapterError(ex.Message), MutationDisposition.None);
        }

        using (payload)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await _client.SendJsonAsync(
                    HttpMethod.Post, FeaturesPath(), payload.RootElement.GetRawText(), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var nativeId = FeatureId(response.RootElement);
                if (string.IsNullOrWhiteSpace(nativeId))
                {
                    return SetSketchFailure(AdapterError("The Onshape sketch response did not contain a feature ID."), MutationDisposition.Unknown,
                        OperationStatus.Uncertain);
                }

                var reference = SketchReference.Create();
                _sketchIds.Add(reference, nativeId);
                var operation = new OperationResult<SketchReference>(OperationStatus.Succeeded, MutationDisposition.Present, null, reference);
                _result = _result with { Sketch = operation };
                return operation;
            }
            catch (Exception ex)
            {
                return SetSketchFailure(AdapterError(ex.Message), MutationDisposition.Unknown, OperationStatus.Uncertain);
            }
        }
    }

    public async ValueTask<OperationResult<FeatureReference>> ExtrudeNewBody(
        SketchReference sketch,
        ExtrusionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (_result.Extrusion.Status != OperationStatus.NotAttempted || FailedOrUncertain(_result.Sketch.Status))
        {
            return _result.Extrusion;
        }

        if (sketch is null || !_sketchIds.TryGetValue(sketch, out var nativeSketchId))
        {
            return SetExtrusionFailure(AdapterError("The sketch reference was not issued by this context."), MutationDisposition.None);
        }

        var normalized = PartModelingValidation.ValidateExtrusion(definition);
        if (!normalized.IsValid)
        {
            return SetExtrusionFailure(normalized.Error!, MutationDisposition.None);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var payload = OnshapeFeatureTemplates.ExtrudeFromSketch(
                nativeSketchId, normalized.Value!.Depth, Direction(normalized.Value.Direction), "new");
            using var response = await _client.SendJsonAsync(
                HttpMethod.Post, FeaturesPath(), payload.RootElement.GetRawText(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(FeatureId(response.RootElement)))
            {
                return SetExtrusionFailure(AdapterError("The Onshape extrusion response did not contain a feature ID."), MutationDisposition.Unknown,
                    OperationStatus.Uncertain);
            }

            var operation = new OperationResult<FeatureReference>(
                OperationStatus.Succeeded, MutationDisposition.Present, null, FeatureReference.Create());
            _result = _result with { Extrusion = operation };
            return operation;
        }
        catch (Exception ex)
        {
            return SetExtrusionFailure(AdapterError(ex.Message), MutationDisposition.Unknown, OperationStatus.Uncertain);
        }
    }

    private OperationResult<SketchReference> SetSketchFailure(
        PartModelingError error,
        MutationDisposition mutation,
        OperationStatus status = OperationStatus.Failed)
    {
        var operation = new OperationResult<SketchReference>(status, mutation, error, null);
        _result = _result with { Sketch = operation };
        return operation;
    }

    private OperationResult<FeatureReference> SetExtrusionFailure(
        PartModelingError error,
        MutationDisposition mutation,
        OperationStatus status = OperationStatus.Failed)
    {
        var operation = new OperationResult<FeatureReference>(status, mutation, error, null);
        _result = _result with { Extrusion = operation };
        return operation;
    }

    private static JsonDocument BuildSketch(OnshapeMappedProfile profile, CanonicalPlane plane) => profile switch
    {
        OnshapeMappedRectangle rectangle => OnshapeSketchBuilder.BuildClosedProfile(
            rectangle.CornersM.Select(point => new SketchPoint(point.X * 100, point.Y * 100)).ToArray(), plane.ToString()),
        OnshapeMappedCircle circle => OnshapeSketchBuilder.BuildCircle(circle.CenterM.X, circle.CenterM.Y, circle.RadiusM, plane.ToString()),
        _ => throw new InvalidOperationException("The mapped Onshape profile is not supported."),
    };

    private static ExtrusionDefinition PlaceholderExtrusion() =>
        new(1, LengthUnit.Meter, ExtrusionDirection.Positive);

    private static string Direction(ExtrusionDirection direction) => direction == ExtrusionDirection.Negative ? "negative" : "positive";

    private string FeaturesPath() => $"/api/v6/partstudios/d/{Segment(_target.DocumentId)}/w/{Segment(_target.WorkspaceOrVersionId)}/e/{Segment(_target.ElementId)}/features";

    private static string Segment(string value) => Uri.EscapeDataString(value);

    private static string? FeatureId(JsonElement response) =>
        response.TryGetProperty("featureId", out var topLevel) && topLevel.ValueKind == JsonValueKind.String
            ? topLevel.GetString()
            : response.TryGetProperty("feature", out var feature) && feature.ValueKind == JsonValueKind.Object
                && feature.TryGetProperty("featureId", out var nested) && nested.ValueKind == JsonValueKind.String
                ? nested.GetString()
                : null;

    private static PartModelingError AdapterError(string detail) =>
        new(PartModelingErrorCode.DegenerateGeometry, detail);

    private static bool FailedOrUncertain(OperationStatus status) =>
        status is OperationStatus.Failed or OperationStatus.Uncertain;
}
