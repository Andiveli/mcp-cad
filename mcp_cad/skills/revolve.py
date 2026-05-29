"""Revolve skill — composable 3D revolve operation.

Composes sketch_create + profile drawing + axis line + revolve into a
single call.  For custom profiles drawn with sketch tools, use the
``revolve()`` tool directly — this skill auto-draws the profile.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider


def skill_revolve(
    provider: CADProvider,
    plane: str = "XY",
    profile: str = "",
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

    For custom profiles (rectangles, splines, etc.), draw your shape
    with sketch tools, then call ``revolve(profile=\"1\", axis=\"N\")``
    where N is the SketchLine index of the axis line.

    Parameters
    ----------
    plane:
        Work plane: "XY", "XZ", or "YZ" (default: "XY").
    profile_cx, profile_cy:
        Center of the auto-drawn circular profile (cm).
    profile_radius:
        Radius of the auto-drawn circular profile (cm).
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
    dict with sketch, profile, axis, and revolve results.

    Examples
    --------
    # Torus (donut)
    skill_revolve(profile_cx=5, profile_radius=2)

    # Cut a groove from an existing body
    skill_revolve(profile_cx=2, profile_radius=0.5, operation="cut")

    # For custom profiles, use revolve() directly:
    sketch_create("XY")
    sketch_rectangle(x1=1, y1=0, x2=3, y2=5)   # profile to revolve
    sketch_line(0, -1, 0, 6)                     # axis line at x=0
    revolve(profile="1", axis="3")               # SketchLine 3 = axis
    """
    results: dict[str, Any] = {}

    # 1. Create sketch (only when auto-drawing the profile — if the
    #    user provides a profile index, they already have a sketch)
    if not profile:
        r = provider.sketch_create(plane)
        results["sketch"] = r
        if not r.get("success"):
            return results

    # 2. Draw profile if none provided
    profile_index = profile
    if not profile:
        r = provider.sketch_circle(profile_cx, profile_cy, profile_radius)
        results["profile"] = r
        if not r.get("success"):
            return results
        profile_index = "1"  # first entity is the circle

    # 3. Draw axis line (vertical at axis_x), tagged as "eje"
    r = provider.sketch_line(axis_x, axis_y1, axis_x, axis_y2, tag="eje")
    results["axis"] = r
    if not r.get("success"):
        return results

    # 4. Revolve using tag reference
    r = provider.revolve(
        profile=profile_index,
        axis="@eje",
        angle=angle,
        operation=operation,
    )
    results["revolve"] = r

    return results

    # 2. Draw circular profile
    r = provider.sketch_circle(profile_cx, profile_cy, profile_radius)
    results["profile"] = r
    if not r.get("success"):
        return results

    # 3. Draw axis line (vertical at axis_x)
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
