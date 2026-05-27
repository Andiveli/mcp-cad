"""Tab: Sketch → Panel: Pattern — circular pattern of sketch entities.

Examples:
    # Pattern entities 1,2,3 around entity 4, 6 copies, 360°
    skill_pattern_circular(entities="1,2,3", axis="4", count=6)

    # 45° offset between instances, on both sides
    skill_pattern_circular(entities="1", axis="1", count=4, angle=45, fitted=False, symmetric=True)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_pattern_circular(
    provider: CADProvider,
    entities: str = "1",
    axis: str = "1",
    count: int = 6,
    angle: float = 360.0,
    fitted: bool = True,
    symmetric: bool = False,
) -> dict[str, Any]:
    """Create a circular pattern of sketch entities.

    Select entities by their index in the sketch (1-based), and
    an axis point for the rotation center.

    Args:
        entities: Comma-separated entity indices, e.g. "1,2,3".
        axis: Sketch point index for rotation center.
        count: Number of instances including original.
        angle: Degrees between instances (or total sweep if fitted=True).
        fitted: True → 360° with 6 count = 60° spacing equally.
                False → angle is offset between each instance.
        symmetric: Distribute on both sides of original geometry.

    Returns:
        dict with ``success``, ``pattern_type``, count, angle.

    Examples:
        # 6 copies equally around a circle
        skill_pattern_circular(entities="1", axis="2", count=6)

        # 4 copies at 45° each
        skill_pattern_circular(entities="1", axis="1", count=4, angle=45, fitted=False)
    """
    try:
        return provider.sketch_circular_pattern(
            entities, axis, count, angle, fitted, symmetric,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
