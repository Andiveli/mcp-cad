"""Unit tests for the FastMCP server tool registration and error handling.

All tests run on plain mocks — no real Inventor installation required.
Verifies that:
- Tools are properly registered on the FastMCP instance
- Each tool delegates to the correct provider method
- Errors (InventorDisconnectedError, InventorCOMError) are caught
  and converted to the standard {success, error} envelope
"""

from __future__ import annotations

from unittest.mock import MagicMock, patch

import pytest

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.tools import register_tools


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_mock_provider():
    """Create a mock CADProvider with sensible return values."""
    provider = MagicMock()

    # Connection methods
    provider.connect.return_value = {"connected": True, "inventor_version": "28.0"}
    provider.health.return_value = {"connected": True, "inventor_version": "28.0"}
    provider.disconnect.return_value = {"status": "disconnected"}

    # Document methods
    provider.doc_open.return_value = {"success": True, "document": "test.ipt"}
    provider.doc_new_part.return_value = {"success": True}
    provider.doc_new_assembly.return_value = {"success": True}
    provider.doc_save.return_value = {"success": True}
    provider.doc_save_as.return_value = {"success": True}
    provider.doc_close.return_value = {"success": True}

    # Sketch methods
    provider.sketch_create.return_value = {"success": True, "sketch_name": "Sketch1"}
    provider.sketch_line.return_value = {"success": True}
    provider.sketch_circle.return_value = {"success": True}
    provider.sketch_arc.return_value = {"success": True}
    provider.sketch_rectangle.return_value = {"success": True}
    provider.sketch_dimension.return_value = {"success": True}

    # Feature methods
    provider.extrude.return_value = {"success": True, "feature_type": "extrude"}
    provider.revolve.return_value = {"success": True, "feature_type": "revolve"}
    provider.fillet.return_value = {"success": True, "feature_type": "fillet"}
    provider.chamfer.return_value = {"success": True, "feature_type": "chamfer"}

    # Parameter methods
    provider.param_list.return_value = {"success": True, "parameters": []}
    provider.param_get.return_value = {"success": True}
    provider.param_set.return_value = {"success": True}
    provider.param_set_expression.return_value = {"success": True}

    # Property methods
    provider.iproperty_get.return_value = {"success": True}
    provider.iproperty_set.return_value = {"success": True}
    provider.iproperty_summary.return_value = {"success": True}
    provider.iproperty_custom_get.return_value = {"success": True}
    provider.iproperty_custom_set.return_value = {"success": True}

    # Export methods
    provider.export_step.return_value = {"success": True, "path": "out.stp"}
    provider.export_stl.return_value = {"success": True, "path": "out.stl"}
    provider.export_pdf.return_value = {"success": True, "path": "out.pdf"}
    provider.export_dxf.return_value = {"success": True, "path": "out.dxf"}

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
# Tool registration
# ==================================================================


class TestToolRegistration:
    """Verify all expected tools are registered."""

    def test_all_tools_registered(self):
        """Should register all MCP tools."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()

        register_tools(fake_mcp, provider)

        expected_tools = [
            "inventor_connect",
            "inventor_health",
            "inventor_disconnect",
            "doc_open",
            "doc_new_part",
            "doc_new_assembly",
            "doc_save",
            "doc_save_as",
            "doc_close",
            "sketch_create",
            "sketch_line",
            "sketch_circle",
            "sketch_arc",
            "sketch_rectangle",
            "sketch_dimension",
            "extrude",
            "revolve",
            "fillet",
            "chamfer",
            "param_list",
            "param_get",
            "param_set",
            "param_set_expression",
            "iproperty_get",
            "iproperty_set",
            "iproperty_summary",
            "iproperty_custom_get",
            "iproperty_custom_set",
            "export_step",
            "export_stl",
            "export_pdf",
            "export_dxf",
        ]

        for name in expected_tools:
            assert name in tools, f"Tool '{name}' not registered"

    def test_tool_count(self):
        """Should register exactly 35 tools."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()

        register_tools(fake_mcp, provider)

        assert len(tools) == 41


# ==================================================================
# Connection tools — delegation
# ==================================================================


