"""Tab: Sketch → Panel: Pattern — sketch entity patterns.

Examples:
    # Circular: 6 copies around axis point 2
    skill_pattern_circular(entities="1", axis="2", count=6)

    # Rectangular: 3x2 grid with 5cm spacing
    skill_pattern_rectangular(entities="1", x_axis="2", x_count=3, x_spacing=5, y_axis="3", y_count=2, y_spacing=5)
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


def skill_pattern_rectangular(
    provider: CADProvider,
    entities: str = "1",
    x_axis: str = "1",
    x_count: int = 2,
    x_spacing: float = 5.0,
    y_axis: str = "",
    y_count: int = 1,
    y_spacing: float = 0.0,
) -> dict[str, Any]:
    """Create a rectangular pattern of sketch entities.

    Args:
        entities: Comma-separated entity indices to pattern.
        x_axis: Linear sketch entity index for X direction.
        x_count: Number of instances in X.
        x_spacing: Spacing in X direction (cm).
        y_axis: Linear entity for Y (optional, for 2D grid).
        y_count: Instances in Y.
        y_spacing: Spacing in Y (cm).

    Returns:
        dict with ``success``, ``pattern_type``, counts, spacing.

    Examples:
        # Linear pattern: 5 copies at 10cm along X
        skill_pattern_rectangular(entities="1", x_axis="2", x_count=5, x_spacing=10)

        # 2D grid: 3x2 at 5cm
        skill_pattern_rectangular(entities="1", x_axis="2", x_count=3, x_spacing=5, y_axis="3", y_count=2, y_spacing=5)
    """
    try:
        return provider.sketch_rectangular_pattern(
            entities, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
