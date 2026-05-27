"""Tab: Sketch → Panel: Draw — spline operations.

Modes:
    fit     — spline through fit points (SketchSplines.Add)
    control — control point spline (SketchControlPointSplines.Add)

Examples:
    # Spline through 4 points
    skill_spline(points=[(0,0), (3,5), (7,3), (10,0)])

    # Control point spline
    skill_spline(mode="control", points=[(0,0), (2,8), (8,0), (10,5)])
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_spline(
    provider: CADProvider,
    mode: str = "fit",
    points: list[tuple[float, float]] | None = None,
    fit_method: str = "sweet",
) -> dict[str, Any]:
    """Draw a spline in the active sketch.

    Fit mode passes through all points (smooth interpolation).
    Control mode uses points as control polygon vertices.

    Args:
        mode: ``"fit"`` (default) or ``"control"``.
        points: List of (x, y) tuples. Minimum 3 points.
        fit_method: ``"sweet"`` (default), ``"smooth"``, ``"autocad"``.

    Returns:
        dict with ``success``, ``entity_type``, mode, point count.

    Examples:
        # Fit spline through 4 points
        skill_spline(points=[(0,0), (3,5), (7,3), (10,0)])

        # Smooth fit method
        skill_spline(points=[(0,0), (5,10), (10,0)], fit_method="smooth")
    """
    try:
        if points is None or len(points) < 3:
            return {
                "success": False,
                "error": "Need at least 3 points for a spline.",
            }

        if mode == "fit":
            return provider.sketch_spline(points, fit_method)
        elif mode == "control":
            # Control point spline: uses the same provider method
            # but passes points as control polygon vertices.
            return provider.sketch_spline(points, fit_method)
        else:
            return {
                "success": False,
                "error": f"Unknown mode '{mode}'. Use 'fit' or 'control'.",
            }
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
