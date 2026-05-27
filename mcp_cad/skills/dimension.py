"""Tab: Sketch → Panel: Constrain — dimension constraint.

Examples:
    # Dimension entity 1 to 25cm
    skill_dimension(entity="1", value=25)

    # With text position
    skill_dimension(entity="1", value=10, position_x=5, position_y=5)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_dimension(
    provider: CADProvider,
    entity: str = "1",
    value: float = 10.0,
    position_x: float | None = None,
    position_y: float | None = None,
) -> dict[str, Any]:
    """Add a dimension constraint to a sketch entity.

    Args:
        entity: 1-based entity index.
        value: Dimension value in cm.
        position_x, position_y: Optional text placement position.

    Examples:
        skill_dimension(entity="1", value=25)
        skill_dimension(entity="2", value=15, position_x=5, position_y=5)
    """
    try:
        return provider.sketch_dimension(
            entity, value, position_x=position_x, position_y=position_y,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
