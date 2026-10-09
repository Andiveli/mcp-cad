namespace McpCad.Onshape;

/// <summary>Per-session Onshape defaults; authentication state is added with provider integration.</summary>
public sealed class OnshapeDriver
{
    /// <summary>Default workspace/version mode, historically "w" unless configured otherwise.</summary>
    public string DefaultWvm { get; set; } = "w";
}