class TestConnectionTools:
    """Verify connection tools delegate to the provider."""

    def test_inventor_connect_delegates(self):
        """Should call provider.connect() and return result."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["inventor_connect"]()
        provider.connect.assert_called_once()
        assert result["connected"] is True

    def test_inventor_health_delegates(self):
        """Should call provider.health() and return result."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["inventor_health"]()
        provider.health.assert_called_once()
        assert result["connected"] is True

    def test_inventor_disconnect_delegates(self):
        """Should call provider.disconnect() and return result."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["inventor_disconnect"]()
        provider.disconnect.assert_called_once()
        assert result["status"] == "disconnected"


# ==================================================================
# Document tools — delegation
# ==================================================================


class TestDocumentTools:
    """Verify document tools delegate to the provider."""

    def test_doc_open_delegates(self):
        """Should call provider.doc_open()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["doc_open"](path=r"C:\test.ipt")
        provider.doc_open.assert_called_once_with(r"C:\test.ipt")
        assert result["success"] is True

    def test_doc_new_part_delegates(self):
        """Should call provider.doc_new_part()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["doc_new_part"](template="Sheet Metal")
        provider.doc_new_part.assert_called_once_with("Sheet Metal")

    def test_doc_save_delegates(self):
        """Should call provider.doc_save()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["doc_save"]()
        provider.doc_save.assert_called_once()


# ==================================================================
# Sketch tools — delegation
# ==================================================================


class TestSketchTools:
    """Verify sketch tools delegate with correct arguments."""

    def test_sketch_create_delegates(self):
        """Should call provider.sketch_create()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["sketch_create"](plane="XZ")
        provider.sketch_create.assert_called_once_with("XZ")

    def test_sketch_line_delegates(self):
        """Should call provider.sketch_line() with coordinates."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["sketch_line"](x1=0.0, y1=0.0, x2=1.0, y2=1.0)
        provider.sketch_line.assert_called_once_with(0.0, 0.0, 1.0, 1.0)

    def test_sketch_dimension_merges_position(self):
        """Should pass position_x and position_y as keyword args to the provider."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["sketch_dimension"](entity="d0", value=5.0, position_x=1.0, position_y=2.0)
        provider.sketch_dimension.assert_called_once_with("d0", 5.0, position_x=1.0, position_y=2.0)

    def test_sketch_dimension_no_position(self):
        """Should pass None for both position kwargs when not provided."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["sketch_dimension"](entity="d0", value=5.0)
        provider.sketch_dimension.assert_called_once_with("d0", 5.0, position_x=None, position_y=None)


# ==================================================================
# Error handling — standard envelope
# ==================================================================


