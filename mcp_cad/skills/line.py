"""Tab: Sketch → Panel: Draw — line operations.

Modes:
    simple   — two-point line (start_x, start_y → end_x, end_y)
    midpoint — mid + end point line (mid_x, mid_y → end_x, end_y)

Examples:
    # Simple line from (0,0) to (5,5)
    skill_line(mode="simple", start_x=0, start_y=0, end_x=5, end_y=5)

    # Midpoint line: center at (5,5), goes to (10,5)
    skill_line(mode="midpoint", mid_x=5, mid_y=5, end_x=10, end_y=5)
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_line(
    provider: CADProvider,
    mode: str = "simple",
    # --- simple / midpoint shared ---
    end_x: float = 0.0,
    end_y: float = 0.0,
    # --- simple mode ---
    start_x: float = 0.0,
    start_y: float = 0.0,
    # --- midpoint mode ---
    mid_x: float = 0.0,
    mid_y: float = 0.0,
) -> dict[str, Any]:
    """Draw a line in the active sketch.

    The skill auto-activates a sketch on XY if none is active.
    Uses ``AddByTwoPoints`` for simple lines and
    ``AddByMidEndPoints`` for midpoint lines.

    Args:
        mode: Line type — ``"simple"`` (default) or ``"midpoint"``.
        end_x, end_y: End point of the line (both modes).
        start_x, start_y: Start point (simple mode only).
        mid_x, mid_y: Midpoint / center point (midpoint mode only).

    Returns:
        dict with ``success``, ``entity_type``, mode, and coordinates.

    Examples:
        # Simple from (0,0) to (10,5) — most common
        skill_line(start_x=0, start_y=0, end_x=10, end_y=5)

        # Midpoint line: center at (50,50), extends to (100,50)
        skill_line(mode="midpoint", mid_x=50, mid_y=50, end_x=100, end_y=50)
    """
    try:
        if mode == "simple":
            return provider.sketch_line(start_x, start_y, end_x, end_y)
        elif mode == "midpoint":
            # AddByMidEndPoints draws a line centered at (mid, end).
            # Compute the opposite endpoint so we can use AddByTwoPoints.
            opp_x = 2.0 * mid_x - end_x
            opp_y = 2.0 * mid_y - end_y
            return provider.sketch_line(opp_x, opp_y, end_x, end_y)
        else:
            return {
                "success": False,
                "error": f"Unknown mode '{mode}'. Use 'simple' or 'midpoint'.",
            }
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
