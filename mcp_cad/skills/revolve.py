"""Revolve skill — composable 3D revolve operation.

Handles sketch creation, axis line drawing, and revolve in one call.
The LLM can either draw its own profile with sketch tools and pass the
profile index, or let the skill draw a default circle profile.
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
    """Tab: 3D — Revolve a profile around a vertical axis line.

    Creates a sketch (if needed), draws an axis line, and revolves.
    The axis is always the first SketchLine (index "1").

    **Using an existing profile**: draw your shape with sketch tools,
    then pass ``profile="1"`` (the profile index).  The skill handles
    sketch + axis + revolve.

    **Letting the skill draw**: omit ``profile`` and the skill draws
    a circle with the given center and radius.

    Parameters
    ----------
    plane:
        Work plane: "XY", "XZ", or "YZ" (default: "XY").
    profile:
        Existing profile index to revolve (e.g. "1").
        If empty, a circle is drawn using the profile_* params.
    profile_cx, profile_cy:
        Center of the auto-drawn circular profile (cm).
    profile_radius:
        Radius of the auto-drawn circular profile (cm).
    axis_x:
        X position of the vertical revolution axis line.
    axis_y1, axis_y2:
        Start/end Y of the axis line.
    angle:
        Revolution angle in degrees (default: 360 for full revolve).
    operation:
        "join", "cut", or "intersect" (default: "join").

    Returns
    -------
    dict with sketch, profile, axis, and revolve results.

    Examples
    --------
    # Torus — skill draws everything
    skill_revolve(profile_cx=5, profile_radius=2)

    # Custom profile — LLM draws shape, skill does axis + revolve
    sketch_create("XY")
    sketch_rectangle(x1=0, y1=0, x2=3, y2=10)   # profile to revolve
    skill_revolve(profile="1", axis_x=0)          # axis at x=0, revolve it

    # Cut a groove from an existing body
    skill_revolve(profile_cx=2, profile_radius=0.5, operation="cut")
    """
    results: dict[str, Any] = {}

    # 1. Create sketch (if not already active)
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

    # 3. Draw axis line (vertical at axis_x)
    r = provider.sketch_line(axis_x, axis_y1, axis_x, axis_y2)
    results["axis"] = r
    if not r.get("success"):
        return results

    # 4. Revolve: axis is always SketchLine 1
    r = provider.revolve(
        profile=profile_index,
        axis="1",  # first SketchLine
        angle=angle,
        operation=operation,
    )
    results["revolve"] = r

    return results
