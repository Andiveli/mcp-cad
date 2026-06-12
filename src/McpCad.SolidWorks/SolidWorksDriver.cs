using System.Runtime.InteropServices;
using McpCad.Core.Exceptions;
using SldWorks = SolidWorks.Interop.sldworks.SldWorks;
using ModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;

namespace McpCad.SolidWorks;

/// <summary>
/// Low-level driver for SolidWorks COM connection lifecycle.
/// Mirrors InventorDriver structure but for "SldWorks.Application".
/// Handles attach-to-running (GetActiveObject), idempotent Connect/Health/Disconnect,
/// stale RPC detection, best-effort ReleaseComObject.
/// See design.md §4 and engram #272 sw-01 (COM lifetime), sw-02 (GetActive vs Create), sw-03 (version).
/// </summary>
public class SolidWorksDriver
{
    private SldWorks? _swApp;
    private bool _connected;
    private bool _explicitlyDisconnected; // for robust Disconnect (prevents auto-reconnect in getter leaving inconsistent state after RPC/stale)

    // P/Invoke — .NET 8 doesn't expose Marshal.GetActiveObject (same as InventorDriver)
    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid lpclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private static object GetActiveComObject(string progId)
    {
        int hr = CLSIDFromProgID(progId, out Guid clsid);
        if (hr < 0)
            throw new COMException($"Invalid ProgID '{progId}'", hr);
        hr = GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
        if (hr < 0)
            throw new COMException($"No running instance of '{progId}'", hr);
        return obj;
    }

    /// <summary>
    /// The active SolidWorks Application COM object.
    /// Auto-connects on first access if not already connected (unless explicitly disconnected).
    /// Throws <see cref="InventorConnectionException"/> if connection fails. State after Disconnect/RPC protected by explicit flag.
    /// </summary>
    public SldWorks SwApp
    {
        get
        {
            if (_swApp is not null)
                return _swApp;

            if (_explicitlyDisconnected)
                throw new InventorConnectionException("Explicitly disconnected (via Disconnect()). Call Connect() to re-attach. Prevents stale auto-connect races post-RPC.");

            // Auto-connect on first use — Connect() is idempotent and handles all error cases (returns error dict on fail, does not throw)
            Connect();

            if (_swApp is null)
                throw new InventorConnectionException(
                    "Not connected to SolidWorks. Make sure SolidWorks is running. (Connect returned error state; see prior health/Connect result)");

            return _swApp;
        }
    }

    /// <summary>Whether the driver currently holds a COM reference to SolidWorks.</summary>
    public bool IsConnected => _connected && _swApp is not null;

