"""TUI menu application for mcp-cad installer.

Uses ``prompt_toolkit`` for an interactive terminal UI with checkbox-based
agent selection and keyboard navigation.

Key bindings
------------
- **Enter** — confirm selection / run selected items
- **Space** — toggle checkbox on the highlighted item
- **Esc** / **q** — quit the application
- **Up** / **Down** — navigate menu items (wrapping)

Usage::

    from scripts.tui.app import TUIMenu
    from scripts.tui.state import State

    state = State.from_file("scripts/tui/state.json")
    menu = TUIMenu(state, state_path="scripts/tui/state.json")
    menu.run()
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import TYPE_CHECKING

from prompt_toolkit import Application
from prompt_toolkit.key_binding import KeyBindings
from prompt_toolkit.layout import HSplit, Layout, Window
from prompt_toolkit.layout.controls import FormattedTextControl
from prompt_toolkit.layout.dimension import Dimension
from prompt_toolkit.styles import Style

if TYPE_CHECKING:
    from scripts.tui.state import State

log = logging.getLogger(__name__)


class TUIMenu:
    """Interactive TUI menu for mcp-cad agent registration.

    Displays a checkbox list of registered :class:`MenuItem` entries and
    provides keyboard navigation.  After running selected items, state is
    saved to disk if anything changed.

    Parameters
    ----------
    state:
        Persisted user preferences loaded on startup.
    state_path:
        Path to save state back to.  If ``None``, state is not persisted.
    """

    def __init__(
        self,
        state: State,
        state_path: Path | str | None = None,
    ) -> None:
        self.state = state
        self.state_path = Path(state_path) if state_path else None
        self.items: list = []
        self.selected_index: int = 0
        self.checked: set[str] = set()
        self.error_msg: str | None = None
        self.status_msg: str | None = None
        self._dirty: bool = False
        self._app: Application | None = None

        self._load_items()
        self._preselect_last_agent()

    # ------------------------------------------------------------------
    # Initialization helpers
    # ------------------------------------------------------------------

    def _load_items(self) -> None:
        """Load registered items from the global registry."""
        from scripts.tui.registry import get_all

        self.items = get_all()

    def _preselect_last_agent(self) -> None:
        """Pre-select the last agent from saved state."""
        last = self.state.get_last_agent()
        if last:
            for item in self.items:
                if getattr(item, "name", None) == last:
                    self.checked.add(item.name)
                    break

    # ------------------------------------------------------------------
    # Dynamic text generators for prompt_toolkit controls
    # ------------------------------------------------------------------

    def _title_text(self) -> list[tuple[str, str]]:
        """Formatted text for the application title bar."""
        return [("class:title", " mcp-cad TUI Installer ")]

    def _menu_text(self) -> list[tuple[str, str]]:
        """Formatted text for the menu items list."""
        if not self.items:
            return [("class:disabled", "  No items registered yet.")]

        lines: list[tuple[str, str]] = []
        for i, item in enumerate(self.items):
            name = getattr(item, "name", str(item))
            desc = getattr(item, "description", "")
            enabled = getattr(item, "is_enabled", True)

            check = "\u2713" if name in self.checked else " "   # ✓
            if i == self.selected_index:
                cursor = "\u2192"                                 # →
            else:
                cursor = " "

            if not enabled:
                style = "class:disabled"
            elif i == self.selected_index:
                style = "class:selected"
            else:
                style = ""

            lines.append((style, f" {cursor} [{check}] {name}  \u2014  {desc}\n"))

        return lines

    def _status_text(self) -> list[tuple[str, str]]:
        """Formatted text for status or error messages."""
        if self.error_msg:
            return [("class:error", f" \u2717 {self.error_msg}")]
        if self.status_msg:
            return [("class:status", f" \u2713 {self.status_msg}")]
        return [("", "")]

    def _footer_text(self) -> list[tuple[str, str]]:
        """Formatted text for the key-binding footer."""
        return [
            (
                "class:footer",
                " [Enter] Confirm  [Space] Toggle  [\u2191\u2193] Navigate  [Esc/q] Quit ",
            ),
        ]

    # ------------------------------------------------------------------
    # Layout and application setup
    # ------------------------------------------------------------------

    def _build_layout(self) -> Layout:
        """Assemble the prompt_toolkit layout from dynamic controls."""
        body = HSplit(
            [
                Window(
                    content=FormattedTextControl(text=self._title_text),
                    height=Dimension(min=1, max=1),
                ),
                Window(height=1),  # blank separator
                Window(
                    content=FormattedTextControl(text=self._menu_text),
                ),
                Window(
                    content=FormattedTextControl(text=self._status_text),
                    height=Dimension(min=1, max=1),
                ),
                Window(
                    content=FormattedTextControl(text=self._footer_text),
                    height=Dimension(min=1, max=1),
                ),
            ]
        )
        return Layout(body)

    def _build_keybindings(self) -> KeyBindings:
        """Configure keyboard shortcuts."""
        kb = KeyBindings()

        @kb.add("up")
        def _navigate_up(event: object) -> None:
            if self.items:
                self.selected_index = (self.selected_index - 1) % len(self.items)
                self._clear_messages()
                event.app.invalidate()

        @kb.add("down")
        def _navigate_down(event: object) -> None:
            if self.items:
                self.selected_index = (self.selected_index + 1) % len(self.items)
                self._clear_messages()
                event.app.invalidate()

        @kb.add("space")
        def _toggle_check(event: object) -> None:
            if self.items and 0 <= self.selected_index < len(self.items):
                item = self.items[self.selected_index]
                name = getattr(item, "name", None)
                if name is not None:
                    if name in self.checked:
                        self.checked.discard(name)
                    else:
                        self.checked.add(name)
                    self._dirty = True
            event.app.invalidate()

        @kb.add("enter")
        def _confirm(event: object) -> None:
            self._run_selected()
            event.app.invalidate()

        @kb.add("escape")
        @kb.add("q")
        def _quit(event: object) -> None:
            self._save_if_dirty()
            event.app.exit()

        return kb

    @staticmethod
    def _build_style() -> Style:
        """Define the visual style for the TUI."""
        return Style.from_dict(
            {
                "title": "bold fg:white bg:ansiblue",
                "selected": "fg:ansicyan bold",
                "disabled": "fg:ansigray italic",
                "error": "fg:ansiwhite bg:ansired",
                "status": "fg:ansigreen",
                "footer": "fg:ansiyellow",
            }
        )

    # ------------------------------------------------------------------
    # Actions
    # ------------------------------------------------------------------

    def _clear_messages(self) -> None:
        """Clear error and status messages on navigation."""
        self.error_msg = None
        self.status_msg = None

    def _run_selected(self) -> None:
        """Execute all checked items that are enabled.

        Stops on the first error and displays the error message.
        Updates :attr:`state.last_agent` on success.
        """
        if not self.checked:
            self.status_msg = "No items selected. Use [Space] to toggle."
            return

        for name in sorted(self.checked):
            item = next(
                (i for i in self.items if getattr(i, "name", None) == name),
                None,
            )
            if item is None:
                continue

            if not getattr(item, "is_enabled", True):
                self.status_msg = f"Skipped {name} (disabled)."
                continue

            try:
                item.run()
                self.status_msg = f"Done: {name}"
                self.state.last_agent = name
                self._dirty = True
            except Exception as exc:
                self.error_msg = f"{name}: {exc}"
                log.error("Item %s failed: %s", name, exc)
                return  # stop on first error

    def _save_if_dirty(self) -> None:
        """Persist state to disk if it has changed since load."""
        if self._dirty and self.state_path:
            try:
                self.state.save(self.state_path)
            except OSError:
                log.warning("Failed to save state to %s", self.state_path)

    # ------------------------------------------------------------------
    # Public entry point
    # ------------------------------------------------------------------

    def run(self) -> None:
        """Build and run the interactive TUI application.

        On exit, state is saved to disk if any selections were made.
        """
        self._app = Application(
            layout=self._build_layout(),
            key_bindings=self._build_keybindings(),
            style=self._build_style(),
            full_screen=False,
            mouse_support=False,
        )
        try:
            self._app.run()
        finally:
            self._save_if_dirty()