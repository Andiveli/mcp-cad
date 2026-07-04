namespace McpCad.Core;

/// <summary>Session-scoped provider override set by <c>mcp_cad_select_provider</c> (not persisted to config).</summary>
public sealed class CadExecutionContext
{
    private static readonly AsyncLocal<CadExecutionContext?> CurrentHolder = new();

    public static CadExecutionContext Current
    {
        get => CurrentHolder.Value ??= new CadExecutionContext();
        internal set => CurrentHolder.Value = value;
    }

    /// <summary>Skill-selected provider for this async flow; null uses config default.</summary>
    public string? SelectedProvider { get; set; }

    /// <summary>Active provider for the current router scope (Phase 2).</summary>
    public string? CurrentProvider { get; set; }

    /// <summary>Prior context restored when a <see cref="IProviderRouter.PushProvider"/> scope is disposed.</summary>
    public CadExecutionContext? PriorContext { get; set; }

    /// <summary>Clears async-local state between unit tests.</summary>
    public static void ResetForTests() => CurrentHolder.Value = null;
}