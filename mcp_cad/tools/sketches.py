"""Sketch operation tools — create, draw, dimension.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def sketch_create(provider: CADProvider, plane: str = "XY") -> dict[str, Any]:
    """Create a new sketch on the specified work plane."""
    try:
        return provider.sketch_create(plane)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_line(
    provider: CADProvider,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
) -> dict[str, Any]:
    """Draw a line segment in the active sketch."""
    try:
        return provider.sketch_line(x1, y1, x2, y2)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_circle(
    provider: CADProvider,
    cx: float,
    cy: float,
    radius: float,
) -> dict[str, Any]:
    """Draw a circle in the active sketch."""
    try:
        return provider.sketch_circle(cx, cy, radius)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_arc(
    provider: CADProvider,
    cx: float,
    cy: float,
    radius: float,
    start_angle: float,
    end_angle: float,
) -> dict[str, Any]:
    """Draw an arc in the active sketch."""
    try:
        return provider.sketch_arc(cx, cy, radius, start_angle, end_angle)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_rectangle(
    provider: CADProvider,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
) -> dict[str, Any]:
    """Draw a rectangle in the active sketch."""
    try:
        return provider.sketch_rectangle(x1, y1, x2, y2)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_dimension(
    provider: CADProvider,
    entity: str,
    value: float,
    position_x: float | None = None,
    position_y: float | None = None,
) -> dict[str, Any]:
    """Add a dimension constraint to the active sketch."""
    try:
        if position_x is not None and position_y is not None:
            position: tuple[float, float] | None = (position_x, position_y)
        else:
            position = None
        return provider.sketch_dimension(entity, value, position)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}