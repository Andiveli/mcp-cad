"""Unit tests for the skills system.

Verifies that skill functions compose provider operations correctly
and handle errors properly. All tests use a mock provider — no real
Inventor connection required.
"""

from __future__ import annotations

from unittest.mock import MagicMock, call

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.skills import register_skills
from mcp_cad.skills.base import Skill, SkillResult
from mcp_cad.skills.drilling import crear_patron_taladros


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_mock_provider():
    """Create a mock CADProvider with sensible return values."""
    provider = MagicMock()

    provider.sketch_create.return_value = {"success": True, "sketch_name": "Sketch1"}
    provider.sketch_circle.return_value = {"success": True, "entity_type": "circle"}
    provider.extrude.return_value = {"success": True, "feature_type": "extrude"}

    return provider


def _make_mcp():
    """Create a FastMCP-like mock that records tool registrations."""
    tools = {}

    class FakeMCP:
        def tool(self):
            """Decorator that registers a function as a tool."""
            def decorator(fn):
                tools[fn.__name__] = fn
                return fn
            return decorator

    fake_mcp = FakeMCP()
    return fake_mcp, tools


# ==================================================================
# Skill base class
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
        assert "data" not in d  # empty data dict merged in

    def test_to_dict_failure(self):
        result = SkillResult(success=False, message="Failed", data={"error": "boom"})
        d = result.to_dict()
        assert d["success"] is False
        assert d["error"] == "boom"


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
# Drilling skill — composition
# ==================================================================


class TestDrillingSkillComposition:
    """Verify drilling skill calls provider methods in correct order."""

    def test_linear_pattern_composition(self):
        """Should call sketch_create, then N circles, then extrude-cut."""
        provider = _make_mock_provider()
        result = crear_patron_taladros(
            provider,
            diametro=2.0,
            profundidad=5.0,
            espaciado=10.0,
            cantidad=4,
        )

        # 1 sketch_create on XY
        provider.sketch_create.assert_called_once_with("XY")

        # 4 circles at calculated positions
        assert provider.sketch_circle.call_count == 4
        provider.sketch_circle.assert_any_call(0.0, 0.0, 1.0)   # i=0, x=0+0*10
        provider.sketch_circle.assert_any_call(10.0, 0.0, 1.0)  # i=1
        provider.sketch_circle.assert_any_call(20.0, 0.0, 1.0)  # i=2
        provider.sketch_circle.assert_any_call(30.0, 0.0, 1.0)  # i=3

        # 1 extrude-cut
        provider.extrude.assert_called_once_with(
            "1", 5.0, direction="positive", operation="cut"
        )

        # Result is success
        assert result["success"] is True
        assert result["holes"] == 4
        assert result["cantidad"] == 4
        assert result["diametro"] == 2.0

    def test_single_hole_pattern(self):
        """Single-hole pattern should create 1 circle."""
        provider = _make_mock_provider()
        result = crear_patron_taladros(
            provider,
            diametro=1.0,
            profundidad=2.0,
            espaciado=5.0,
            cantidad=1,
        )

        provider.sketch_create.assert_called_once_with("XY")
        assert provider.sketch_circle.call_count == 1
        provider.sketch_circle.assert_called_once_with(0.0, 0.0, 0.5)
        provider.extrude.assert_called_once()

    def test_pattern_with_offsets(self):
        """Pattern with x_centro and y_centro offsets."""
        provider = _make_mock_provider()
        result = crear_patron_taladros(
            provider,
            diametro=2.0,
            profundidad=3.0,
            espaciado=5.0,
            cantidad=3,
            x_centro=10.0,
            y_centro=20.0,
        )

        # Circles at offset positions
        provider.sketch_circle.assert_any_call(10.0, 20.0, 1.0)   # i=0
        provider.sketch_circle.assert_any_call(15.0, 20.0, 1.0)   # i=1
        provider.sketch_circle.assert_any_call(20.0, 20.0, 1.0)   # i=2

    def test_radius_is_half_diameter(self):
        """Verify circle radius = diametro / 2."""
        provider = _make_mock_provider()
        crear_patron_taladros(
            provider,
            diametro=8.0,
            profundidad=1.0,
            espaciado=10.0,
            cantidad=1,
        )
        provider.sketch_circle.assert_called_once_with(0.0, 0.0, 4.0)


# ==================================================================
# Drilling skill — error handling
# ==================================================================


class TestDrillingSkillErrors:
    """Verify skill error handling."""

    def test_disconnected_error_returns_error(self):
        """Provider disconnect during sketch_create returns error envelope."""
        provider = _make_mock_provider()
        provider.sketch_create.side_effect = InventorDisconnectedError("gone")

        result = crear_patron_taladros(
            provider, diametro=2.0, profundidad=5.0, espaciado=10.0, cantidad=3
        )

        assert result["success"] is False
        assert "gone" in result["error"]

    def test_com_error_returns_error(self):
        """Provider COM error returns error envelope."""
        provider = _make_mock_provider()
        provider.sketch_circle.side_effect = InventorCOMError("bad geom")

        result = crear_patron_taladros(
            provider, diametro=2.0, profundidad=5.0, espaciado=10.0, cantidad=3
        )

        assert result["success"] is False
        assert "bad geom" in result["error"]

    def test_generic_error_returns_error(self):
        """Unexpected exception returns error envelope."""
        provider = _make_mock_provider()
        provider.extrude.side_effect = RuntimeError("crash")

        result = crear_patron_taladros(
            provider, diametro=2.0, profundidad=5.0, espaciado=10.0, cantidad=3
        )

        assert result["success"] is False
        assert "crash" in result["error"]


# ==================================================================
# Skills registration in server
# ==================================================================


class TestSkillsRegistration:
    """Verify skills register as MCP tools."""

    def test_drilling_skill_registered(self):
        """crear_patron_taladros should be registered as a tool."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)

        assert "crear_patron_taladros" in tools

    def test_drilling_skill_delegates(self):
        """Registered skill tool should call the skill function."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)

        result = tools["crear_patron_taladros"](
            diametro=2.0,
            profundidad=5.0,
            espaciado=10.0,
            cantidad=3,
        )

        assert result["success"] is True
        assert result["holes"] == 3
        provider.sketch_create.assert_called_once()

    def test_drilling_skill_with_offsets(self):
        """Skill tool should pass offset parameters."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_skills(fake_mcp, provider)

        result = tools["crear_patron_taladros"](
            diametro=1.0,
            profundidad=2.0,
            espaciado=5.0,
            cantidad=2,
            x_centro=10.0,
            y_centro=20.0,
        )

        assert result["success"] is True