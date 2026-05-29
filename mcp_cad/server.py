"""FastMCP server instance and tool registration for Inventor MCP.

The server creates a ``CADProvider`` via the adapter factory and registers
all tools and skills using generic registration functions.  This module has
**no** direct imports from Inventor manager classes — all CAD operations flow
through the ``CADProvider`` protocol.
"""

from __future__ import annotations

import logging
import sys
from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.providers.inventor.adapter import create_inventor_provider
from mcp_cad.skills import register_skills
from mcp_cad.tools import register_tools

log = logging.getLogger(__name__)

__version__ = "0.1.0"


def main() -> None:
    """Entry point: create provider, register tools & skills, run server.

    Supports ``--version`` flag for cross-client diagnostics.
    Errors during startup are logged to stderr so MCP clients (VS Code, Pi,
    OpenCode) can surface them instead of silently reporting "Connection closed".
    """
    if "--version" in sys.argv or "-V" in sys.argv:
        print(f"mcp-cad {__version__}")
        return

    try:
        mcp = FastMCP("mcp-cad")

        # Create the Inventor-backed provider via the adapter factory.
        # The factory instantiates RealInventorDriver and all manager instances
        # internally — server.py never touches individual managers.
        provider = create_inventor_provider()

        # Register all 32 atomic MCP tools (connection, doc, sketch, feature,
        # param, property, export).
        register_tools(mcp, provider)

        # Register higher-level skill tools (e.g. drilling pattern).
        register_skills(mcp, provider)

        mcp.run(transport="stdio")
    except Exception as exc:
        # Log the full traceback to stderr so the MCP client can capture it.
        # Without this, the process dies silently and the client only sees
        # "-32000: Connection closed".
        print(f"mcp-cad: startup failed: {exc}", file=sys.stderr)
        import traceback
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)