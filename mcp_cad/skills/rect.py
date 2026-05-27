"""Tab: Sketch → Panel: Draw — rectangle operations.

Modes:
    diagonal — two opposite corners (x1,y1, x2,y2)
    center   — center point + one corner (cx,cy, corner_x, corner_y)

Examples:
    # Rectangle from (0,0) to (10,5)
    skill_rect(x1=0, y1=0, x2=10, y2=5)

    # Centered rectangle: center at (5,5), corner at (10,7.5)
    skill_rect(mode="center", cx=5, cy=5, corner_x=10, corner_y=7.5)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_rect(
    provider: CADProvider,
    mode: str = "diagonal",
    # --- diagonal mode ---
    x1: float = 0.0,
    y1: float = 0.0,
    x2: float = 10.0,
    y2: float = 10.0,
    # --- center mode ---
    cx: float = 0.0,
    cy: float = 0.0,
    corner_x: float = 5.0,
    corner_y: float = 5.0,
) -> dict[str, Any]:
    """Draw a rectangle in the active sketch.

    Diagonal mode uses ``AddAsTwoPointRectangle`` (two opposite corners,
    aligned to sketch axes).  Center mode uses
    ``AddAsTwoPointCenteredRectangle`` (center + one corner).

    Args:
        mode: ``"diagonal"`` (default) or ``"center"``.
        x1, y1, x2, y2: Opposite corners (diagonal mode).
        cx, cy: Center point (center mode).
        corner_x, corner_y: One corner (center mode).

    Returns:
        dict with ``success``, ``entity_type``, mode, and parameters.

    Examples:
        # Rectangle from origin to (10,5)
        skill_rect(x1=0, y1=0, x2=10, y2=5)

        # Centered 20x10 rectangle at (10,5)
        skill_rect(mode="center", cx=10, cy=5, corner_x=20, corner_y=10)
    """
    try:
        if mode == "diagonal":
            return provider.sketch_rectangle(x1, y1, x2, y2)
        elif mode == "center":
            # AddAsTwoPointCenteredRectangle: center (cx,cy), corner (corner_x, corner_y)
            # The existing sketch_rectangle uses diagonal mode.
            # For center mode, compute the opposite corner from center symmetry.
            opp_x = 2.0 * cx - corner_x
            opp_y = 2.0 * cy - corner_y
            return provider.sketch_rectangle(corner_x, corner_y, opp_x, opp_y)
        else:
            return {
                "success": False,
                "error": f"Unknown mode '{mode}'. Use 'diagonal' or 'center'.",
            }
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
