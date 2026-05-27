"""Tab: Sketch → Panel: Modify — offset, move, rotate.

Examples:
    # Offset entity 2 by 5cm
    skill_offset(entities="2", distance=5)

    # Move entity 1 by (10, 0)
    skill_move(entities="1", dx=10, dy=0)

    # Rotate entity 1 90° around origin
    skill_rotate(entities="1", cx=0, cy=0, angle=90)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_offset(
    provider: CADProvider,
    entities: str = "1",
    offset_x: float = 0.0,
    offset_y: float = 1.0,
    include_connected: bool = False,
) -> dict[str, Any]:
    """Offset sketch entities through a point.

    The shortest distance from the offset point to the base entity
    determines the offset distance.

    Args:
        entities: Comma-separated entity indices.
        offset_x, offset_y: Point through which the offset passes.
        include_connected: Also offset connected loop entities.

    Examples:
        skill_offset(entities="2", offset_x=0, offset_y=5)
    """
    try:
        return provider.sketch_offset(entities, offset_x, offset_y, include_connected)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def skill_move(
    provider: CADProvider,
    entities: str = "1",
    dx: float = 0.0,
    dy: float = 0.0,
    copy: bool = False,
) -> dict[str, Any]:
    """Move sketch entities by a vector.

    Args:
        entities: Comma-separated entity indices.
        dx, dy: Delta vector in cm.
        copy: True → copy instead of move.

    Examples:
        skill_move(entities="1", dx=10, dy=0)
        skill_move(entities="1,2", dx=5, dy=5, copy=True)
    """
    try:
        return provider.sketch_move(entities, dx, dy, copy)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def skill_rotate(
    provider: CADProvider,
    entities: str = "1",
    cx: float = 0.0,
    cy: float = 0.0,
    angle: float = 90.0,
    copy: bool = False,
) -> dict[str, Any]:
    """Rotate sketch entities around a center point.

    Args:
        entities: Comma-separated entity indices.
        cx, cy: Rotation center point.
        angle: Rotation angle in degrees (CCW).
        copy: True → copy instead of rotate.

    Examples:
        skill_rotate(entities="1", cx=0, cy=0, angle=90)
        skill_rotate(entities="1,2,3", cx=5, cy=5, angle=45, copy=True)
    """
    try:
        return provider.sketch_rotate(entities, cx, cy, angle, copy)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def skill_delete_sketch(
    provider: CADProvider,
) -> dict[str, Any]:
    """Delete the active sketch.

    Only works if the sketch is not consumed by a feature.

    Examples:
        skill_delete_sketch()
    """
    try:
        return provider.sketch_delete()
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def skill_trim(
    provider: CADProvider,
    entity: str = "1",
    cutting_entity: str = "2",
    side: str = "end",
) -> dict[str, Any]:
    """Trim a sketch entity to its intersection with another.

    Uses Geometry.Intersect() to find the intersection point,
    then moves the specified endpoint to that point.

    Args:
        entity: Entity index to trim (1-based).
        cutting_entity: Entity to trim against.
        side: "start" or "end" — which endpoint of entity to move.

    Examples:
        # Trim the end of entity 1 to where it meets entity 2
        skill_trim(entity="1", cutting_entity="2")

        # Trim the start of entity 3 to entity 1
        skill_trim(entity="3", cutting_entity="1", side="start")
    """
    try:
        return provider.sketch_trim(entity, cutting_entity, side)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def skill_scale(
    provider: CADProvider,
    entities: str = "1",
    cx: float = 0.0,
    cy: float = 0.0,
    factor: float = 2.0,
) -> dict[str, Any]:
    """Scale sketch entities around a center point.

    Uses Geometry to read positions, computes scaled coordinates via
    ``new = center + (old - center) * factor``, and moves endpoints.

    Args:
        entities: Comma-separated entity indices.
        cx, cy: Scale center point.
        factor: >1 = enlarge, <1 = shrink, 1 = no change.

    Examples:
        # Double size around origin
        skill_scale(entities="1,2", cx=0, cy=0, factor=2)

        # Shrink to half around center (5,5)
        skill_scale(entities="1", cx=5, cy=5, factor=0.5)
    """
    try:
        return provider.sketch_scale(entities, cx, cy, factor)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def skill_mirror(
    provider: CADProvider,
    entities: str = "1",
    mirror_entity: str = "2",
) -> dict[str, Any]:
    """Mirror sketch entities across a mirror line.

    Reflects each entity's endpoints across the specified line
    using point-to-line projection math.

    Args:
        entities: Comma-separated entity indices to mirror.
        mirror_entity: Index of the line entity used as mirror axis.

    Examples:
        skill_mirror(entities="1", mirror_entity="2")
        skill_mirror(entities="1,3,5", mirror_entity="2")
    """
    try:
        return provider.sketch_mirror(entities, mirror_entity)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
