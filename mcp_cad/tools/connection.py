"""Connection lifecycle tools — connect, disconnect, health.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def connect(provider: CADProvider) -> dict[str, Any]:
    """Connect to the CAD application."""
    try:
        return provider.connect()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def disconnect(provider: CADProvider) -> dict[str, Any]:
    """Release the connection to the CAD application."""
    try:
        return provider.disconnect()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def health(provider: CADProvider) -> dict[str, Any]:
    """Check connection health and document state."""
    try:
        return provider.health()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}