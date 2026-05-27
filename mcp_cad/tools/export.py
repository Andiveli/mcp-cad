"""Export operation tools — STEP, STL, PDF, DXF.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def export_step(
    provider: CADProvider,
    path: str,
    options: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Export the active document to STEP format."""
    try:
        return provider.export_step(path, options)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def export_stl(
    provider: CADProvider,
    path: str,
    options: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Export the active document to STL format."""
    try:
        return provider.export_stl(path, options)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def export_pdf(
    provider: CADProvider,
    path: str,
    options: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Export the active document to PDF format."""
    try:
        return provider.export_pdf(path, options)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def export_dxf(
    provider: CADProvider,
    path: str,
    options: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Export the active document's sketch or flat pattern to DXF."""
    try:
        return provider.export_dxf(path, options)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}