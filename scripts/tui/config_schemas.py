"""JSON configuration templates for MCP agent registration.

These schemas serve as the single source of truth for the configuration
format expected by each supported agent.  Python-path placeholders
(`{python_exe}`) are replaced at registration time with the actual
venv Python executable path via :func:`format_schema`.

Schemas
-------
- :data:`OPENCODE_SCHEMA` — OpenCode project-level config (``opencode.json``)
- :data:`CLAUDE_SCHEMA`  — Claude Desktop config (``claude_desktop_config.json``)
- :data:`PI_SCHEMA`      — Pi (IntelliJ) config (``settings.json``)
"""

from __future__ import annotations

from copy import deepcopy

# ---------------------------------------------------------------------------
# OpenCode format — project-level opencode.json
# URL: https://opencode.ai/config.json
# ---------------------------------------------------------------------------
OPENCODE_SCHEMA: dict = {
    "$schema": "https://opencode.ai/config.json",
    "mcp": {
        "mcp-cad": {
            "type": "local",
            "command": ["{python_exe}", "-m", "mcp_cad"],
        }
    },
}

# ---------------------------------------------------------------------------
# Claude Desktop format — mcpServers in claude_desktop_config.json
# Path: %APPDATA%\\Claude\\claude_desktop_config.json
# ---------------------------------------------------------------------------
CLAUDE_SCHEMA: dict = {
    "mcpServers": {
        "mcp-cad": {
            "command": "{python_exe}",
            "args": ["-m", "mcp_cad"],
        }
    },
}

# ---------------------------------------------------------------------------
# Pi (IntelliJ) format — standard MCP via mcpServers
# ---------------------------------------------------------------------------
PI_SCHEMA: dict = {
    "mcpServers": {
        "mcp-cad": {
            "command": "{python_exe}",
            "args": ["-m", "mcp_cad"],
        }
    },
}

# ---------------------------------------------------------------------------
# VS Code / GitHub Copilot format — user-level mcp.json
# Path: %APPDATA%/Code/User/mcp.json
# ---------------------------------------------------------------------------
VSCODE_SCHEMA: dict = {
    "servers": {
        "mcp-cad": {
            "command": "{python_exe}",
            "args": ["-m", "mcp_cad"],
        }
    },
}


def format_schema(schema: dict, python_exe: str) -> dict:
    """Return a deep copy of *schema* with ``{python_exe}`` placeholders replaced.

    Parameters
    ----------
    schema:
        One of the module-level schemas (or any dict containing the placeholder).
    python_exe:
        Absolute path to the venv Python executable.

    Returns
    -------
    dict
        A new dict with all ``{python_exe}`` occurrences replaced.
    """
    result = deepcopy(schema)
    _replace_placeholder(result, python_exe)
    return result


def _replace_placeholder(obj: dict | list, python_exe: str) -> None:
    """Recursively replace ``{python_exe}`` in string values."""
    if isinstance(obj, dict):
        for key, value in obj.items():
            if isinstance(value, str) and "{python_exe}" in value:
                obj[key] = value.replace("{python_exe}", python_exe)
            elif isinstance(value, (dict, list)):
                _replace_placeholder(value, python_exe)
    elif isinstance(obj, list):
        for i, item in enumerate(obj):
            if isinstance(item, str) and "{python_exe}" in item:
                obj[i] = item.replace("{python_exe}", python_exe)
            elif isinstance(item, (dict, list)):
                _replace_placeholder(item, python_exe)