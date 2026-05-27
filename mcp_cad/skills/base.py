"""Skill base class and result type for composable CAD operations.

Skills are higher-level operations that compose atomic provider calls
(e.g. sketch → circle → extrude-cut) into meaningful workflows.
They depend ONLY on the ``CADProvider`` protocol — never on Inventor-specific
modules.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider


class SkillResult:
    """Structured result from a skill operation.

    Wraps success/failure state, a human-readable message, and optional
    data payload.  Converts naturally to a dict for MCP tool responses.
    """

    def __init__(
        self,
        success: bool,
        message: str,
        data: dict | None = None,
    ) -> None:
        self.success = success
        self.message = message
        self.data = data or {}

    def to_dict(self) -> dict[str, Any]:
        """Convert to the standard MCP response envelope."""
        result: dict[str, Any] = {
            "success": self.success,
            "message": self.message,
        }
        result.update(self.data)
        return result


class Skill:
    """Base class for composable CAD skills.

    Subclasses receive a ``CADProvider`` via dependency injection and
    register their operations as MCP tools via ``register(mcp_instance)``.
    """

    def __init__(self, provider: CADProvider) -> None:
        self.provider = provider

    def register(self, mcp_instance: Any) -> None:
        """Register this skill's operations as MCP tools.

        Must be overridden by subclasses.
        """
        raise NotImplementedError("Subclasses must implement register()")