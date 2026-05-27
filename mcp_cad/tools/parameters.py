"""Parameter management tools — list, get, set, set_expression.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def param_list(
    provider: CADProvider,
    filter_pattern: str | None = None,
) -> dict[str, Any]:
    """List model parameters, optionally filtered by name pattern."""
    try:
        return provider.param_list(filter_pattern)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def param_get(provider: CADProvider, name: str) -> dict[str, Any]:
    """Get a specific model parameter by name."""
    try:
        return provider.param_get(name)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def param_set(provider: CADProvider, name: str, value: float) -> dict[str, Any]:
    """Set a model parameter value by name."""
    try:
        return provider.param_set(name, value)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def param_set_expression(
    provider: CADProvider,
    name: str,
    expression: str,
) -> dict[str, Any]:
    """Set a model parameter using an expression (e.g. 'd0 * 2')."""
    try:
        return provider.param_set_expression(name, expression)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}