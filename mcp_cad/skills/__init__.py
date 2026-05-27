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
