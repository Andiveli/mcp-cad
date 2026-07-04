using McpCad.Core;
using McpCad.Core.Exceptions;

namespace McpCad.Tools;

/// <summary>Per-call provider resolution for converged MCP tools.</summary>
public static class RouterToolDispatch
{
    public static Dictionary<string, object?> WithProvider(
        IProviderRouter router,
        Func<ICadProvider, Dictionary<string, object?>> action)
    {
        var provider = router.Resolve(router.CurrentProvider);
        return action(provider);
    }

    public static Dictionary<string, object?> WithMechanical(
        IProviderRouter router,
        string operation,
        Func<IMechanicalCadProvider, Dictionary<string, object?>> action)
    {
        var provider = router.Resolve(router.CurrentProvider);
        if (provider is not IMechanicalCadProvider mech)
        {
            return ToolHelpers.Error(
                $"{router.CurrentProvider} does not support {operation} operations");
        }

        return action(mech);
    }

    public static Dictionary<string, object?> WithWeb(
        IProviderRouter router,
        Func<ICadWebProvider, Dictionary<string, object?>> action)
    {
        var provider = router.Resolve(router.CurrentProvider);
        if (provider is not ICadWebProvider web)
        {
            return ToolHelpers.Error(
                "This operation requires a web CAD provider (Onshape)");
        }

        return action(web);
    }

    public static Dictionary<string, object?> CatchMechanical(
        IProviderRouter router,
        string operation,
        Func<IMechanicalCadProvider, Dictionary<string, object?>> action)
        => WithMechanical(router, operation, mech => ToolExceptionCatch.Run(() => action(mech)));

    public static Dictionary<string, object?> CatchProvider(
        IProviderRouter router,
        Func<ICadProvider, Dictionary<string, object?>> action)
        => WithProvider(router, p => ToolExceptionCatch.Run(() => action(p)));

    public static Dictionary<string, object?> CatchWeb(
        IProviderRouter router,
        Func<ICadWebProvider, Dictionary<string, object?>> action)
        => WithWeb(router, web => ToolExceptionCatch.Run(() => action(web)));
}

internal static class ToolExceptionCatch
{
    public static Dictionary<string, object?> Run(Func<Dictionary<string, object?>> action)
    {
        try
        {
            return action();
        }
        catch (InventorConnectionException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (InventorComException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (CadProviderException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error: {ex.Message}");
        }
    }
}