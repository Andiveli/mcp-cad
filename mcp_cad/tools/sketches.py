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
        return provider.sketch_dimension(
            entity, value, position_x=position_x, position_y=position_y
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_point(
    provider: CADProvider,
    x: float,
    y: float,
) -> dict[str, Any]:
    """Draw a point in the active sketch."""
    try:
        return provider.sketch_point(x, y)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_spline(
    provider: CADProvider,
    points: list[tuple[float, float]],
    fit_method: str = "sweet",
) -> dict[str, Any]:
    """Draw a spline through fit points."""
    try:
        return provider.sketch_spline(points, fit_method)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_ellipse(
    provider: CADProvider,
    cx: float,
    cy: float,
    major_radius: float,
    minor_radius: float,
    major_axis_angle: float = 0.0,
) -> dict[str, Any]:
    """Draw an ellipse in the active sketch."""
    try:
        return provider.sketch_ellipse(cx, cy, major_radius, minor_radius, major_axis_angle)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_circular_pattern(
    provider: CADProvider,
    entities: str,
    axis: str,
    count: int,
    angle: float = 360.0,
    fitted: bool = True,
    symmetric: bool = False,
) -> dict[str, Any]:
    """Create a circular pattern of sketch entities."""
    try:
        return provider.sketch_circular_pattern(
            entities, axis, count, angle, fitted, symmetric,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_rectangular_pattern(
    provider: CADProvider,
    entities: str,
    x_axis: str,
    x_count: int,
    x_spacing: float,
    y_axis: str = "",
    y_count: int = 1,
    y_spacing: float = 0.0,
) -> dict[str, Any]:
    """Create a rectangular pattern of sketch entities."""
    try:
        return provider.sketch_rectangular_pattern(
            entities, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_offset(
    provider: CADProvider,
    entities: str,
    distance: float,
    natural_direction: bool = True,
    include_connected: bool = False,
) -> dict[str, Any]:
    """Offset sketch entities by a distance."""
    try:
        return provider.sketch_offset(entities, distance, natural_direction, include_connected)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_move(
    provider: CADProvider,
    entities: str,
    dx: float,
    dy: float,
    copy: bool = False,
) -> dict[str, Any]:
    """Move sketch entities by a vector."""
    try:
        return provider.sketch_move(entities, dx, dy, copy)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_rotate(
    provider: CADProvider,
    entities: str,
    cx: float,
    cy: float,
    angle: float,
    copy: bool = False,
) -> dict[str, Any]:
    """Rotate sketch entities around a center point."""
    try:
        return provider.sketch_rotate(entities, cx, cy, angle, copy)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_delete(
    provider: CADProvider,
) -> dict[str, Any]:
    """Delete the active sketch."""
    try:
        return provider.sketch_delete()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_constraint(
    provider: CADProvider,
    mode: str,
    entity1: str,
    entity2: str = "",
    sym_line: str = "",
    axis: str = "major",
) -> dict[str, Any]:
    """Add a geometric constraint between sketch entities."""
    try:
        return provider.sketch_constraint(mode, entity1, entity2, sym_line, axis)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_trim(
    provider: CADProvider,
    entity: str,
    cutting_entity: str,
    side: str = "end",
) -> dict[str, Any]:
    """Trim a sketch entity to its intersection with another."""
    try:
        return provider.sketch_trim(entity, cutting_entity, side)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_scale(
    provider: CADProvider,
    entities: str,
    cx: float,
    cy: float,
    factor: float,
) -> dict[str, Any]:
    """Scale sketch entities around a center point."""
    try:
        return provider.sketch_scale(entities, cx, cy, factor)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def sketch_mirror(
    provider: CADProvider,
    entities: str,
    mirror_entity: str,
) -> dict[str, Any]:
    """Mirror sketch entities across a mirror line."""
    try:
        return provider.sketch_mirror(entities, mirror_entity)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}