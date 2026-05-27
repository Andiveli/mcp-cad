"""Tab: Sketch → Panel: Draw — arc operations.

Modes:
    center — center + start + end points (cx,cy, sx,sy, ex,ey)
    sweep  — center + radius + angles (cx,cy, radius, start_angle, sweep_angle)
    3point — three points along arc (x1,y1, x_mid,y_mid, x_end,y_end)

Angles are in DEGREES (converted to radians internally for Inventor).

Examples:
    # Center-defined arc from (10,0) to (0,10) around origin
    skill_arc(mode="center", cx=0, cy=0, sx=10, sy=0, ex=0, ey=10)

    # Sweep angle arc: radius 5, from 0° to 90°
    skill_arc(mode="sweep", cx=0, cy=0, radius=5, start_angle=0, sweep_angle=90)

    # 3-point arc
    skill_arc(mode="3point", x1=0, y1=0, x_mid=5, y_mid=5, x_end=10, y_end=0)
"""

from __future__ import annotations

from math import atan2, cos, degrees, radians, sin, sqrt, pi
from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def _angle_from_center(cx: float, cy: float, px: float, py: float) -> float:
    """Angle in radians from center (cx,cy) to point (px,py), 0° = +X axis."""
    return atan2(py - cy, px - cx)


def _sweep_between(
    cx: float, cy: float,
    sx: float, sy: float,
    ex: float, ey: float,
    ccw: bool = True,
) -> float:
    """Compute sweep angle from start to end around center.

    Returns angle in radians, positive = CCW.
    """
    a_start = _angle_from_center(cx, cy, sx, sy)
    a_end = _angle_from_center(cx, cy, ex, ey)

    if ccw:
        if a_end <= a_start:
            a_end += 2 * pi
    else:
        if a_end >= a_start:
            a_end -= 2 * pi

    return a_end - a_start


def skill_arc(
    provider: CADProvider,
    mode: str = "center",
    # --- center mode ---
    cx: float = 0.0,
    cy: float = 0.0,
    sx: float = 1.0,
    sy: float = 0.0,
    ex: float = 0.0,
    ey: float = 1.0,
    ccw: bool = True,
    # --- sweep mode ---
    radius: float = 1.0,
    start_angle: float = 0.0,
    sweep_angle: float = 90.0,
    # --- 3point mode ---
    x1: float = 0.0,
    y1: float = 0.0,
    x_mid: float = 0.0,
    y_mid: float = 0.0,
    x_end: float = 0.0,
    y_end: float = 0.0,
) -> dict[str, Any]:
    """Draw an arc in the active sketch.

    Args:
        mode: ``"center"`` (default), ``"sweep"``, or ``"3point"``.
        cx, cy: Center point (center and sweep modes).
        sx, sy: Start point (center mode).
        ex, ey: End point (center mode).
        ccw: Counter-clockwise sweep (center mode, default True).
        radius: Arc radius in cm (sweep mode).
        start_angle: Start angle in degrees, 0° = +X (sweep mode).
        sweep_angle: Sweep angle in degrees, CCW (sweep mode).
        x1,y1: Start point (3point mode).
        x_mid,y_mid: Point along arc (3point mode).
        x_end,y_end: End point (3point mode).

    Returns:
        dict with ``success``, ``entity_type``, mode, and parameters.

    Examples:
        # Center arc: origin, from right to top, CCW
        skill_arc(cx=0, cy=0, sx=10, sy=0, ex=0, ey=10)

        # Sweep: radius 5, 0° to 180° (semicircle)
        skill_arc(mode="sweep", cx=0, cy=0, radius=5, start_angle=0, sweep_angle=180)

        # 3-point arc
        skill_arc(mode="3point", x1=0, y1=0, x_mid=5, y_mid=5, x_end=10, y_end=0)
    """
    try:
        if mode == "center":
            r = sqrt((sx - cx) ** 2 + (sy - cy) ** 2)
            sa_rad = _angle_from_center(cx, cy, sx, sy)
            sw_rad = _sweep_between(cx, cy, sx, sy, ex, ey, ccw)
            return provider.sketch_arc(cx, cy, r, degrees(sa_rad), degrees(sa_rad + sw_rad))
        elif mode == "sweep":
            return provider.sketch_arc(cx, cy, radius, start_angle, start_angle + sweep_angle)
        elif mode == "3point":
            # Compute center from 3 points (same math as skill_circle)
            # Use perpendicular bisector of chords (x1,y1)→(x_mid,y_mid) and (x_mid,y_mid)→(x_end,y_end)
            mx1 = (x1 + x_mid) / 2.0
            my1 = (y1 + y_mid) / 2.0
            mx2 = (x_mid + x_end) / 2.0
            my2 = (y_mid + y_end) / 2.0

            dx1 = x_mid - x1
            dy1 = y_mid - y1
            dx2 = x_end - x_mid
            dy2 = y_end - y_mid

            eps = 1e-12
            if abs(dy1) < eps:
                if abs(dy2) < eps:
                    raise ValueError("Points are collinear — cannot form an arc")
                s2 = -dx2 / dy2
                arc_cx = mx1
                arc_cy = s2 * (arc_cx - mx2) + my2
            elif abs(dy2) < eps:
                s1 = -dx1 / dy1
                arc_cx = mx2
                arc_cy = s1 * (arc_cx - mx1) + my1
            else:
                s1 = -dx1 / dy1
                s2 = -dx2 / dy2
                if abs(s1 - s2) < eps:
                    raise ValueError("Points are collinear — cannot form an arc")
                arc_cx = (my2 - my1 + s1 * mx1 - s2 * mx2) / (s1 - s2)
                arc_cy = s1 * (arc_cx - mx1) + my1

            arc_r = sqrt((arc_cx - x1) ** 2 + (arc_cy - y1) ** 2)
            sa_rad = _angle_from_center(arc_cx, arc_cy, x1, y1)
            # Sweep angle: from start to end passing through mid
            ea_rad = _angle_from_center(arc_cx, arc_cy, x_end, y_end)
            ma_rad = _angle_from_center(arc_cx, arc_cy, x_mid, y_mid)
            # Determine direction by checking if mid is between start and end CCW
            if ea_rad <= sa_rad:
                ea_rad += 2 * pi
            if ma_rad <= sa_rad:
                ma_rad += 2 * pi
            if ma_rad <= ea_rad:
                pass  # CCW: start → mid → end
            else:
                ea_rad -= 2 * pi  # CW: end is before start

            return provider.sketch_arc(arc_cx, arc_cy, arc_r, degrees(sa_rad), degrees(ea_rad))
        else:
            return {
                "success": False,
                "error": f"Unknown mode '{mode}'. Use 'center', 'sweep', or '3point'.",
            }
    except ValueError as exc:
        return {"success": False, "error": str(exc)}
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
