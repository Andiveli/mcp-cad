"""Atomic JSON config read/write/merge for MCP agent registration.

All functions are pure Python — no COM, no Inventor, no external processes.
Testable with ``tmp_path`` pytest fixtures.

Public API
----------
- :func:`read_config`        — read and parse a JSON config file
- :func:`read_config_jsonc`  — read JSON with ``//`` comment support
- :func:`write_config`       — atomic write via temp file + ``os.replace``
- :func:`deep_merge`         — recursively merge two dicts
- :func:`merge_entry`        — merge an entry into a config dict under a key
- :func:`register_opencode`  — register mcp-cad in OpenCode config
- :func:`register_claude`    — register mcp-cad in Claude Desktop config
- :func:`register_pi`        — register mcp-cad in Pi settings
- :func:`register_vscode`    — register mcp-cad in VS Code settings.json
"""

from __future__ import annotations

import json
import os
import re
from pathlib import Path

from scripts.tui.config_schemas import (
    CLAUDE_SCHEMA,
    OPENCODE_SCHEMA,
    PI_SCHEMA,
    VSCODE_SCHEMA,
    format_schema,
)


# ---------------------------------------------------------------------------
# Low-level config I/O
# ---------------------------------------------------------------------------


def read_config(path: str | Path) -> dict | None:
    """Read and parse a JSON config file.

    Returns
    -------
    dict | None
        Parsed configuration, or ``None`` if the file does not exist.

    Raises
    ------
    json.JSONDecodeError
        If the file exists but contains invalid JSON.
    OSError
        If the file cannot be read due to a filesystem error.
    """
    p = Path(path)
    if not p.exists():
        return None
    with p.open(encoding="utf-8") as f:
        return json.load(f)


def read_config_jsonc(path: str | Path) -> dict | None:
    """Read and parse a JSONC (JSON with ``//`` comments) config file.

    Strips single-line ``//`` comments and trailing commas before parsing.
    Does **not** support block comments (``/* */``).

    Returns
    -------
    dict | None
        Parsed configuration, or ``None`` if the file does not exist.

    Raises
    ------
    json.JSONDecodeError
        If the file exists but contains invalid JSON after comment stripping.
    OSError
        If the file cannot be read due to a filesystem error.
    """
    p = Path(path)
    if not p.exists():
        return None
    with p.open(encoding="utf-8") as f:
        text = f.read()
    # Remove // comments (but not inside strings)
    text = re.sub(r"^\s*//.*$", "", text, flags=re.MULTILINE)
    # Handle trailing // comments on lines with data
    text = re.sub(r'("[^"\\]*(?:\\.[^"\\]*)*")\s*//.*$', r"\1", text, flags=re.MULTILINE)
    # Remove trailing commas before } or ] (JSONC permits them, JSON does not)
    text = re.sub(r",(\s*[}\]])", r"\1", text)
    return json.loads(text)


