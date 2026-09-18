using McpCad.Core.Exceptions;
using McpCad.Core.Modeling;
using McpCad.Core.Models;

namespace McpCad.Onshape.Modeling;

internal sealed class OnshapePartModelingPreflightException : CadProviderException
{
    public OnshapePartModelingPreflightException(PartModelingError validationError)
        : base(validationError.Detail)
    {
        ValidationError = validationError;
    }

    public PartModelingError ValidationError { get; }
}

/// <summary>
/// Prepares one portable sketch/extrusion request for an address-bound Onshape target without model mutation.
/// </summary>
internal sealed class OnshapePartModelingPreflightAdapter
{
    private readonly OnshapePartModelingFrameReader _frameReader;

    public OnshapePartModelingPreflightAdapter(OnshapeClient client, CadElementAddress target)
    {
        _frameReader = new OnshapePartModelingFrameReader(client, target);
    }

    public async Task<OnshapePartModelingGeometry> PrepareAsync(
        SketchDefinition definition,
        ExtrusionDefinition extrusion,
        CancellationToken cancellationToken)
    {
        var normalizedSketch = PartModelingValidation.ValidateSketch(definition);
        if (!normalizedSketch.IsValid)
        {
            throw new OnshapePartModelingPreflightException(normalizedSketch.Error!);
        }

        var normalizedExtrusion = PartModelingValidation.ValidateExtrusion(extrusion);
        if (!normalizedExtrusion.IsValid)
        {
            throw new OnshapePartModelingPreflightException(normalizedExtrusion.Error!);
        }

        var frame = await _frameReader.ReadAsync(normalizedSketch.Value!.Plane, cancellationToken).ConfigureAwait(false);
        return OnshapePartModelingGeometryMapper.Map(normalizedSketch.Value, normalizedExtrusion.Value!, frame);
    }
}
