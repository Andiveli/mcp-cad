"""VS Code agent registration item.

Registers mcp-cad in the workspace ``.vscode/mcp.json`` so that
the GitHub Copilot Chat agent picks it up automatically.
"""

from __future__ import annotations

import sys
from typing import Callable

from scripts.tui.items.base import MenuItem
from scripts.tui.registry import register


@register(name="vscode", description="Register mcp-cad in .vscode/mcp.json (Copilot Chat)", category="agent")
class _VSCodeItem(MenuItem):
    """Register mcp-cad in the workspace ``.vscode/mcp.json``."""

    def __init__(self, register_fn: Callable[[], str] | None = None) -> None:
        self._register_fn = register_fn

    @property
    def name(self) -> str:  # noqa: D102
        return "vscode"

    @property
    def description(self) -> str:  # noqa: D102
        return "Register mcp-cad in .vscode/mcp.json (Copilot Chat)"

    @property
    def is_enabled(self) -> bool:  # noqa: D102
        return True

    def run(self) -> str:
        """Register mcp-cad in VS Code workspace config.

        Uses the provided *register_fn* (for testing) or falls back to
        :func:`~scripts.tui.install_logic.register_vscode` with the
        current working directory and ``sys.executable``.
        """
        if self._register_fn is not None:
            return self._register_fn()
        from scripts.tui import install_logic

        return install_logic.register_vscode(".", sys.executable)
