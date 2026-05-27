"""Document management tools — open, new, save, close.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def doc_open(provider: CADProvider, path: str) -> dict[str, Any]:
    """Open an existing document."""
    try:
        return provider.doc_open(path)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def doc_new_part(provider: CADProvider, template: str = "") -> dict[str, Any]:
    """Create a new part document."""
    try:
        return provider.doc_new_part(template)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def doc_new_assembly(provider: CADProvider, template: str = "") -> dict[str, Any]:
    """Create a new assembly document."""
    try:
        return provider.doc_new_assembly(template)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def doc_save(provider: CADProvider) -> dict[str, Any]:
    """Save the active document."""
    try:
        return provider.doc_save()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def doc_save_as(provider: CADProvider, path: str) -> dict[str, Any]:
    """Save the active document to a new path."""
    try:
        return provider.doc_save_as(path)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def doc_close(provider: CADProvider, save: bool = True) -> dict[str, Any]:
    """Close the active document."""
    try:
        return provider.doc_close(save)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}