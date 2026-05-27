"""Tab: Sketch → Panel: Constrain — dimension constraint.

Modes: linear, radius, diameter, angle.

Examples:
    # Linear dimension between points 1 and 2
    skill_dimension(mode="linear", entity1="1", entity2="2", value=25)

    # Radius dimension on circle
    skill_dimension(mode="radius", entity1="3", value=5)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_dimension(
    provider: CADProvider,
    mode: str = "linear",
    entity1: str = "1",
    entity2: str = "",
    value: float | None = None,
    orientation: str = "aligned",
    position_x: float | None = None,
    position_y: float | None = None,
) -> dict[str, Any]:
    """Add a dimension constraint to the active sketch.

    Args:
        mode: "linear", "radius", "diameter", or "angle".
        entity1: First entity/point index (1-based).
        entity2: Second entity/point index (linear and angle modes).
        value: Dimension value in cm. If None, uses current measured value.
        orientation: "aligned" (default), "horizontal", or "vertical" (linear only).
        position_x, position_y: Optional text placement.

    Examples:
        skill_dimension(mode="linear", entity1="1", entity2="2", value=25)
        skill_dimension(mode="radius", entity1="3", value=5)
        skill_dimension(mode="angle", entity1="1", entity2="2", value=45)
    """
    try:
        return provider.sketch_dimension(
            mode, entity1, entity2, value, orientation, position_x, position_y,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
