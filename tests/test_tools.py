"""Unit tests for generic tool modules.

Verifies that each tool function delegates correctly to the provider
with proper argument forwarding, and that errors are caught and
converted to the standard {success, error} envelope.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.tools.connection import connect, disconnect, health
from mcp_cad.tools.documents import (
    doc_close,
    doc_new_assembly,
    doc_new_part,
    doc_open,
    doc_save,
    doc_save_as,
)
from mcp_cad.tools.export import export_dxf, export_pdf, export_step, export_stl
from mcp_cad.tools.features import chamfer, extrude, fillet, revolve
from mcp_cad.tools.parameters import (
    param_get,
    param_list,
    param_set,
    param_set_expression,
)
from mcp_cad.tools.properties import (
    iproperty_custom_get,
    iproperty_custom_set,
    iproperty_get,
    iproperty_set,
    iproperty_summary,
)
from mcp_cad.tools.sketches import (
    sketch_arc,
    sketch_circle,
    sketch_create,
    sketch_dimension,
    sketch_line,
    sketch_rectangle,
)


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_mock_provider():
    """Create a mock CADProvider with sensible return values."""
    provider = MagicMock()

    # Connection
    provider.connect.return_value = {"connected": True}
    provider.disconnect.return_value = {"status": "disconnected"}
    provider.health.return_value = {"connected": True}

    # Documents
    provider.doc_open.return_value = {"success": True, "document": "test.ipt"}
    provider.doc_new_part.return_value = {"success": True}
    provider.doc_new_assembly.return_value = {"success": True}
    provider.doc_save.return_value = {"success": True}
    provider.doc_save_as.return_value = {"success": True}
    provider.doc_close.return_value = {"success": True}

    # Sketches
    provider.sketch_create.return_value = {"success": True}
    provider.sketch_line.return_value = {"success": True}
    provider.sketch_circle.return_value = {"success": True}
    provider.sketch_arc.return_value = {"success": True}
    provider.sketch_rectangle.return_value = {"success": True}
    provider.sketch_dimension.return_value = {"success": True}

    # Features
    provider.extrude.return_value = {"success": True}
    provider.revolve.return_value = {"success": True}
    provider.fillet.return_value = {"success": True}
    provider.chamfer.return_value = {"success": True}

    # Parameters
    provider.param_list.return_value = {"success": True}
    provider.param_get.return_value = {"success": True}
    provider.param_set.return_value = {"success": True}
    provider.param_set_expression.return_value = {"success": True}

    # Properties
    provider.iproperty_get.return_value = {"success": True}
    provider.iproperty_set.return_value = {"success": True}
    provider.iproperty_summary.return_value = {"success": True}
    provider.iproperty_custom_get.return_value = {"success": True}
    provider.iproperty_custom_set.return_value = {"success": True}

    # Export
    provider.export_step.return_value = {"success": True}
    provider.export_stl.return_value = {"success": True}
    provider.export_pdf.return_value = {"success": True}
    provider.export_dxf.return_value = {"success": True}

    return provider


# ==================================================================
# Connection tools
# ==================================================================


class TestConnectionTools:
    """Connection tool functions delegate to provider methods."""

    def test_connect(self):
        provider = _make_mock_provider()
        result = connect(provider)
        provider.connect.assert_called_once()
        assert result["connected"] is True

    def test_disconnect(self):
        provider = _make_mock_provider()
        result = disconnect(provider)
        provider.disconnect.assert_called_once()
        assert result["status"] == "disconnected"

    def test_health(self):
        provider = _make_mock_provider()
        result = health(provider)
        provider.health.assert_called_once()
        assert result["connected"] is True

    def test_connect_error(self):
        provider = _make_mock_provider()
        provider.connect.side_effect = InventorCOMError("fail")
        result = connect(provider)
        assert result["success"] is False
        assert "fail" in result["error"]

    def test_disconnect_error(self):
        provider = _make_mock_provider()
        provider.disconnect.side_effect = RuntimeError("boom")
        result = disconnect(provider)
        assert result["success"] is False
        assert "boom" in result["error"]

    def test_health_disconnected_error(self):
        provider = _make_mock_provider()
        provider.health.side_effect = InventorDisconnectedError("gone")
        result = health(provider)
        assert result["success"] is False
        assert "gone" in result["error"]


# ==================================================================
# Document tools
# ==================================================================


class TestDocumentTools:
    """Document tool functions delegate to provider methods."""

    def test_doc_open(self):
        provider = _make_mock_provider()
        result = doc_open(provider, "/path/to/file.ipt")
        provider.doc_open.assert_called_once_with("/path/to/file.ipt")
        assert result["success"] is True

    def test_doc_new_part_default(self):
        provider = _make_mock_provider()
        result = doc_new_part(provider)
        provider.doc_new_part.assert_called_once_with("")
        assert result["success"] is True

    def test_doc_new_part_with_template(self):
        provider = _make_mock_provider()
        result = doc_new_part(provider, template="Sheet Metal")
        provider.doc_new_part.assert_called_once_with("Sheet Metal")

    def test_doc_new_assembly_default(self):
        provider = _make_mock_provider()
        result = doc_new_assembly(provider)
        provider.doc_new_assembly.assert_called_once_with("")

    def test_doc_save(self):
        provider = _make_mock_provider()
        result = doc_save(provider)
        provider.doc_save.assert_called_once()

    def test_doc_save_as(self):
        provider = _make_mock_provider()
        result = doc_save_as(provider, "/new/path.ipt")
        provider.doc_save_as.assert_called_once_with("/new/path.ipt")

    def test_doc_close_default(self):
        provider = _make_mock_provider()
        result = doc_close(provider)
        provider.doc_close.assert_called_once_with(True)

    def test_doc_close_no_save(self):
        provider = _make_mock_provider()
        result = doc_close(provider, save=False)
        provider.doc_close.assert_called_once_with(False)

    def test_doc_open_error_envelope(self):
        provider = _make_mock_provider()
        provider.doc_open.side_effect = InventorDisconnectedError("Not connected")
        result = doc_open(provider, "missing.ipt")
        assert result["success"] is False
        assert "Not connected" in result["error"]


# ==================================================================
# Sketch tools
# ==================================================================


class TestSketchTools:
    """Sketch tool functions delegate with correct arguments."""

    def test_sketch_create_default(self):
        provider = _make_mock_provider()
        result = sketch_create(provider)
        provider.sketch_create.assert_called_once_with("XY")

    def test_sketch_create_xz(self):
        provider = _make_mock_provider()
        result = sketch_create(provider, plane="XZ")
        provider.sketch_create.assert_called_once_with("XZ")

    def test_sketch_line(self):
        provider = _make_mock_provider()
        result = sketch_line(provider, 0.0, 0.0, 10.0, 5.0)
        provider.sketch_line.assert_called_once_with(0.0, 0.0, 10.0, 5.0)

    def test_sketch_circle(self):
        provider = _make_mock_provider()
        result = sketch_circle(provider, 5.0, 5.0, 3.0)
        provider.sketch_circle.assert_called_once_with(5.0, 5.0, 3.0)

    def test_sketch_arc(self):
        provider = _make_mock_provider()
        result = sketch_arc(provider, 0.0, 0.0, 5.0, 0.0, 1.57)
        provider.sketch_arc.assert_called_once_with(0.0, 0.0, 5.0, 0.0, 1.57)

    def test_sketch_rectangle(self):
        provider = _make_mock_provider()
        result = sketch_rectangle(provider, 0.0, 0.0, 10.0, 5.0)
        provider.sketch_rectangle.assert_called_once_with(0.0, 0.0, 10.0, 5.0)

    def test_sketch_dimension_with_position(self):
        provider = _make_mock_provider()
        result = sketch_dimension(provider, "Line1", 50.0, position_x=5.0, position_y=10.0)
        provider.sketch_dimension.assert_called_once_with("Line1", 50.0, (5.0, 10.0))

    def test_sketch_dimension_no_position(self):
        provider = _make_mock_provider()
        result = sketch_dimension(provider, "Line1", 25.0)
        provider.sketch_dimension.assert_called_once_with("Line1", 25.0, None)

    def test_sketch_dimension_only_x_ignores_position(self):
        """If only position_x is given, position should be None."""
        provider = _make_mock_provider()
        result = sketch_dimension(provider, "Line1", 25.0, position_x=5.0)
        provider.sketch_dimension.assert_called_once_with("Line1", 25.0, None)


# ==================================================================
# Feature tools
# ==================================================================


class TestFeatureTools:
    """Feature tool functions delegate with correct arguments."""

    def test_extrude_defaults(self):
        provider = _make_mock_provider()
        result = extrude(provider, "profile1", 10.0)
        provider.extrude.assert_called_once_with("profile1", 10.0, "positive", 0.0, "new_body")

    def test_extrude_with_all_params(self):
        provider = _make_mock_provider()
        result = extrude(provider, "p1", 5.0, direction="negative", taper=0.1, operation="cut")
        provider.extrude.assert_called_once_with("p1", 5.0, "negative", 0.1, "cut")

    def test_revolve_defaults(self):
        provider = _make_mock_provider()
        result = revolve(provider, "profile1", "axis1")
        provider.revolve.assert_called_once_with("profile1", "axis1", 360.0, "join")

    def test_revolve_with_all_params(self):
        provider = _make_mock_provider()
        result = revolve(provider, "p1", "a1", angle=180.0, operation="cut")
        provider.revolve.assert_called_once_with("p1", "a1", 180.0, "cut")

    def test_fillet_defaults(self):
        provider = _make_mock_provider()
        result = fillet(provider, "1,3,5", 2.0)
        provider.fillet.assert_called_once_with("1,3,5", 2.0, "constant")

    def test_chamfer_defaults(self):
        provider = _make_mock_provider()
        result = chamfer(provider, "1,2", 1.0)
        provider.chamfer.assert_called_once_with("1,2", 1.0, "equal_distance")

    def test_extrude_error_envelope(self):
        provider = _make_mock_provider()
        provider.extrude.side_effect = InventorCOMError("bad profile")
        result = extrude(provider, "bad", 1.0)
        assert result["success"] is False
        assert "bad profile" in result["error"]


# ==================================================================
# Parameter tools
# ==================================================================


class TestParameterTools:
    """Parameter tool functions delegate with correct arguments."""

    def test_param_list_default(self):
        provider = _make_mock_provider()
        result = param_list(provider)
        provider.param_list.assert_called_once_with(None)

    def test_param_list_with_filter(self):
        provider = _make_mock_provider()
        result = param_list(provider, filter_pattern="d0")
        provider.param_list.assert_called_once_with("d0")

    def test_param_get(self):
        provider = _make_mock_provider()
        result = param_get(provider, "d0")
        provider.param_get.assert_called_once_with("d0")

    def test_param_set(self):
        provider = _make_mock_provider()
        result = param_set(provider, "d0", 10.0)
        provider.param_set.assert_called_once_with("d0", 10.0)

    def test_param_set_expression(self):
        provider = _make_mock_provider()
        result = param_set_expression(provider, "d1", "d0 * 2")
        provider.param_set_expression.assert_called_once_with("d1", "d0 * 2")


# ==================================================================
# iProperty tools
# ==================================================================


class TestIPropertyTools:
    """iProperty tool functions delegate with correct arguments."""

    def test_iproperty_get_default(self):
        provider = _make_mock_provider()
        result = iproperty_get(provider, "Title")
        provider.iproperty_get.assert_called_once_with("Title", "Summary")

    def test_iproperty_get_with_set(self):
        provider = _make_mock_provider()
        result = iproperty_get(provider, "Part Number", property_set="Project")
        provider.iproperty_get.assert_called_once_with("Part Number", "Project")

    def test_iproperty_set_default(self):
        provider = _make_mock_provider()
        result = iproperty_set(provider, "Title", "New Title")
        provider.iproperty_set.assert_called_once_with("Title", "New Title", "Summary")

    def test_iproperty_set_with_set(self):
        provider = _make_mock_provider()
        result = iproperty_set(provider, "Part Number", "ABC", property_set="Project")
        provider.iproperty_set.assert_called_once_with("Part Number", "ABC", "Project")

    def test_iproperty_summary(self):
        provider = _make_mock_provider()
        result = iproperty_summary(provider)
        provider.iproperty_summary.assert_called_once()

    def test_iproperty_custom_get(self):
        provider = _make_mock_provider()
        result = iproperty_custom_get(provider, "Vendor")
        provider.iproperty_custom_get.assert_called_once_with("Vendor")

    def test_iproperty_custom_set(self):
        provider = _make_mock_provider()
        result = iproperty_custom_set(provider, "Vendor", "Acme")
        provider.iproperty_custom_set.assert_called_once_with("Vendor", "Acme")


# ==================================================================
# Export tools
# ==================================================================


class TestExportTools:
    """Export tool functions delegate with correct arguments."""

    def test_export_step_no_options(self):
        provider = _make_mock_provider()
        result = export_step(provider, "/out/part.stp")
        provider.export_step.assert_called_once_with("/out/part.stp", None)

    def test_export_step_with_options(self):
        provider = _make_mock_provider()
        opts = {"tolerance": 0.01}
        result = export_step(provider, "/out/part.stp", options=opts)
        provider.export_step.assert_called_once_with("/out/part.stp", opts)

    def test_export_stl(self):
        provider = _make_mock_provider()
        result = export_stl(provider, "/out/part.stl")
        provider.export_stl.assert_called_once_with("/out/part.stl", None)

    def test_export_pdf(self):
        provider = _make_mock_provider()
        result = export_pdf(provider, "/out/part.pdf")
        provider.export_pdf.assert_called_once_with("/out/part.pdf", None)

    def test_export_dxf(self):
        provider = _make_mock_provider()
        result = export_dxf(provider, "/out/sketch.dxf")
        provider.export_dxf.assert_called_once_with("/out/sketch.dxf", None)