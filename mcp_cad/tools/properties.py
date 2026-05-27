"""iProperty management tools — get, set, summary, custom get/set.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def iproperty_get(
    provider: CADProvider,
    name: str,
    property_set: str = "Summary",
) -> dict[str, Any]:
    """Get an iProperty value by name."""
    try:
        return provider.iproperty_get(name, property_set)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def iproperty_set(
    provider: CADProvider,
    name: str,
    value: Any,
    property_set: str = "Summary",
) -> dict[str, Any]:
    """Set an iProperty value by name."""
    try:
        return provider.iproperty_set(name, value, property_set)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def iproperty_summary(provider: CADProvider) -> dict[str, Any]:
    """Get all Summary iProperties."""
    try:
        return provider.iproperty_summary()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def iproperty_custom_get(
    provider: CADProvider,
    name: str,
) -> dict[str, Any]:
    """Get a custom iProperty by name."""
    try:
        return provider.iproperty_custom_get(name)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def iproperty_custom_set(
    provider: CADProvider,
    name: str,
    value: Any,
) -> dict[str, Any]:
    """Set a custom iProperty. Creates it if it doesn't exist."""
    try:
        return provider.iproperty_custom_set(name, value)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}