"""Revolve skill — composable 3D revolve operation.

Creates a sketch, draws a profile and axis line, then revolves the
profile around the axis to produce a 3D solid.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider


def skill_revolve(
    provider: CADProvider,
    plane: str = "XY",
    profile_cx: float = 3.0,
    profile_cy: float = 0.0,
    profile_radius: float = 1.0,
    axis_x: float = 0.0,
    axis_y1: float = -1.0,
    axis_y2: float = 5.0,
    angle: float = 360.0,
    operation: str = "join",
) -> dict[str, Any]:
    """Tab: 3D — Revolve a circular profile around a vertical axis.

    Composes sketch_create + sketch_circle + sketch_line + revolve.
    Defaults produce a torus (circle at x=3 revolved around x=0).
    For a solid cylinder set profile_cx=0 so the circle touches the axis.

    Parameters
    ----------
    plane:
        Work plane: "XY", "XZ", or "YZ" (default: "XY").
    profile_cx, profile_cy:
        Center of the circular profile (cm).  Offset from the axis.
        Set ``profile_cx=0`` for a solid cylinder (circle on axis).
    profile_radius:
        Radius of the circular profile (cm).
    axis_x:
        X position of the vertical revolution axis line.
    axis_y1, axis_y2:
        Start/end Y of the axis line (must extend past the profile).
    angle:
        Revolution angle in degrees (default: 360 for full revolve).
    operation:
        "join", "cut", or "intersect" (default: "join").

    Returns
    -------
    dict with sketch creation, drawing, and revolve results.

    Examples
    --------
    # Torus (donut): circle offset from axis
    skill_revolve(plane="XY", profile_cx=5, profile_radius=2)

    # Solid cylinder: circle at axis, touches axis at its left edge
    skill_revolve(plane="XY", profile_cx=0, profile_radius=3)

    # Partial revolve (180° half-torus)
    skill_revolve(plane="XY", profile_cx=4, profile_radius=1.5, angle=180)
    """
    results: dict[str, Any] = {}

    # 1. Create sketch
    r = provider.sketch_create(plane)
    results["sketch"] = r
    if not r.get("success"):
        return results

    # 2. Draw circular profile.
    # For a solid cylinder set profile_cx = profile_radius so the circle
    # touches (but does not cross) the axis.
    r = provider.sketch_circle(profile_cx, profile_cy, profile_radius)
    results["profile"] = r
    if not r.get("success"):
        return results

    # 3. Draw axis line (vertical at axis_x).
    # The axis must NOT intersect the profile — keep it outside the circle.
    r = provider.sketch_line(axis_x, axis_y1, axis_x, axis_y2)
    results["axis"] = r
    if not r.get("success"):
        return results

    # 4. Revolve: profile is entity 1 (circle), axis is SketchLine 1
    r = provider.revolve(
        profile="1",
        axis="1",  # first SketchLine
        angle=angle,
        operation=operation,
    )
    results["revolve"] = r

    return results
