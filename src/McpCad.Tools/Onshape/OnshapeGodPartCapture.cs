using McpCad.Core.Models;
using ModelContextProtocol.Protocol;

namespace McpCad.Tools.Onshape;

public partial class OnshapeSkillTools
{
    partial void AppendFinalCaptures(Dictionary<string, object?> result, CadElementAddress address, int width, int height, string edges, ref CallToolResult? captureResult)
    {
        var captures = new List<Dictionary<string, object?>>();
        var images = new List<ContentBlock>();
        foreach (var view in new[] { "iso", "top" })
        {
            try
            {
                var capture = provider.CaptureView(address, view, width, height, edges);
                if (!IsSuccessful(capture) || !capture.TryGetValue("image_base64", out var image) || image is not string imageBase64 || string.IsNullOrWhiteSpace(imageBase64))
                {
                    captures.Add(CaptureFailure(view, capture, GetError(capture, $"Onshape {view} capture failed.")));
                    continue;
                }

                try
                {
                    images.Add(ImageContentBlock.FromBytes(Convert.FromBase64String(imageBase64), "image/png"));
                    captures.Add(new Dictionary<string, object?> { ["view"] = view, ["success"] = true, ["mime_type"] = "image/png", ["image_base64"] = imageBase64, ["result"] = capture });
                }
                catch (FormatException ex)
                {
                    captures.Add(CaptureFailure(view, capture, $"Onshape {view} capture returned invalid base64: {ex.Message}"));
                }
            }
            catch (Exception ex)
            {
                captures.Add(CaptureFailure(view, null, ex.Message));
            }
        }

        var captureSuccess = captures.All(capture => capture["success"] is true);
        result["modelingSuccess"] = true;
        result["captureSuccess"] = captureSuccess;
        result["captures"] = captures;
        result["success"] = captureSuccess;
        if (!captureSuccess)
        {
            result["failedPhase"] = "capture";
            result["error"] = "One or more final visual captures failed.";
        }

        captureResult = ToCallToolResult(result, images);
    }

    private static Dictionary<string, object?> CaptureFailure(string view, Dictionary<string, object?>? result, string error) => new()
    {
        ["view"] = view, ["success"] = false, ["mime_type"] = result?.TryGetValue("mime_type", out var mimeType) is true ? mimeType : null,
        ["image_base64"] = null, ["result"] = result, ["error"] = error,
    };
}
