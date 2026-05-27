"""Claude Desktop agent registration item.

Registers mcp-cad in Claude Desktop's ``claude_desktop_config.json``.
"""

from __future__ import annotations

import sys
from typing import Callable

from scripts.tui.items.base import MenuItem
from scripts.tui.registry import register


@register(name="claude", description="Register mcp-cad in Claude Desktop config", category="agent")
class _ClaudeItem(MenuItem):
    """Register mcp-cad in Claude Desktop config."""

    def __init__(self, register_fn: Callable[[], str] | None = None) -> None:
        self._register_fn = register_fn

    @property
    def name(self) -> str:  # noqa: D102
        return "claude"

    @property
    def description(self) -> str:  # noqa: D102
        return "Register mcp-cad in Claude Desktop config"

    @property
    def is_enabled(self) -> bool:  # noqa: D102
        return True

    def run(self) -> str:
        """Register mcp-cad in Claude Desktop config.

        Uses the provided *register_fn* (for testing) or falls back to
        :func:`~scripts.tui.install_logic.register_claude` with
        ``sys.executable``.
        """
        if self._register_fn is not None:
            return self._register_fn()
        from scripts.tui import install_logic

        return install_logic.register_claude(sys.executable)