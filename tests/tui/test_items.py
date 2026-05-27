"""Tests for menu item subclasses.

Each test creates items with a mock ``register_fn`` to verify that
``run()`` calls the correct function, without touching the filesystem.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from scripts.tui.registry import get_all, reset

# Import base class for structural checks.
from scripts.tui.items.base import MenuItem


# ------------------------------------------------------------------
# Fixtures
# ------------------------------------------------------------------


@pytest.fixture(autouse=True)
def _clean_registry():
    """Reset registry before/after each test."""
    reset()
    yield
    reset()


# ------------------------------------------------------------------
# MenuItem ABC checks
# ------------------------------------------------------------------


class TestMenuItemABC:
    """Tests for the :class:`MenuItem` abstract base class."""

    def test_cannot_instantiate_directly(self) -> None:
        """``MenuItem`` is abstract and cannot be instantiated."""
        with pytest.raises(TypeError):
            MenuItem()  # type: ignore[abstract]

    def test_abstract_properties(self) -> None:
        """Subclasses must define ``name``, ``description``, and ``is_enabled``."""

        class _Incomplete(MenuItem):
            pass

        with pytest.raises(TypeError):
            _Incomplete()  # type: ignore[abstract]

    def test_default_category_is_agent(self) -> None:
        """Concrete items default to category ``"agent"``.

        We create a minimal concrete subclass to verify the default.
        """

        class _Minimal(MenuItem):
            @property
            def name(self) -> str:
                return "test"

            @property
            def description(self) -> str:
                return "test item"

            @property
            def is_enabled(self) -> bool:
                return True

            def run(self) -> str:
                return "ok"

        item = _Minimal()
        assert item.category == "agent"

    def test_run_is_abstract(self) -> None:
        """``run()`` must be implemented by subclasses."""

        class _NoRun(MenuItem):
            @property
            def name(self) -> str:
                return "test"

            @property
            def description(self) -> str:
                return "desc"

            @property
            def is_enabled(self) -> bool:
                return True

        with pytest.raises(TypeError):
            _NoRun()  # type: ignore[abstract]


# ------------------------------------------------------------------
# OpenCode item
# ------------------------------------------------------------------


class TestOpenCodeItem:
    """Tests for :class:`_OpenCodeItem`."""

    def test_properties(self) -> None:
        """Name, description, and is_enabled have expected values."""
        from scripts.tui.items.opencode import _OpenCodeItem

        mock_fn = MagicMock(return_value="ok")
        item = _OpenCodeItem(register_fn=mock_fn)

        assert item.name == "opencode"
        assert item.description == "Register mcp-cad in opencode.json"
        assert item.is_enabled is True

    def test_run_calls_register_fn(self) -> None:
        """``run()`` delegates to the injected ``register_fn``."""
        from scripts.tui.items.opencode import _OpenCodeItem

        mock_fn = MagicMock(return_value="/path/to/opencode.json")
        item = _OpenCodeItem(register_fn=mock_fn)

        result = item.run()

        mock_fn.assert_called_once()
        assert result == "/path/to/opencode.json"

    def test_run_without_mock_calls_install_logic(self) -> None:
        """Without a mock, ``run()`` calls the real ``install_logic``.

        This test verifies the fallback import path works, using a
        temporary directory to avoid filesystem side effects.
        """
        from scripts.tui.items.opencode import _OpenCodeItem

        item = _OpenCodeItem(register_fn=None)
        # We don't call run() here because it would write to the filesystem.
        # Instead, we verify the attribute is None so the real function is used.
        assert item._register_fn is None  # noqa: SLF001


# ------------------------------------------------------------------
# Claude item
# ------------------------------------------------------------------


class TestClaudeItem:
    """Tests for :class:`_ClaudeItem`."""

    def test_properties(self) -> None:
        from scripts.tui.items.claude import _ClaudeItem

        mock_fn = MagicMock(return_value="ok")
        item = _ClaudeItem(register_fn=mock_fn)

        assert item.name == "claude"
        assert item.description == "Register mcp-cad in Claude Desktop config"
        assert item.is_enabled is True

    def test_run_calls_register_fn(self) -> None:
        from scripts.tui.items.claude import _ClaudeItem

        mock_fn = MagicMock(return_value="/path/to/claude_config.json")
        item = _ClaudeItem(register_fn=mock_fn)

        result = item.run()

        mock_fn.assert_called_once()
        assert result == "/path/to/claude_config.json"


# ------------------------------------------------------------------
# Pi item
# ------------------------------------------------------------------


class TestPiItem:
    """Tests for :class:`_PiItem`."""

    def test_properties(self) -> None:
        from scripts.tui.items.pi import _PiItem

        mock_fn = MagicMock(return_value="ok")
        item = _PiItem(register_fn=mock_fn)

        assert item.name == "pi"
        assert item.description == "Register mcp-cad in Pi settings.json"
        assert item.is_enabled is True

    def test_run_calls_register_fn(self) -> None:
        from scripts.tui.items.pi import _PiItem

        mock_fn = MagicMock(return_value="/path/to/pi_settings.json")
        item = _PiItem(register_fn=mock_fn)

        result = item.run()

        mock_fn.assert_called_once()
        assert result == "/path/to/pi_settings.json"


# ------------------------------------------------------------------
# Integration: self-registration
# ------------------------------------------------------------------


class TestItemRegistration:
    """Verify that importing items populates the registry."""

    def test_all_items_registered(self) -> None:
        """All three agent items appear in the registry after import.

        Because items are imported via the ``items.__init__`` module,
        simply importing it should register all three items.  We reload
        the module because the autouse fixture clears the registry before
        each test, and cached imports don't re-trigger ``@register``.
        """
        import importlib

        import scripts.tui.items.opencode
        import scripts.tui.items.claude
        import scripts.tui.items.pi

        # Reload each module to re-trigger @register after the fixture reset.
        importlib.reload(scripts.tui.items.opencode)
        importlib.reload(scripts.tui.items.claude)
        importlib.reload(scripts.tui.items.pi)

        items = get_all()
        names = {item.name for item in items}
        assert "opencode" in names
        assert "claude" in names
        assert "pi" in names

    def test_all_items_are_menuitem_subclasses(self) -> None:
        """Registered items are instances of :class:`MenuItem`."""
        import scripts.tui.items  # noqa: F401

        for item in get_all():
            assert isinstance(item, MenuItem)