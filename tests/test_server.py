"""Unit tests for the FastMCP server tool registration and error handling.

All tests run on plain mocks — no real Inventor installation required.
Verifies that:
- Tools are properly registered on the FastMCP instance
- Each tool delegates to the correct manager method
- Errors (InventorDisconnectedError, InventorCOMError) are caught
  and converted to the standard {success, error} envelope
"""

from __future__ import annotations

from unittest.mock import MagicMock, patch

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.server import register_tools


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_managers():
    """Create mock managers with fresh MagicMock instances."""
    driver = MagicMock()
    driver.connect = MagicMock(return_value={"connected": True, "inventor_version": "28.0"})
    driver.health = MagicMock(return_value={"connected": True, "inventor_version": "28.0"})
    driver.disconnect = MagicMock(return_value={"status": "disconnected"})

    doc_mgr = MagicMock()
    sketch_mgr = MagicMock()
    feature_mgr = MagicMock()
    param_mgr = MagicMock()
    prop_mgr = MagicMock()
    export_mgr = MagicMock()

    return driver, doc_mgr, sketch_mgr, feature_mgr, param_mgr, prop_mgr, export_mgr


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
        driver, doc, sketch, feature, param, prop, export = _make_managers()

        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

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
        """Should register exactly the expected number of tools."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()

        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        assert len(tools) == 32


# ==================================================================
# Connection tools — delegation
# ==================================================================


class TestConnectionTools:
    """Verify connection tools delegate to the driver."""

    def test_inventor_connect_delegates(self):
        """Should call driver.connect() and return result."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["inventor_connect"]()
        driver.connect.assert_called_once()
        assert result["connected"] is True

    def test_inventor_health_delegates(self):
        """Should call driver.health() and return result."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["inventor_health"]()
        driver.health.assert_called_once()
        assert result["connected"] is True

    def test_inventor_disconnect_delegates(self):
        """Should call driver.disconnect() and return result."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["inventor_disconnect"]()
        driver.disconnect.assert_called_once()
        assert result["status"] == "disconnected"


# ==================================================================
# Document tools — delegation
# ==================================================================


class TestDocumentTools:
    """Verify document tools delegate to the document manager."""

    def test_doc_open_delegates(self):
        """Should call doc_mgr.doc_open()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        doc.doc_open = MagicMock(return_value={"success": True, "document": "test.ipt"})
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["doc_open"](path=r"C:\test.ipt")
        doc.doc_open.assert_called_once_with(r"C:\test.ipt")
        assert result["success"] is True

    def test_doc_new_part_delegates(self):
        """Should call doc_mgr.doc_new_part()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["doc_new_part"](template="Sheet Metal")
        doc.doc_new_part.assert_called_once_with("Sheet Metal")

    def test_doc_save_delegates(self):
        """Should call doc_mgr.doc_save()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["doc_save"]()
        doc.doc_save.assert_called_once()


# ==================================================================
# Sketch tools — delegation
# ==================================================================


class TestSketchTools:
    """Verify sketch tools delegate with correct arguments."""

    def test_sketch_create_delegates(self):
        """Should call sketch_mgr.sketch_create()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["sketch_create"](plane="XZ")
        sketch.sketch_create.assert_called_once_with("XZ")

    def test_sketch_line_delegates(self):
        """Should call sketch_mgr.sketch_line() with coordinates."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["sketch_line"](x1=0.0, y1=0.0, x2=1.0, y2=1.0)
        sketch.sketch_line.assert_called_once_with(0.0, 0.0, 1.0, 1.0)

    def test_sketch_dimension_merges_position(self):
        """Should merge position_x and position_y into a tuple."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["sketch_dimension"](entity="d0", value=5.0, position_x=1.0, position_y=2.0)
        sketch.sketch_dimension.assert_called_once_with("d0", 5.0, (1.0, 2.0))

    def test_sketch_dimension_no_position(self):
        """Should pass None when position is not provided."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["sketch_dimension"](entity="d0", value=5.0)
        sketch.sketch_dimension.assert_called_once_with("d0", 5.0, None)


# ==================================================================
# Error handling — standard envelope
# ==================================================================


class TestErrorHandling:
    """Verify errors are caught and converted to the {success, error} envelope."""

    def test_disconnected_error_returns_error_envelope(self):
        """InventorDisconnectedError should become {success: False, error: ...}."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        doc.doc_open.side_effect = InventorDisconnectedError("Not connected")
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["doc_open"](path="test.ipt")
        assert result["success"] is False
        assert "Not connected" in result["error"]

    def test_com_error_returns_error_envelope(self):
        """InventorCOMError should become {success: False, error: ...}."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        doc.doc_open.side_effect = InventorCOMError("File not found")
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["doc_open"](path="missing.ipt")
        assert result["success"] is False
        assert "File not found" in result["error"]

    def test_generic_exception_returns_error_envelope(self):
        """Unexpected exceptions should also become {success: False, error: ...}."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        doc.doc_save.side_effect = RuntimeError("Unexpected crash")
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["doc_save"]()
        assert result["success"] is False
        assert "Unexpected crash" in result["error"]

    def test_connect_error_returns_error_envelope(self):
        """Connection errors should return the error envelope."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        driver.connect.side_effect = InventorCOMError("COM init failed")
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["inventor_connect"]()
        assert result["success"] is False
        assert "COM init failed" in result["error"]

    def test_health_never_raises(self):
        """Health should always return a dict, never raise."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        driver.health.side_effect = RuntimeError("Boom")
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["inventor_health"]()
        assert result["success"] is False
        assert "Boom" in result["error"]


