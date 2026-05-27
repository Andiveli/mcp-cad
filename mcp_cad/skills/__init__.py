"""Skills package — composable CAD operations built on the CADProvider protocol.

Provides ``register_skills(mcp_instance, provider)`` which registers all
skill-based MCP tools.  Skills compose atomic provider operations into
higher-level workflows.

Add new skills by creating a module in this package and registering its
tool(s) in ``register_skills()`` below.
"""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.core.protocol import CADProvider


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
    # Register each skill tool here as:
    #
    #     @mcp_instance.tool()
    #     def skill_name(...) -> dict[str, Any]:
    #         ...

    pass  # No skills registered yet — add new skills below
