"""Tests for :mod:`scripts.tui.state`.

Verifies :class:`State` persistence: ``from_file``, ``save``, preference
management, and edge cases with missing/malformed files.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from scripts.tui.state import State


# ------------------------------------------------------------------
# from_file
# ------------------------------------------------------------------


class TestStateFromFile:
    """Tests for :meth:`State.from_file`."""

    def test_returns_default_when_file_missing(self, tmp_path: Path) -> None:
        """Missing state file produces a default empty state."""
        state = State.from_file(tmp_path / "missing.json")
        assert state.last_agent == ""
        assert state.custom_paths == {}
        assert state.preferences == {}

    def test_loads_valid_state(self, tmp_path: Path) -> None:
        """Valid JSON file is loaded into a State instance."""
        path = tmp_path / "state.json"
        path.write_text(
            json.dumps({
                "last_agent": "opencode",
                "custom_paths": {"pi": "/custom/path"},
                "preferences": {"theme": "dark"},
            }),
            encoding="utf-8",
        )

        state = State.from_file(path)
        assert state.last_agent == "opencode"
        assert state.custom_paths == {"pi": "/custom/path"}
        assert state.preferences == {"theme": "dark"}

    def test_handles_malformed_json(self, tmp_path: Path) -> None:
        """Malformed JSON returns a default state without error."""
        path = tmp_path / "bad.json"
        path.write_text("{not valid json", encoding="utf-8")

        state = State.from_file(path)
        assert state.last_agent == ""

    def test_handles_non_dict_json(self, tmp_path: Path) -> None:
        """A JSON value that is not a dict returns a default state."""
        path = tmp_path / "array.json"
        path.write_text("[1, 2, 3]", encoding="utf-8")

        state = State.from_file(path)
        assert state.last_agent == ""

    def test_fills_missing_fields_with_defaults(self, tmp_path: Path) -> None:
        """Partial state files fill missing keys with defaults."""
        path = tmp_path / "partial.json"
        path.write_text(
            json.dumps({"last_agent": "claude"}),
            encoding="utf-8",
        )

        state = State.from_file(path)
        assert state.last_agent == "claude"
        assert state.custom_paths == {}
        assert state.preferences == {}

    def test_handles_empty_file(self, tmp_path: Path) -> None:
        """Empty file returns a default state."""
        path = tmp_path / "empty.json"
        path.write_text("", encoding="utf-8")

        state = State.from_file(path)
        assert state.last_agent == ""


# ------------------------------------------------------------------
# save
# ------------------------------------------------------------------


class TestStateSave:
    """Tests for :meth:`State.save`."""

    def test_round_trip(self, tmp_path: Path) -> None:
        """Save followed by from_file restores identical state."""
        path = tmp_path / "state.json"
        original = State(
            last_agent="pi",
            custom_paths={"pi": "/some/path"},
            preferences={"auto": "true"},
        )
        original.save(path)

        loaded = State.from_file(path)
        assert loaded.last_agent == "pi"
        assert loaded.custom_paths == {"pi": "/some/path"}
        assert loaded.preferences == {"auto": "true"}

    def test_creates_parent_directories(self, tmp_path: Path) -> None:
        """Missing parent directories are created automatically."""
        path = tmp_path / "deep" / "nested" / "state.json"
        State().save(path)
        assert path.exists()

    def test_atomic_write_replaces_existing(self, tmp_path: Path) -> None:
        """Saving over an existing file replaces it atomically."""
        path = tmp_path / "state.json"
        State(last_agent="first").save(path)
        State(last_agent="second").save(path)

        loaded = State.from_file(path)
        assert loaded.last_agent == "second"


# ------------------------------------------------------------------
# Convenience accessors
# ------------------------------------------------------------------


class TestStateAccessors:
    """Tests for :meth:`get_last_agent` and :meth:`set_preference`."""

    def test_get_last_agent(self) -> None:
        """Returns the last agent string."""
        state = State(last_agent="opencode")
        assert state.get_last_agent() == "opencode"

    def test_get_last_agent_default(self) -> None:
        """Default last_agent is an empty string."""
        state = State()
        assert state.get_last_agent() == ""

    def test_set_preference(self) -> None:
        """Preferences can be set and retrieved."""
        state = State()
        state.set_preference("theme", "dark")
        assert state.preferences["theme"] == "dark"

    def test_set_preference_overwrites(self) -> None:
        """Setting the same key overwrites the previous value."""
        state = State()
        state.set_preference("theme", "light")
        state.set_preference("theme", "dark")
        assert state.preferences["theme"] == "dark"