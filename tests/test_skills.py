"""Unit tests for skill_sketch and skill_line."""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.skills.sketch import skill_sketch
from mcp_cad.skills.line import skill_line
from mcp_cad.skills import register_skills
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def _make_mock_provider():
    provider = MagicMock()
    provider.sketch_create.return_value = {"success": True, "plane": "XY"}
    provider.sketch_line.return_value = {"success": True, "entity_type": "line"}
    return provider


def _make_mcp():
    tools = {}
    class FakeMCP:
        def tool(self):
            def decorator(fn):
                tools[fn.__name__] = fn
                return fn
            return decorator
    return FakeMCP(), tools


# ==================================================================
# skill_sketch
# ==================================================================


class TestSkillSketch:
    def test_creates_sketch_on_xy(self):
        provider = _make_mock_provider()
        result = skill_sketch(provider, "XY")
        provider.sketch_create.assert_called_once_with("XY")
        assert result["success"] is True

    def test_creates_sketch_on_xz(self):
        provider = _make_mock_provider()
        result = skill_sketch(provider, "XZ")
        provider.sketch_create.assert_called_once_with("XZ")

    def test_defaults_to_xy(self):
        provider = _make_mock_provider()
        skill_sketch(provider)
        provider.sketch_create.assert_called_once_with("XY")

    def test_error_returns_failure(self):
        provider = _make_mock_provider()
        provider.sketch_create.side_effect = InventorCOMError("boom")
        result = skill_sketch(provider, "XY")
        assert result["success"] is False
        assert "boom" in result["error"]


# ==================================================================
# skill_line
# ==================================================================


class TestSkillLine:
    def test_simple_line(self):
        provider = _make_mock_provider()
        result = skill_line(provider, mode="simple",
                            start_x=0, start_y=0, end_x=10, end_y=5)
        provider.sketch_line.assert_called_once_with(0.0, 0.0, 10.0, 5.0)
        assert result["success"] is True

    def test_simple_defaults(self):
        provider = _make_mock_provider()
        skill_line(provider)
        provider.sketch_line.assert_called_once_with(0.0, 0.0, 0.0, 0.0)

    def test_midpoint_line_computes_opposite(self):
        """Midpoint at (5,5), end at (10,5) → start at (0,5)."""
        provider = _make_mock_provider()
        skill_line(provider, mode="midpoint",
                   mid_x=5, mid_y=5, end_x=10, end_y=5)
        # opp_x = 2*5-10 = 0, opp_y = 2*5-5 = 5
        provider.sketch_line.assert_called_once_with(0.0, 5.0, 10.0, 5.0)

    def test_midpoint_diagonal(self):
        """Midpoint at (0,0), end at (5,5) → start at (-5,-5)."""
        provider = _make_mock_provider()
        skill_line(provider, mode="midpoint",
                   mid_x=0, mid_y=0, end_x=5, end_y=5)
        provider.sketch_line.assert_called_once_with(-5.0, -5.0, 5.0, 5.0)

    def test_unknown_mode(self):
        provider = _make_mock_provider()
        result = skill_line(provider, mode="curved")
        assert result["success"] is False
        assert "Unknown mode" in result["error"]

    def test_error_returns_failure(self):
        provider = _make_mock_provider()
        provider.sketch_line.side_effect = InventorCOMError("bad")
        result = skill_line(provider, mode="simple",
                            start_x=0, start_y=0, end_x=5, end_y=5)
        assert result["success"] is False
        assert "bad" in result["error"]


# ==================================================================
# Registration
# ==================================================================


class TestSkillsRegistration:
    def test_skill_sketch_registered(self):
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)
        assert "skill_sketch" in tools

    def test_skill_line_registered(self):
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)
        assert "skill_line" in tools

    def test_skill_sketch_delegates(self):
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)
        result = tools["skill_sketch"](plane="XZ")
        provider.sketch_create.assert_called_once_with("XZ")
        assert result["success"] is True

    def test_skill_line_simple_delegates(self):
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)
        result = tools["skill_line"](start_x=0, start_y=0, end_x=5, end_y=5)
        provider.sketch_line.assert_called_once_with(0.0, 0.0, 5.0, 5.0)
        assert result["success"] is True

    def test_skill_line_midpoint_delegates(self):
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)
        result = tools["skill_line"](mode="midpoint",
                                     mid_x=5, mid_y=5, end_x=10, end_y=5)
        provider.sketch_line.assert_called_once_with(0.0, 5.0, 10.0, 5.0)
        assert result["success"] is True
