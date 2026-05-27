"""Tab: Sketch → Panel: Draw — ellipse operations.

Examples:
    # Ellipse at origin, major=5 horizontal, minor=3 vertical
    skill_ellipse(cx=0, cy=0, major_radius=5, minor_radius=3)

    # Rotated ellipse: major axis at 45°
    skill_ellipse(cx=5, cy=5, major_radius=8, minor_radius=4, angle=45)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_ellipse(
    provider: CADProvider,
    cx: float = 0.0,
    cy: float = 0.0,
    major_radius: float = 5.0,
    minor_radius: float = 3.0,
    angle: float = 0.0,
) -> dict[str, Any]:
    """Draw an ellipse in the active sketch.

    Args:
        cx, cy: Center point.
        major_radius: Major axis radius in cm.
        minor_radius: Minor axis radius in cm.
        angle: Major axis angle in degrees (0° = +X axis).

    Returns:
        dict with ``success``, ``entity_type``, and parameters.

    Examples:
        # Horizontal ellipse: radius 5 x 3
        skill_ellipse(cx=0, cy=0, major_radius=5, minor_radius=3)

        # Rotated 45°
        skill_ellipse(cx=10, cy=10, major_radius=8, minor_radius=4, angle=45)
    """
    try:
        return provider.sketch_ellipse(cx, cy, major_radius, minor_radius, angle)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
