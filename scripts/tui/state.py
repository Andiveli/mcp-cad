"""State persistence for the TUI installer.

Remembers user selections across runs: last-run agent, custom paths,
and preferences.  State is stored as JSON and written atomically to
prevent corruption on crash.

Usage::

    from scripts.tui.state import State

    state = State.from_file("scripts/tui/state.json")
    state.last_agent = "opencode"
    state.save("scripts/tui/state.json")
"""

from __future__ import annotations

import json
import os
from dataclasses import asdict, dataclass, field
from pathlib import Path


@dataclass
class State:
    """Persisted state for the TUI installer.

    Attributes
    ----------
    last_agent:
        Name of the last successfully run agent item.
    custom_paths:
        User-defined override paths keyed by agent name
        (e.g. ``{"pi": "C:\\custom\\path\\settings.json"}``).
    preferences:
        Reserved for future UI preferences.
    """

    last_agent: str = ""
    custom_paths: dict = field(default_factory=dict)
    preferences: dict = field(default_factory=dict)

    # ------------------------------------------------------------------
    # Persistence
    # ------------------------------------------------------------------

    @classmethod
    def from_file(cls, path: str | Path) -> State:
        """Load state from a JSON file.

        Returns a default :class:`State` if the file is missing or
        contains malformed JSON.  Partial schemas are accepted — missing
        fields are filled with defaults.
        """
        p = Path(path)
        if not p.exists():
            return cls()
        try:
            data = json.loads(p.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return cls()

        if not isinstance(data, dict):
            return cls()

        return cls(
            last_agent=data.get("last_agent", ""),
            custom_paths=data.get("custom_paths", {}),
            preferences=data.get("preferences", {}),
        )

    def save(self, path: str | Path) -> None:
        """Persist state to a JSON file using atomic write.

        Creates parent directories if they do not exist.
        On failure the temp file is removed, leaving the original intact.
        """
        p = Path(path)
        p.parent.mkdir(parents=True, exist_ok=True)
        data = asdict(self)
        tmp = p.with_suffix(".tmp")
        try:
            tmp.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
            os.replace(tmp, p)
        except BaseException:
            if tmp.exists():
                tmp.unlink()
            raise

    # ------------------------------------------------------------------
    # Convenience accessors
    # ------------------------------------------------------------------

    def get_last_agent(self) -> str:
        """Return the name of the last agent that completed successfully."""
        return self.last_agent

    def set_preference(self, key: str, value: str) -> None:
        """Update a preference entry."""
        self.preferences[key] = value