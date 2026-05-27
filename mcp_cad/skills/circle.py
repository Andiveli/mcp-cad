"""Tab: Sketch → Panel: Draw — circle operations.

Modes:
    center — center point + radius (cx, cy, radius)
    3point — three perimeter points (x1,y1, x2,y2, x3,y3)

Examples:
    # Circle at (5,5) with radius 3
    skill_circle(cx=5, cy=5, radius=3)

    # 3-point circle through three perimeter points
    skill_circle(mode="3point", x1=0, y1=0, x2=5, y2=5, x3=10, y3=0)
"""

from __future__ import annotations

from math import sqrt
from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def _circle_from_three_points(
    x1: float, y1: float,
    x2: float, y2: float,
    x3: float, y3: float,
) -> tuple[float, float, float]:
    """Compute center (cx, cy) and radius from three perimeter points.

    Uses perpendicular bisector intersection.  Returns (cx, cy, radius).
    """
    # Midpoints of chords P1-P2 and P2-P3
    mx1 = (x1 + x2) / 2.0
    my1 = (y1 + y2) / 2.0
    mx2 = (x2 + x3) / 2.0
    my2 = (y2 + y3) / 2.0

    # Slopes of chords
    dx1 = x2 - x1
    dy1 = y2 - y1
    dx2 = x3 - x2
    dy2 = y3 - y2

    # Perpendicular slopes (negative reciprocal).
    # Handle vertical/horizontal edge cases.
    eps = 1e-12

    if abs(dy1) < eps:
        # Chord P1-P2 is horizontal → perpendicular bisector is vertical (x = mx1)
        if abs(dy2) < eps:
            # Both chords horizontal → points are collinear, circle is degenerate
            raise ValueError("Points are collinear — cannot form a circle")
        # Perp bisector of P2-P3: passes through (mx2, my2) with slope -dx2/dy2
        s2 = -dx2 / dy2
        cx = mx1
        cy = s2 * (cx - mx2) + my2
    elif abs(dy2) < eps:
        # Chord P2-P3 is horizontal → perpendicular bisector is vertical (x = mx2)
        s1 = -dx1 / dy1
        cx = mx2
        cy = s1 * (cx - mx1) + my1
    else:
        s1 = -dx1 / dy1
        s2 = -dx2 / dy2

        if abs(s1 - s2) < eps:
            raise ValueError("Points are collinear — cannot form a circle")

        # Intersection: s1*(x - mx1) + my1 = s2*(x - mx2) + my2
        cx = (my2 - my1 + s1 * mx1 - s2 * mx2) / (s1 - s2)
        cy = s1 * (cx - mx1) + my1

    # Radius = distance from center to any point
    radius = sqrt((cx - x1) ** 2 + (cy - y1) ** 2)
    return cx, cy, radius


def skill_circle(
    provider: CADProvider,
    mode: str = "center",
    # --- center mode ---
    cx: float = 0.0,
    cy: float = 0.0,
    radius: float = 1.0,
    # --- 3point mode ---
    x1: float = 0.0,
    y1: float = 0.0,
    x2: float = 0.0,
    y2: float = 0.0,
    x3: float = 0.0,
    y3: float = 0.0,
) -> dict[str, Any]:
    """Draw a circle in the active sketch.

    Center mode uses ``AddByCenterRadius``.
    3-point mode computes center and radius from three perimeter
    points, then uses the same underlying circle tool — no
    provider changes needed.

    Args:
        mode: Circle type — ``"center"`` (default) or ``"3point"``.
        cx, cy: Center point (center mode).
        radius: Radius in cm (center mode).
        x1,y1, x2,y2, x3,y3: Three perimeter points (3point mode).

    Returns:
        dict with ``success``, ``entity_type``, mode, and parameters.

    Examples:
        # Circle at origin, radius 5
        skill_circle(cx=0, cy=0, radius=5)

        # 3-point circle
        skill_circle(mode="3point", x1=0, y1=0, x2=10, y2=10, x3=20, y3=0)
    """
    try:
        if mode == "center":
            return provider.sketch_circle(cx, cy, radius)
        elif mode == "3point":
            cx_calc, cy_calc, r_calc = _circle_from_three_points(
                x1, y1, x2, y2, x3, y3
            )
            return provider.sketch_circle(cx_calc, cy_calc, r_calc)
        else:
            return {
                "success": False,
                "error": f"Unknown mode '{mode}'. Use 'center' or '3point'.",
            }
    except ValueError as exc:
        return {"success": False, "error": str(exc)}
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}

