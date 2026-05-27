"""Unit tests for the skills system.

Verifies Skill base class, SkillResult, and the register_skills
infrastructure.  Specific skill tests live alongside their modules.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.skills import register_skills
from mcp_cad.skills.base import Skill, SkillResult


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_mock_provider():
    """Create a mock CADProvider."""
    return MagicMock()


def _make_mcp():
    """Create a FastMCP-like mock that records tool registrations."""
    tools = {}

    class FakeMCP:
        def tool(self):
            def decorator(fn):
                tools[fn.__name__] = fn
                return fn
            return decorator

    fake_mcp = FakeMCP()
    return fake_mcp, tools


# ==================================================================
# SkillResult
# ==================================================================


class TestSkillResult:
    """Verify SkillResult data class."""

    def test_to_dict_success(self):
        result = SkillResult(success=True, message="Done", data={"holes": 4})
        d = result.to_dict()
        assert d["success"] is True
        assert d["message"] == "Done"
        assert d["holes"] == 4

    def test_to_dict_no_data(self):
        result = SkillResult(success=True, message="OK")
        d = result.to_dict()
        assert d["success"] is True
        assert d["message"] == "OK"

    def test_to_dict_failure(self):
        result = SkillResult(success=False, message="Failed", data={"error": "boom"})
        d = result.to_dict()
        assert d["success"] is False
        assert d["error"] == "boom"


# ==================================================================
# Skill base class
# ==================================================================


class TestSkillBaseClass:
    """Verify Skill base class behavior."""

    def test_skill_stores_provider(self):
        provider = MagicMock()
        skill = Skill(provider)
        assert skill.provider is provider

    def test_skill_register_raises(self):
        """Base Skill.register() must raise NotImplementedError."""
        provider = MagicMock()
        skill = Skill(provider)
        with pytest.raises(NotImplementedError):
            skill.register(MagicMock())


# ==================================================================
# Skills registration infrastructure
# ==================================================================


class TestSkillsRegistration:
    """Verify register_skills works with empty skillset."""

    def test_register_skills_noops_without_skills(self):
        """register_skills should not fail when no skills are registered."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)
        # No tools registered, no crash
        assert isinstance(tools, dict)

    def test_register_skills_accepts_provider(self):
        """register_skills should accept a CADProvider."""
        fake_mcp, _ = _make_mcp()
        provider = _make_mock_provider()
        # Should not raise
        register_skills(fake_mcp, provider)
