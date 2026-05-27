"""Tests for :mod:`scripts.tui.install_logic`.

Verifies atomic JSON read/write, deep merge, and per-agent registration
using ``tmp_path`` fixtures.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

from scripts.tui.install_logic import (
    deep_merge,
    merge_entry,
    read_config,
    register_claude,
    register_opencode,
    register_pi,
    write_config,
)


# ------------------------------------------------------------------
# Low-level read / write
# ------------------------------------------------------------------


class TestReadConfig:
    """Tests for :func:`read_config`."""

    def test_returns_none_for_missing_file(self, tmp_path: Path) -> None:
        """Missing files return ``None`` instead of raising."""
        result = read_config(tmp_path / "nonexistent.json")
        assert result is None

    def test_reads_valid_json(self, tmp_path: Path) -> None:
        """Existing JSON files are parsed and returned."""
        path = tmp_path / "config.json"
        path.write_text('{"key": "value"}', encoding="utf-8")
        result = read_config(path)
        assert result == {"key": "value"}

    def test_raises_on_malformed_json(self, tmp_path: Path) -> None:
        """Malformed JSON files raise ``json.JSONDecodeError``."""
        path = tmp_path / "bad.json"
        path.write_text("{invalid", encoding="utf-8")
        with pytest.raises(json.JSONDecodeError):
            read_config(path)


class TestWriteConfig:
    """Tests for :func:`write_config`."""

    def test_creates_parent_dirs(self, tmp_path: Path) -> None:
        """Parent directories are created if they do not exist."""
        path = tmp_path / "nested" / "dir" / "config.json"
        write_config(path, {"created": True})
        assert path.exists()
        assert json.loads(path.read_text(encoding="utf-8")) == {"created": True}

    def test_atomic_write(self, tmp_path: Path) -> None:
        """Write is atomic — original is replaced only on success."""
        path = tmp_path / "atomic.json"
        path.write_text('{"original": true}', encoding="utf-8")

        write_config(path, {"updated": True})

        result = json.loads(path.read_text(encoding="utf-8"))
        assert result == {"updated": True}

    @pytest.mark.skipif(
        sys.platform == "win32",
        reason="Windows ACLs do not enforce chmod write restrictions for admin users",
    )
    def test_no_tmp_file_left_on_error_posix(self, tmp_path: Path) -> None:
        """If writing fails, no temporary file is left behind (POSIX)."""
        import os
        import stat

        readonly_dir = tmp_path / "readonly"
        readonly_dir.mkdir()
        path = readonly_dir / "config.json"

        os.chmod(readonly_dir, stat.S_IRUSR | stat.S_IXUSR)

        try:
            with pytest.raises(OSError):
                write_config(path, {"should": "fail"})
            tmp_files = list(readonly_dir.glob("*.tmp"))
            assert len(tmp_files) == 0
        finally:
            os.chmod(readonly_dir, stat.S_IRWXU)

    def test_atomic_write_cleanup_on_exception(self, tmp_path: Path) -> None:
        """The temp file is removed even when write_config raises."""
        path = tmp_path / "config.json"
        # Write a valid file first
        path.write_text('{"original": true}', encoding="utf-8")

        # Force a failure by monkeypatching json.dump to raise
        original_dump = json.dump

        def _failing_dump(*args, **kwargs):
            raise RuntimeError("simulated failure")

        import unittest.mock

        with unittest.mock.patch("scripts.tui.install_logic.json.dump", side_effect=RuntimeError("boom")):
            with pytest.raises(RuntimeError, match="boom"):
                write_config(path, {"should": "fail"})

        # Original file still intact
        assert json.loads(path.read_text(encoding="utf-8")) == {"original": True}
        # No tmp file left
        tmp_files = list(tmp_path.glob("*.tmp"))
        assert len(tmp_files) == 0


# ------------------------------------------------------------------
# Deep merge
# ------------------------------------------------------------------


class TestDeepMerge:
    """Tests for :func:`deep_merge`."""

    def test_shallow_merge(self) -> None:
        """Top-level keys are merged with override winning."""
        result = deep_merge({"a": 1, "b": 2}, {"b": 3, "c": 4})
        assert result == {"a": 1, "b": 3, "c": 4}

    def test_nested_dict_merge(self) -> None:
        """Nested dicts are merged recursively."""
        result = deep_merge(
            {"mcpServers": {"mcp-cad": {"command": "old"}}},
            {"mcpServers": {"mcp-cad": {"command": "new", "args": ["-m", "mcp_cad"]}}},
        )
        expected = {
            "mcpServers": {
                "mcp-cad": {"command": "new", "args": ["-m", "mcp_cad"]},
            },
        }
        assert result == expected

    def test_does_not_mutate_base(self) -> None:
        """Original dicts are not mutated."""
        base = {"a": {"b": 1}}
        override = {"a": {"c": 2}}
        result = deep_merge(base, override)
        assert result == {"a": {"b": 1, "c": 2}}
        assert base == {"a": {"b": 1}}  # unchanged

    def test_list_override(self) -> None:
        """Lists in the override replace the base entirely."""
        result = deep_merge({"items": [1, 2]}, {"items": [3, 4]})
        assert result == {"items": [3, 4]}


class TestMergeEntry:
    """Tests for :func:`merge_entry`."""

    def test_merge_dict_entry(self) -> None:
        """Dict entries are deep-merged under the key."""
        config = {"mcpServers": {"existing": {"command": "foo"}}}
        entry = {"mcp-cad": {"command": "python"}}
        result = merge_entry(config, "mcpServers", entry)
        assert "existing" in result["mcpServers"]
        assert "mcp-cad" in result["mcpServers"]

    def test_new_key_is_added(self) -> None:
        """New keys are simply inserted."""
        result = merge_entry({}, "mcpServers", {"mcp-cad": {"command": "py"}})
        assert result == {"mcpServers": {"mcp-cad": {"command": "py"}}}

    def test_original_not_mutated(self) -> None:
        """The original config dict is not mutated."""
        config = {"mcpServers": {"old": True}}
        result = merge_entry(config, "mcpServers", {"new": True})
        assert "new" not in config["mcpServers"]
        assert "new" in result["mcpServers"]


# ------------------------------------------------------------------
# Per-agent registration
# ------------------------------------------------------------------


class TestRegisterOpenCode:
    """Tests for :func:`register_opencode`."""

    def test_creates_config_from_scratch(self, tmp_path: Path) -> None:
        """Creates opencode.json in an empty project directory."""
        python_exe = r"C:\venv\Scripts\python.exe"
        result = register_opencode(str(tmp_path), python_exe)

        config = json.loads(Path(result).read_text(encoding="utf-8"))
        assert "mcp" in config
        assert "mcp-cad" in config["mcp"]
        assert config["mcp"]["mcp-cad"]["command"] == [python_exe, "-m", "mcp_cad"]

    def test_merges_into_existing_config(self, tmp_path: Path) -> None:
        """Merges mcp-cad entry into an existing opencode.json."""
        existing = {"mcp": {"other-server": {"type": "local"}}, "$schema": "old"}
        config_path = tmp_path / "opencode.json"
        config_path.write_text(json.dumps(existing), encoding="utf-8")

        register_opencode(str(tmp_path), "/usr/bin/python3")

        config = json.loads(config_path.read_text(encoding="utf-8"))
        assert "other-server" in config["mcp"]
        assert "mcp-cad" in config["mcp"]


class TestRegisterClaude:
    """Tests for :func:`register_claude`."""

    def test_creates_config_in_appdata(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        """Creates claude_desktop_config.json in %APPDATA%/Claude/."""
        monkeypatch.setenv("APPDATA", str(tmp_path))

        result = register_claude(r"C:\venv\Scripts\python.exe")

        config_path = Path(result)
        assert config_path.exists()
        config = json.loads(config_path.read_text(encoding="utf-8"))
        assert "mcpServers" in config
        assert "mcp-cad" in config["mcpServers"]

    def test_merges_with_existing(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        """Preserves existing mcpServers when merging."""
        monkeypatch.setenv("APPDATA", str(tmp_path))
        config_dir = tmp_path / "Claude"
        config_dir.mkdir()
        config_path = config_dir / "claude_desktop_config.json"
        config_path.write_text(
            json.dumps({"mcpServers": {"other": {"command": "old"}}}),
            encoding="utf-8",
        )

        register_claude(r"C:\venv\Scripts\python.exe")

        config = json.loads(config_path.read_text(encoding="utf-8"))
        assert "other" in config["mcpServers"]
        assert "mcp-cad" in config["mcpServers"]


class TestRegisterPi:
    """Tests for :func:`register_pi`."""

    def test_uses_appdata_default(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        """Default path is %APPDATA%/Pi/settings.json."""
        monkeypatch.setenv("APPDATA", str(tmp_path))

        result = register_pi(r"C:\venv\Scripts\python.exe")

        config_path = Path(result)
        assert config_path.exists()
        assert "Pi" in str(config_path)

    def test_uses_custom_path(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        """Explicit ``settings_path`` takes precedence over APPDATA."""
        monkeypatch.setenv("APPDATA", str(tmp_path))
        custom = tmp_path / "custom" / "settings.json"

        result = register_pi(r"C:\venv\Scripts\python.exe", settings_path=str(custom))

        config_path = Path(result)
        assert config_path == custom
        config = json.loads(config_path.read_text(encoding="utf-8"))
        assert "mcpServers" in config
        assert "mcp-cad" in config["mcpServers"]