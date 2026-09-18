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