def write_config(path: str | Path, data: dict) -> None:
    """Write *data* as formatted JSON using atomic temp file + rename.

    Creates parent directories if they do not exist.
    On failure the temp file is removed, leaving the original intact.
    """
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    tmp = p.with_suffix(".tmp")
    try:
        with tmp.open("w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
            f.write("\n")  # trailing newline for diff-friendly output
        os.replace(tmp, p)
    except BaseException:
        if tmp.exists():
            tmp.unlink()
        raise


# ---------------------------------------------------------------------------
# Deep merge
# ---------------------------------------------------------------------------


def deep_merge(base: dict, override: dict) -> dict:
    """Recursively merge *override* into *base*.

    For nested dicts, keys are merged recursively.
    For all other types (including lists), *override* wins.
    Neither *base* nor *override* are mutated — a new dict is returned.
    """
    result = base.copy()
    for key, value in override.items():
        if key in result and isinstance(result[key], dict) and isinstance(value, dict):
            result[key] = deep_merge(result[key], value)
        else:
            result[key] = value
    return result


def merge_entry(config: dict, key: str, entry: dict) -> dict:
    """Merge *entry* into *config* under *key*, preserving other keys.

    If ``config[key]`` and *entry* are both dicts, they are deep-merged.
    Otherwise, *entry* replaces ``config[key]``.
    The original *config* dict is not mutated.
    """
    result = config.copy()
    if key in result and isinstance(result[key], dict) and isinstance(entry, dict):
        result[key] = deep_merge(result[key], entry)
    else:
        result[key] = entry
    return result


# ---------------------------------------------------------------------------
# Per-agent registration
# ---------------------------------------------------------------------------


def register_opencode(project_dir: str | Path, venv_python: str) -> str:
    """Register mcp-cad in the project's ``opencode.json``.

    Parameters
    ----------
    project_dir:
        Directory containing (or to create) ``opencode.json``.
    venv_python:
        Absolute path to the venv Python executable.

    Returns
    -------
    str
        Path to the written config file.

    Raises
    ------
    PermissionError
        If the config file cannot be written.
    json.JSONDecodeError
        If an existing config file contains invalid JSON.
    """
    config_path = Path(project_dir) / "opencode.json"
    entry = format_schema(OPENCODE_SCHEMA, venv_python)

    data = read_config(config_path) or {}
    # Merge the "mcp" key from the schema into existing config
    mcp_entry = entry.get("mcp", {})
    data = merge_entry(data, "mcp", mcp_entry)
    # Preserve $schema if present in the template but missing in data
    if "$schema" in entry and "$schema" not in data:
        data["$schema"] = entry["$schema"]

    write_config(config_path, data)
    return str(config_path)


def register_claude(venv_python: str) -> str:
    """Register mcp-cad in Claude Desktop's config file.

    The config file is located at
    ``%APPDATA%\\Claude\\claude_desktop_config.json`` on Windows.

    Parameters
    ----------
    venv_python:
        Absolute path to the venv Python executable.

    Returns
    -------
    str
        Path to the written config file.

    Raises
    ------
    PermissionError
        If the config file cannot be written.
    json.JSONDecodeError
        If an existing config file contains invalid JSON.
    """
    appdata = os.environ.get("APPDATA", "")
    config_dir = Path(appdata) / "Claude"
    config_path = config_dir / "claude_desktop_config.json"

    entry = format_schema(CLAUDE_SCHEMA, venv_python)

    data = read_config(config_path) or {}
    mcp_servers = entry.get("mcpServers", {})
    data = merge_entry(data, "mcpServers", mcp_servers)

    write_config(config_path, data)
    return str(config_path)


def register_pi(venv_python: str, settings_path: str | None = None) -> str:
    """Register mcp-cad in Pi's settings file.

    Parameters
    ----------
    venv_python:
        Absolute path to the venv Python executable.
    settings_path:
        Explicit path to Pi's ``settings.json``.
        If ``None``, defaults to ``%APPDATA%\\Pi\\settings.json``.

    Returns
    -------
    str
        Path to the written config file.

    Raises
    ------
    PermissionError
        If the config file cannot be written.
    json.JSONDecodeError
        If an existing config file contains invalid JSON.
    """
    if settings_path:
        config_path = Path(settings_path)
    else:
        config_path = Path.home() / ".pi" / "agent" / "mcp.json"

    entry = format_schema(PI_SCHEMA, venv_python)

    data = read_config(config_path) or {}
    mcp_servers = entry.get("mcpServers", {})
    data = merge_entry(data, "mcpServers", mcp_servers)

    write_config(config_path, data)
    return str(config_path)


def register_vscode(venv_python: str, settings_path: str | None = None) -> str:
    """Register mcp-cad in VS Code's user ``mcp.json``.

    Writes under ``servers`` in ``%APPDATA%/Code/User/mcp.json`` so that
    GitHub Copilot Chat discovers the server globally.

    Parameters
    ----------
    venv_python:
        Absolute path to the venv Python executable.
    settings_path:
        Explicit path to VS Code's ``mcp.json``.
        If ``None``, defaults to ``%APPDATA%\\Code\\User\\mcp.json``.

    Returns
    -------
    str
        Path to the written config file.

    Raises
    ------
    PermissionError
        If the config file cannot be written.
    json.JSONDecodeError
        If an existing config file contains invalid JSON.
    """
    if settings_path:
        config_path = Path(settings_path)
    else:
        appdata = os.environ.get("APPDATA", "")
        config_path = Path(appdata) / "Code" / "User" / "mcp.json"

    entry = format_schema(VSCODE_SCHEMA, venv_python)

    data = read_config(config_path) or {}
    servers = entry.get("servers", {})
    data = merge_entry(data, "servers", servers)

    write_config(config_path, data)
    return str(config_path)