class TestErrorHandling:
    """Verify errors are caught and converted to the {success, error} envelope."""

    def test_disconnected_error_returns_error_envelope(self):
        """InventorDisconnectedError should become {success: False, error: ...}."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        provider.doc_open.side_effect = InventorDisconnectedError("Not connected")
        register_tools(fake_mcp, provider)

        result = tools["doc_open"](path="test.ipt")
        assert result["success"] is False
        assert "Not connected" in result["error"]

    def test_com_error_returns_error_envelope(self):
        """InventorCOMError should become {success: False, error: ...}."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        provider.doc_open.side_effect = InventorCOMError("File not found")
        register_tools(fake_mcp, provider)

        result = tools["doc_open"](path="missing.ipt")
        assert result["success"] is False
        assert "File not found" in result["error"]

    def test_generic_exception_returns_error_envelope(self):
        """Unexpected exceptions should also become {success: False, error: ...}."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        provider.doc_save.side_effect = RuntimeError("Unexpected crash")
        register_tools(fake_mcp, provider)

        result = tools["doc_save"]()
        assert result["success"] is False
        assert "Unexpected crash" in result["error"]

    def test_connect_error_returns_error_envelope(self):
        """Connection errors should return the error envelope."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        provider.connect.side_effect = InventorCOMError("COM init failed")
        register_tools(fake_mcp, provider)

        result = tools["inventor_connect"]()
        assert result["success"] is False
        assert "COM init failed" in result["error"]

    def test_health_never_raises(self):
        """Health should always return a dict, never raise."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        provider.health.side_effect = RuntimeError("Boom")
        register_tools(fake_mcp, provider)

        result = tools["inventor_health"]()
        assert result["success"] is False
        assert "Boom" in result["error"]


# ==================================================================
# Export tools — delegation
# ==================================================================


class TestExportTools:
    """Verify export tools delegate to the provider with correct args."""

    def test_export_step_delegates(self):
        """Should call provider.export_step()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["export_step"](path="out.stp")
        provider.export_step.assert_called_once_with("out.stp", None)
        assert result["success"] is True

    def test_export_stl_with_options(self):
        """Should pass options dict to provider."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        opts = {"quality": 2}
        result = tools["export_stl"](path="out.stl", options=opts)
        provider.export_stl.assert_called_once_with("out.stl", opts)

    def test_export_pdf_delegates(self):
        """Should call provider.export_pdf()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["export_pdf"](path="out.pdf")
        provider.export_pdf.assert_called_once_with("out.pdf", None)

    def test_export_dxf_delegates(self):
        """Should call provider.export_dxf()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        result = tools["export_dxf"](path="out.dxf")
        provider.export_dxf.assert_called_once_with("out.dxf", None)


# ==================================================================
# main() wiring
# ==================================================================


class TestMainWiring:
    """Verify that main() sets up FastMCP correctly."""

    @patch("mcp_cad.server.create_inventor_provider")
    @patch("mcp_cad.server.FastMCP")
    def test_main_creates_mcp_instance(self, mock_mcp_cls, mock_create_provider):
        """main() should create a FastMCP instance named 'mcp-cad'."""
        mock_mcp_instance = MagicMock()
        mock_mcp_cls.return_value = mock_mcp_instance
        mock_mcp_instance.run = MagicMock()
        mock_create_provider.return_value = MagicMock()

        from mcp_cad.server import main
        main()

        mock_mcp_cls.assert_called_once_with("mcp-cad")
        mock_mcp_instance.run.assert_called_once_with(transport="stdio")

    @patch("mcp_cad.server.create_inventor_provider")
    @patch("mcp_cad.server.FastMCP")
    def test_main_calls_create_inventor_provider(self, mock_mcp_cls, mock_create_provider):
        """main() should call create_inventor_provider() to get the provider."""
        mock_mcp_instance = MagicMock()
        mock_mcp_cls.return_value = mock_mcp_instance
        mock_mcp_instance.run = MagicMock()
        mock_create_provider.return_value = MagicMock()

        from mcp_cad.server import main
        main()

        mock_create_provider.assert_called_once()


# ==================================================================
# Feature tools — delegation
# ==================================================================


class TestFeatureTools:
    """Verify feature tools delegate with correct arguments."""

    def test_extrude_delegates(self):
        """Should call provider.extrude() with all params."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["extrude"](profile="p1", distance=10.0, direction="positive", taper=0.0, operation="join")
        provider.extrude.assert_called_once_with("p1", 10.0, "positive", 0.0, "join")

    def test_revolve_delegates(self):
        """Should call provider.revolve() with all params."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["revolve"](profile="p1", axis="axis1", angle=180.0, operation="cut")
        provider.revolve.assert_called_once_with("p1", "axis1", 180.0, "cut")


# ==================================================================
# Parameter tools — delegation
# ==================================================================


class TestParameterTools:
    """Verify parameter tools delegate with correct arguments."""

    def test_param_list_delegates(self):
        """Should call provider.param_list()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["param_list"](filter_pattern="d0")
        provider.param_list.assert_called_once_with("d0")

    def test_param_set_expression_delegates(self):
        """Should call provider.param_set_expression()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["param_set_expression"](name="d0", expression="d1 * 2")
        provider.param_set_expression.assert_called_once_with("d0", "d1 * 2")


# ==================================================================
# iProperty tools — delegation
# ==================================================================


class TestIPropertyTools:
    """Verify iProperty tools delegate with correct arguments."""

    def test_iproperty_get_delegates(self):
        """Should call provider.iproperty_get()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["iproperty_get"](name="Title", property_set="Summary")
        provider.iproperty_get.assert_called_once_with("Title", "Summary")

    def test_iproperty_custom_set_delegates(self):
        """Should call provider.iproperty_custom_set()."""
        fake_mcp, tools = _make_mcp()
        provider = _make_mock_provider()
        register_tools(fake_mcp, provider)

        tools["iproperty_custom_set"](name="CustomProp", value="test")
        provider.iproperty_custom_set.assert_called_once_with("CustomProp", "test")