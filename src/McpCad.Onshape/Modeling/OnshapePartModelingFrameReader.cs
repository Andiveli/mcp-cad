using System.Text.Json;
using McpCad.Core.Exceptions;
using McpCad.Core.Modeling;
using McpCad.Core.Models;

namespace McpCad.Onshape.Modeling;

internal enum OnshapePartModelingFrameReadFailure
{
    Notice,
    MicroversionSkew,
    UnsupportedValue,
    MalformedFrame,
}

internal sealed class OnshapePartModelingFrameReadException : CadProviderException
{
    public OnshapePartModelingFrameReadException(OnshapePartModelingFrameReadFailure failure)
        : base("The Onshape datum frame response is invalid.")
    {
        Failure = failure;
    }

    public OnshapePartModelingFrameReadFailure Failure { get; }
}

internal readonly record struct OnshapeFrameVector(double X, double Y, double Z);

internal sealed record OnshapePartModelingFrame(
    OnshapeFrameVector Origin,
    OnshapeFrameVector XAxis,
    OnshapeFrameVector YAxis,
    OnshapeFrameVector Normal);

/// <summary>Reads one canonical datum frame without creating or modifying model features.</summary>
internal sealed class OnshapePartModelingFrameReader
{
    private readonly OnshapeClient _client;
    private readonly CadElementAddress _target;

    public OnshapePartModelingFrameReader(OnshapeClient client, CadElementAddress target)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public async Task<OnshapePartModelingFrame> ReadAsync(CanonicalPlane plane, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var datumFeatureName = DatumFeatureName(plane);
        ValidateTarget();
        using var features = await _client.SendJsonAsync(
            HttpMethod.Get, FeaturesPath(), cancellationToken: cancellationToken).ConfigureAwait(false);
        var libraryVersion = ReadLibraryVersion(features.RootElement);
        var request = JsonSerializer.Serialize(new
        {
            btType = "BTFeatureScriptEvalCall-2377",
            libraryVersion,
            script = EvaluationScript(datumFeatureName),
        });
        using var evaluation = await _client.SendJsonAsync(
            HttpMethod.Post,
            EvaluationPath(),
            request,
            new Dictionary<string, string?> { ["rollbackBarIndex"] = "-1" },
            cancellationToken).ConfigureAwait(false);
        return OnshapePartModelingFrameParser.Parse(evaluation.RootElement);
    }

    private static string EvaluationScript(string datumFeatureName) => $$"""
        function(context is Context, queries) {
            const datum = qCreatedBy(makeId("{{datumFeatureName}}"), EntityType.FACE);
            const plane = alignCanonically(context, evPlane(context, {"face": datum}));
            const y = cross(plane.normal, plane.x);
            return [["origin", plane.origin[0] / meter, plane.origin[1] / meter, plane.origin[2] / meter],
                    ["x", plane.x[0], plane.x[1], plane.x[2]],
                    ["y", y[0], y[1], y[2]],
                    ["normal", plane.normal[0], plane.normal[1], plane.normal[2]]];
        }
        """;

    private static int ReadLibraryVersion(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object)
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }

        if (response.TryGetProperty("microversionSkew", out var skew))
        {
            if (skew.ValueKind != JsonValueKind.False && skew.ValueKind != JsonValueKind.True)
            {
                throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
            }

            if (skew.GetBoolean())
            {
                throw Failure(OnshapePartModelingFrameReadFailure.MicroversionSkew);
            }
        }

        if (!response.TryGetProperty("libraryVersion", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var value) || value < 1)
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }

        return value;
    }

    private string FeaturesPath() => $"/api/v6/partstudios/d/{Segment(_target.DocumentId)}/w/{Segment(_target.WorkspaceOrVersionId)}/e/{Segment(_target.ElementId)}/features";

    private string EvaluationPath() => $"/api/v17/partstudios/d/{Segment(_target.DocumentId)}/w/{Segment(_target.WorkspaceOrVersionId)}/e/{Segment(_target.ElementId)}/featurescript";

    private void ValidateTarget()
    {
        if (string.IsNullOrWhiteSpace(_target.DocumentId) || string.IsNullOrWhiteSpace(_target.WorkspaceOrVersionId)
            || string.IsNullOrWhiteSpace(_target.ElementId))
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }
    }

    private static string DatumFeatureName(CanonicalPlane plane) => plane switch
    {
        CanonicalPlane.Top => "Top",
        CanonicalPlane.Front => "Front",
        CanonicalPlane.Right => "Right",
        _ => throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame),
    };

    private static string Segment(string value) => Uri.EscapeDataString(value);

    private static OnshapePartModelingFrameReadException Failure(OnshapePartModelingFrameReadFailure failure) => new(failure);
}
