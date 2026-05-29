"""Menu items for the TUI installer.

Each agent registration task is a self-registering module.
Import all item modules here to trigger their @register decorators.
"""

from scripts.tui.items.claude import _ClaudeItem  # noqa: F401
from scripts.tui.items.opencode import _OpenCodeItem  # noqa: F401
from scripts.tui.items.pi import _PiItem  # noqa: F401
from scripts.tui.items.vscode import _VSCodeItem  # noqa: F401