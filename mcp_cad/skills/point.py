"""Tab: Sketch → Panel: Draw — point operations.

Examples:
    # Point at (5, 3)
    skill_point(x=5, y=3)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_point(
    provider: CADProvider,
    x: float = 0.0,
    y: float = 0.0,
) -> dict[str, Any]:
    """Draw a point in the active sketch.

    Args:
        x, y: Point coordinates.

    Returns:
        dict with ``success``, ``entity_type``, and coordinates.

    Examples:
        skill_point(x=10, y=5)
    """
    try:
        return provider.sketch_point(x, y)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