# ==================================================================
# Export tools — delegation
# ==================================================================


class TestExportTools:
    """Verify export tools delegate to ExportManager with correct args."""

    def test_export_step_delegates(self):
        """Should call export_mgr.export_step()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        export.export_step = MagicMock(return_value={"success": True, "path": "out.stp"})
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["export_step"](path="out.stp")
        export.export_step.assert_called_once_with("out.stp", None)
        assert result["success"] is True

    def test_export_stl_with_options(self):
        """Should pass options dict to export_mgr."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        export.export_stl = MagicMock(return_value={"success": True, "path": "out.stl"})
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        opts = {"quality": 2}
        result = tools["export_stl"](path="out.stl", options=opts)
        export.export_stl.assert_called_once_with("out.stl", opts)

    def test_export_pdf_delegates(self):
        """Should call export_mgr.export_pdf()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        export.export_pdf = MagicMock(return_value={"success": True, "path": "out.pdf"})
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["export_pdf"](path="out.pdf")
        export.export_pdf.assert_called_once_with("out.pdf", None)

    def test_export_dxf_delegates(self):
        """Should call export_mgr.export_dxf()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        export.export_dxf = MagicMock(return_value={"success": True, "path": "out.dxf"})
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        result = tools["export_dxf"](path="out.dxf")
        export.export_dxf.assert_called_once_with("out.dxf", None)


# ==================================================================
# main() wiring
# ==================================================================


class TestMainWiring:
    """Verify that main() sets up FastMCP correctly."""

    @patch("mcp_cad.server.FastMCP")
    @patch("mcp_cad.server.RealInventorDriver")
    def test_main_creates_mcp_instance(self, mock_driver_cls, mock_mcp_cls):
        """main() should create a FastMCP instance named 'mcp-cad'."""
        mock_mcp_instance = MagicMock()
        mock_mcp_cls.return_value = mock_mcp_instance
        mock_driver_instance = MagicMock()
        mock_driver_instance._inventor = None
        mock_driver_cls.return_value = mock_driver_instance

        # Import main and call it
        # We patch mcp.run to prevent stdio transport
        mock_mcp_instance.run = MagicMock()

        from mcp_cad.server import main
        main()

        mock_mcp_cls.assert_called_once_with("mcp-cad")
        mock_mcp_instance.run.assert_called_once_with(transport="stdio")

    @patch("mcp_cad.server.FastMCP")
    @patch("mcp_cad.server.RealInventorDriver")
    def test_main_creates_driver_and_managers(self, mock_driver_cls, mock_mcp_cls):
        """main() should create a driver and instantiate all managers."""
        mock_mcp_instance = MagicMock()
        mock_mcp_cls.return_value = mock_mcp_instance
        mock_driver_instance = MagicMock()
        mock_driver_instance._inventor = None
        mock_driver_cls.return_value = mock_driver_instance
        mock_mcp_instance.run = MagicMock()

        from mcp_cad.server import main
        main()

        # RealInventorDriver should be instantiated once
        mock_driver_cls.assert_called_once()


# ==================================================================
# Feature tools — delegation
# ==================================================================


class TestFeatureTools:
    """Verify feature tools delegate with correct arguments."""

    def test_extrude_delegates(self):
        """Should call feature_mgr.extrude() with all params."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["extrude"](profile="p1", distance=10.0, direction="positive", taper=0.0, operation="join")
        feature.extrude.assert_called_once_with("p1", 10.0, "positive", 0.0, "join")

    def test_revolve_delegates(self):
        """Should call feature_mgr.revolve() with all params."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["revolve"](profile="p1", axis="axis1", angle=180.0, operation="cut")
        feature.revolve.assert_called_once_with("p1", "axis1", 180.0, "cut")


# ==================================================================
# Parameter tools — delegation
# ==================================================================


class TestParameterTools:
    """Verify parameter tools delegate with correct arguments."""

    def test_param_list_delegates(self):
        """Should call param_mgr.param_list()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        param.param_list = MagicMock(return_value={"success": True, "parameters": []})
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["param_list"](filter_pattern="d0")
        param.param_list.assert_called_once_with("d0")

    def test_param_set_expression_delegates(self):
        """Should call param_mgr.param_set_expression()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["param_set_expression"](name="d0", expression="d1 * 2")
        param.param_set_expression.assert_called_once_with("d0", "d1 * 2")


# ==================================================================
# iProperty tools — delegation
# ==================================================================


class TestIPropertyTools:
    """Verify iProperty tools delegate with correct arguments."""

    def test_iproperty_get_delegates(self):
        """Should call prop_mgr.iproperty_get()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["iproperty_get"](name="Title", property_set="Summary")
        prop.iproperty_get.assert_called_once_with("Title", "Summary")

    def test_iproperty_custom_set_delegates(self):
        """Should call prop_mgr.iproperty_custom_set()."""
        fake_mcp, tools = _make_mcp()
        driver, doc, sketch, feature, param, prop, export = _make_managers()
        register_tools(fake_mcp, driver, doc, sketch, feature, param, prop, export)

        tools["iproperty_custom_set"](name="CustomProp", value="test")
        prop.iproperty_custom_set.assert_called_once_with("CustomProp", "test")