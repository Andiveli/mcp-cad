"""Entry point for the TUI installer.

Run with::

    python -m scripts.tui

Loads all registered menu items, restores persisted state, builds the
interactive menu, and runs the application.
"""

from __future__ import annotations

import logging
from pathlib import Path

from scripts.tui.app import TUIMenu

# Import the items package to trigger @register decorators on all items.
import scripts.tui.items  # noqa: F401

from scripts.tui.state import State

log = logging.getLogger(__name__)

# Default state file sits beside the scripts/tui package.
_STATE_PATH = Path(__file__).parent / "state.json"


def main() -> None:
    """Build and run the TUI installer."""
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")

    state = State.from_file(_STATE_PATH)
    menu = TUIMenu(state, state_path=_STATE_PATH)

    log.info("Starting mcp-cad TUI installer")
    try:
        menu.run()
    except KeyboardInterrupt:
        log.info("Interrupted — exiting")
    finally:
        log.info("Installer finished")


if __name__ == "__main__":
    main()