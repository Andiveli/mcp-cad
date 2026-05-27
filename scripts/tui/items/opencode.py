"""OpenCode agent registration item.

Registers mcp-cad in the project's ``opencode.json`` so that the
OpenCode agent picks it up automatically.
"""

from __future__ import annotations

import sys
from typing import Callable

from scripts.tui.items.base import MenuItem
from scripts.tui.registry import register


@register(name="opencode", description="Register mcp-cad in opencode.json", category="agent")
class _OpenCodeItem(MenuItem):
    """Register mcp-cad in the project's ``opencode.json``."""

    def __init__(self, register_fn: Callable[[], str] | None = None) -> None:
        self._register_fn = register_fn

    @property
    def name(self) -> str:  # noqa: D102
        return "opencode"

    @property
    def description(self) -> str:  # noqa: D102
        return "Register mcp-cad in opencode.json"

    @property
    def is_enabled(self) -> bool:  # noqa: D102
        return True

    def run(self) -> str:
        """Register mcp-cad in OpenCode config.

        Uses the provided *register_fn* (for testing) or falls back to
        :func:`~scripts.tui.install_logic.register_opencode` with the
        current working directory and ``sys.executable``.
        """
        if self._register_fn is not None:
            return self._register_fn()
        from scripts.tui import install_logic

        return install_logic.register_opencode(".", sys.executable)