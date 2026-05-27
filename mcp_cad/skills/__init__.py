"""Skills package — composable CAD operations built on the CADProvider protocol.

Provides ``register_skills(mcp_instance, provider)`` which registers all
skill-based MCP tools.  Skills compose atomic provider operations into
higher-level workflows, organized by Inventor tabs and panels:

    Tab: Sketch → Panel: Draw
        skill_sketch   — create / activate sketch
        skill_line     — simple, midpoint, spline lines

Add new skills by creating a module in this package and registering its
tool(s) in ``register_skills()`` below.
"""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.core.protocol import CADProvider
from mcp_cad.skills.sketch import skill_sketch as _skill_sketch
from mcp_cad.skills.line import skill_line as _skill_line
from mcp_cad.skills.circle import skill_circle as _skill_circle


def register_skills(mcp_instance: FastMCP, provider: CADProvider) -> None:
    """Register all skill-based MCP tools on the FastMCP instance.

    Skills are registered AFTER ``register_tools()`` in server.py.
    """

    # ------------------------------------------------------------------
    # Tab: Sketch — Panel: Draw
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def skill_sketch(
        plane: str = "XY",
    ) -> dict[str, Any]:
        """Tab: Sketch — Create or activate a sketch on a work plane.

        Call before any draw skill. Uses the existing active sketch if
        already on the same plane.

        Args:
            plane: "XY" (default), "XZ", or "YZ".

        Examples:
            skill_sketch("XY")
        """
        return _skill_sketch(provider, plane)

    @mcp_instance.tool()
    def skill_line(
        mode: str = "simple",
        end_x: float = 0.0,
        end_y: float = 0.0,
        start_x: float = 0.0,
        start_y: float = 0.0,
        mid_x: float = 0.0,
        mid_y: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a line.

        Modes:
            simple   — from (start_x, start_y) to (end_x, end_y)
            midpoint — centered at (mid_x, mid_y), ends at (end_x, end_y)

        Examples:
            # Simple line
            skill_line(start_x=0, start_y=0, end_x=10, end_y=5)

            # Midpoint line
            skill_line(mode="midpoint", mid_x=5, mid_y=5, end_x=10, end_y=5)
        """
        return _skill_line(
            provider,
            mode=mode,
            end_x=end_x,
            end_y=end_y,
            start_x=start_x,
            start_y=start_y,
            mid_x=mid_x,
            mid_y=mid_y,
        )

    @mcp_instance.tool()
    def skill_circle(
        mode: str = "center",
        cx: float = 0.0,
        cy: float = 0.0,
        radius: float = 1.0,
        x1: float = 0.0,
        y1: float = 0.0,
        x2: float = 0.0,
        y2: float = 0.0,
        x3: float = 0.0,
        y3: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a circle.

        Modes:
            center — center point + radius (cx, cy, radius)
            3point — three perimeter points (x1,y1, x2,y2, x3,y3)

        Examples:
            # Center-radius circle
            skill_circle(cx=5, cy=5, radius=3)

            # 3-point circle
            skill_circle(mode="3point", x1=0, y1=0, x2=10, y2=10, x3=20, y3=0)
        """
        return _skill_circle(
            provider,
            mode=mode,
            cx=cx,
            cy=cy,
            radius=radius,
            x1=x1,
            y1=y1,
            x2=x2,
            y2=y2,
            x3=x3,
            y3=y3,
        )