    /// <summary>
    /// Connect to a running SolidWorks instance via COM (ProgID "SldWorks.Application").
    /// Preferred: attach to running (GetActiveObject). Create/launch path optional for robustness but not default for MCP-CAD usage.
    /// Idempotent — returns existing connection if healthy.
    /// Returns neutral dict + "provider":"SolidWorks".
    /// </summary>
    public Dictionary<string, object?> Connect()
    {
        // If already connected, probe health to detect stale references
        if (_connected && _swApp is not null)
        {
            var health = Health();
            if (health.TryGetValue("connected", out var conn) && conn is true)
                return health;
        }

        try
        {
            _swApp = (SldWorks)GetActiveComObject("SldWorks.Application");
            _connected = true;
            _explicitlyDisconnected = false; // re-attach clears explicit state for cycles

            string version;
            try
            {
                // SolidWorks version: RevisionNumber or GetBuildNumbers etc. Use what is stable.
                version = _swApp.RevisionNumber() ?? "unknown";
                // Optionally enrich: var build = _swApp.GetBuildNumbers(...) but keep simple for skeleton
            }
            catch
            {
                version = "unknown";
            }

            return new Dictionary<string, object?>
            {
                ["connected"] = true,
                ["version"] = version,
                ["solidworks_version"] = version,
                ["provider"] = "SolidWorks",
            };
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80040154))
        {
            // REGDB_E_CLASSNOTREG — SolidWorks not installed or not registered
            _swApp = null;
            _connected = false;
            return new Dictionary<string, object?>
            {
                ["connected"] = false,
                ["error"] = "SolidWorks is not installed or COM class not registered.",
                ["provider"] = "SolidWorks",
            };
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80080005))
        {
            // CO_E_SERVERFAILURE — SolidWorks running but access denied (permissions / add-in interference)
            _swApp = null;
            _connected = false;
            return new Dictionary<string, object?>
            {
                ["connected"] = false,
                ["error"] = $"Permission denied accessing SolidWorks: {ex.Message}",
                ["provider"] = "SolidWorks",
            };
        }
        catch (Exception ex)
        {
            _swApp = null;
            _connected = false;
            return new Dictionary<string, object?>
            {
                ["connected"] = false,
                ["error"] = $"Failed to connect to SolidWorks: {ex.Message}",
                ["provider"] = "SolidWorks",
            };
        }
    }

    /// <summary>
    /// Release the COM reference to SolidWorks without closing the application.
    /// Idempotent — safe to call when not connected.
    /// Best-effort Marshal.ReleaseComObject + FinalRelease + null (ensures unreachable for cycles; getter will throw on explicit until re-Connect).
    /// </summary>
    public Dictionary<string, object?> Disconnect()
    {
        if (_swApp is not null)
        {
            try
            {
                Marshal.ReleaseComObject(_swApp);
            }
            catch
            {
                // Best-effort release — COM object may already be gone or RPC stale
            }
            try
            {
                Marshal.FinalReleaseComObject(_swApp);
            }
            catch
            {
                // Best-effort final release for robust repeated connect/disconnect cycles
            }
            _swApp = null;
        }
        _connected = false;
        _explicitlyDisconnected = true;
        // Best-effort to help release (COM lifetime per issue; not guaranteed in all hosts)
        try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch (Exception) { /* best-effort GC cleanup */ }
        return new Dictionary<string, object?> { ["status"] = "disconnected", ["provider"] = "SolidWorks" };
    }

    /// <summary>
    /// Check connection health and document state.
    /// Always safe to call — never throws.
    /// Probes ActiveDoc (ModelDoc2) + document count. Handles RPC_E_DISCONNECTED gracefully.
    /// Includes "provider":"SolidWorks" + version keys.
    /// </summary>
    public Dictionary<string, object?> Health()
    {
        if (_swApp is null || !_connected)
        {
            return DisconnectedHealth();
        }

        try
        {
            string version;
            try
            {
                version = _swApp.RevisionNumber() ?? "unknown";
            }
            catch
            {
                version = "unknown";
            }

            int docsCount;
            try
            {
                // Use dynamic to access .Documents (property does not exist on early-bound SldWorks interop in this build env).
                // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (Documents collection)
                var docs = ((dynamic)_swApp).Documents;
                if (docs is System.Collections.ICollection coll) docsCount = coll.Count;
                else if (docs is Array arr) docsCount = arr.Length;
                else docsCount = 0;
            }
            catch
            {
                docsCount = 0;
            }

            string? activeDoc = null;
            try
            {
                var doc = _swApp.ActiveDoc as ModelDoc2;
                if (doc is not null)
                {
                    activeDoc = doc.GetPathName();
                    if (string.IsNullOrEmpty(activeDoc))
                        activeDoc = doc.GetTitle();
                }
            }
            catch
            {
                // No active document or COM reference is stale
            }

            return new Dictionary<string, object?>
            {
                ["connected"] = true,
                ["version"] = version,
                ["solidworks_version"] = version,
                ["documents_open"] = docsCount,
                ["active_document"] = activeDoc,
                ["provider"] = "SolidWorks",
            };
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80010108))
        {
            // RPC_E_DISCONNECTED — SolidWorks was closed externally
            _swApp = null;
            _connected = false;
            return DisconnectedHealth();
        }
        catch
        {
            _swApp = null;
            _connected = false;
            return DisconnectedHealth();
        }
    }

    /// <summary>Get the active SolidWorks document, or null if none. (ModelDoc2)</summary>
    public object? ActiveDocument
    {
        get
        {
            try { return SwApp.ActiveDoc; }
            catch { return null; }
        }
    }

    private static Dictionary<string, object?> DisconnectedHealth() => new()
    {
        ["connected"] = false,
        ["version"] = null,
        ["solidworks_version"] = null,
        ["documents_open"] = 0,
        ["active_document"] = null,
        ["provider"] = "SolidWorks",
    };
}
