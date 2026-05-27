"""Skills package — composable CAD operations built on the CADProvider protocol.

Provides ``register_skills(mcp_instance, provider)`` which registers all
skill-based MCP tools.  Skills compose atomic provider operations into
higher-level workflows (e.g. drilling patterns, bracket generation).
"""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.core.protocol import CADProvider
from mcp_cad.skills.drilling import (
    crear_patron_taladros as _skill_patron_taladros,
)


def register_skills(mcp_instance: FastMCP, provider: CADProvider) -> None:
    """Register all skill-based MCP tools on the FastMCP instance.

    Skills are higher-level operations that compose atomic provider calls.
    They are registered AFTER ``register_tools()`` in server.py.

    Parameters
    ----------
    mcp_instance:
        The FastMCP server instance.
    provider:
        A CADProvider implementation (e.g. InventorProvider).
    """

    @mcp_instance.tool()
    def crear_patron_taladros(
        diametro: float,
        profundidad: float,
        espaciado: float,
        cantidad: int,
        x_centro: float = 0,
        y_centro: float = 0,
    ) -> dict[str, Any]:
        """Create a linear pattern of drilled holes.

        Creates a sketch on XY plane, draws evenly spaced circles,
        and extrude-cuts them to the specified depth.

        Args:
            diametro: Hole diameter (cm).
            profundidad: Cut depth (cm).
            espaciado: Spacing between hole centers (cm).
            cantidad: Number of holes in the pattern.
            x_centro: Pattern origin X offset (cm).
            y_centro: Pattern origin Y offset (cm).
        """
        return _skill_patron_taladros(
            provider,
            diametro,
            profundidad,
            espaciado,
            cantidad,
            x_centro,
            y_centro,
        )