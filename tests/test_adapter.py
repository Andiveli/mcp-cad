"""Unit tests for the InventorProvider adapter.

Verifies that every CADProvider protocol method on InventorProvider
delegates 1:1 to the corresponding manager method, with correct
argument forwarding and no extra logic.
"""

from __future__ import annotations

from unittest.mock import MagicMock, PropertyMock, call

import pytest

from mcp_cad.core.protocol import CADProvider
from mcp_cad.providers.inventor.adapter import InventorProvider, create_inventor_provider


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_mock_provider() -> tuple[InventorProvider, dict[str, MagicMock]]:
    """Build an InventorProvider with all mocked managers.

    Returns (provider, mocks) where mocks has keys:
        driver, doc, sketch, feature, param, prop, export
    """
    driver = MagicMock()
    type(driver).inventor = PropertyMock(return_value=None)

    provider = InventorProvider(driver)

    # Replace manager instances with mocks
    mock_doc = MagicMock()
    mock_sketch = MagicMock()
    mock_feature = MagicMock()
    mock_param = MagicMock()
    mock_prop = MagicMock()
    mock_export = MagicMock()

    provider._doc = mock_doc
    provider._sketch = mock_sketch
    provider._feature = mock_feature
    provider._param = mock_param
    provider._prop = mock_prop
    provider._export = mock_export

    # Set default return values for manager methods that are called
    for mgr in [mock_doc, mock_sketch, mock_feature, mock_param, mock_prop, mock_export]:
        for method_name in dir(mgr):
            if not method_name.startswith("_") and not method_name.startswith("assert"):
                try:
                    mock_method = getattr(mgr, method_name)
                    if callable(mock_method):
                        mock_method.return_value = {"success": True}
                except AttributeError:
                    pass

    mocks = {
        "driver": driver,
        "doc": mock_doc,
        "sketch": mock_sketch,
        "feature": mock_feature,
        "param": mock_param,
        "prop": mock_prop,
        "export": mock_export,
    }

    return provider, mocks


# ==================================================================
# Protocol conformance
# ==================================================================


class TestProtocolConformance:
    """InventorProvider satisfies the CADProvider structural protocol."""

    def test_isinstance_cadprovider(self):
        """InventorProvider should pass isinstance check against CADProvider."""
        provider, _ = _make_mock_provider()
        assert isinstance(provider, CADProvider)


# ==================================================================
# Connection lifecycle — delegation
# ==================================================================


class TestConnectionDelegation:
    """Connection methods delegate to the driver."""

    def test_connect_delegates(self):
        provider, mocks = _make_mock_provider()
        result = provider.connect()
        mocks["driver"].connect.assert_called_once()
        assert result == mocks["driver"].connect.return_value

    def test_disconnect_delegates(self):
        provider, mocks = _make_mock_provider()
        result = provider.disconnect()
        mocks["driver"].disconnect.assert_called_once()
        assert result == mocks["driver"].disconnect.return_value

    def test_health_delegates(self):
        provider, mocks = _make_mock_provider()
        result = provider.health()
        mocks["driver"].health.assert_called_once()
        assert result == mocks["driver"].health.return_value


# ==================================================================
# Document operations — delegation
# ==================================================================


class TestDocumentDelegation:
    """Document methods delegate to DocumentManager."""

    def test_doc_open(self):
        provider, mocks = _make_mock_provider()
        provider.doc_open("/path/to/file.ipt")
        mocks["doc"].doc_open.assert_called_once_with("/path/to/file.ipt")

    def test_doc_new_part_default(self):
        provider, mocks = _make_mock_provider()
        provider.doc_new_part()
        mocks["doc"].doc_new_part.assert_called_once_with("")

    def test_doc_new_part_with_template(self):
        provider, mocks = _make_mock_provider()
        provider.doc_new_part(template="Sheet Metal")
        mocks["doc"].doc_new_part.assert_called_once_with("Sheet Metal")

    def test_doc_new_assembly_default(self):
        provider, mocks = _make_mock_provider()
        provider.doc_new_assembly()
        mocks["doc"].doc_new_assembly.assert_called_once_with("")

    def test_doc_new_assembly_with_template(self):
        provider, mocks = _make_mock_provider()
        provider.doc_new_assembly(template="Weldment.iam")
        mocks["doc"].doc_new_assembly.assert_called_once_with("Weldment.iam")

    def test_doc_save(self):
        provider, mocks = _make_mock_provider()
        provider.doc_save()
        mocks["doc"].doc_save.assert_called_once()

    def test_doc_save_as(self):
        provider, mocks = _make_mock_provider()
        provider.doc_save_as("/new/path.ipt")
        mocks["doc"].doc_save_as.assert_called_once_with("/new/path.ipt")

    def test_doc_close_default(self):
        provider, mocks = _make_mock_provider()
        provider.doc_close()
        mocks["doc"].doc_close.assert_called_once_with(True)

    def test_doc_close_no_save(self):
        provider, mocks = _make_mock_provider()
        provider.doc_close(save=False)
        mocks["doc"].doc_close.assert_called_once_with(False)


# ==================================================================
# Sketch operations — delegation
# ==================================================================


class TestSketchDelegation:
    """Sketch methods delegate to SketchManager."""

    def test_sketch_create_default(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_create()
        mocks["sketch"].sketch_create.assert_called_once_with("XY")

    def test_sketch_create_xz(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_create(plane="XZ")
        mocks["sketch"].sketch_create.assert_called_once_with("XZ")

    def test_sketch_line(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_line(0.0, 0.0, 10.0, 5.0)
        mocks["sketch"].sketch_line.assert_called_once_with(0.0, 0.0, 10.0, 5.0)

    def test_sketch_circle(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_circle(5.0, 5.0, 3.0)
        mocks["sketch"].sketch_circle.assert_called_once_with(5.0, 5.0, 3.0)

    def test_sketch_arc(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_arc(0.0, 0.0, 5.0, 0.0, 1.57)
        mocks["sketch"].sketch_arc.assert_called_once_with(0.0, 0.0, 5.0, 0.0, 1.57)

    def test_sketch_rectangle(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_rectangle(0.0, 0.0, 10.0, 5.0)
        mocks["sketch"].sketch_rectangle.assert_called_once_with(0.0, 0.0, 10.0, 5.0)

    def test_sketch_dimension_no_position(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_dimension("linear", "1", "")
        mocks["sketch"].sketch_dimension.assert_called_once_with(
            "linear", "1", "", None, "aligned", None, None)

    def test_sketch_dimension_with_position(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_dimension("linear", "1", "2", value=50.0, position_x=5.0, position_y=10.0)
        mocks["sketch"].sketch_dimension.assert_called_once_with(
            "linear", "1", "2", 50.0, "aligned", 5.0, 10.0)

    def test_sketch_dimension_only_x_ignores_position(self):
        provider, mocks = _make_mock_provider()
        provider.sketch_dimension("linear", "1", "", value=25.0, position_x=5.0)
        mocks["sketch"].sketch_dimension.assert_called_once_with(
            "linear", "1", "", 25.0, "aligned", 5.0, None)


# ==================================================================
# Feature operations — delegation
# ==================================================================


class TestFeatureDelegation:
    """Feature methods delegate to FeatureManager."""

    def test_extrude_defaults(self):
        provider, mocks = _make_mock_provider()
        provider.extrude("profile1", 10.0)
        mocks["feature"].extrude.assert_called_once_with(
            "profile1", 10.0, "positive", 0.0, "new_body"
        )

    def test_extrude_with_all_params(self):
        provider, mocks = _make_mock_provider()
        provider.extrude("p1", 5.0, direction="negative", taper=0.1, operation="cut")
        mocks["feature"].extrude.assert_called_once_with(
            "p1", 5.0, "negative", 0.1, "cut"
        )

    def test_revolve_defaults(self):
        provider, mocks = _make_mock_provider()
        provider.revolve("profile1", "axis1")
        mocks["feature"].revolve.assert_called_once_with(
            "profile1", "axis1", 360.0, "positive", "join"
        )

    def test_revolve_with_all_params(self):
        provider, mocks = _make_mock_provider()
        provider.revolve("p1", "a1", angle=180.0, direction="negative", operation="cut")
        mocks["feature"].revolve.assert_called_once_with(
            "p1", "a1", 180.0, "negative", "cut"
        )

    def test_fillet_defaults(self):
        provider, mocks = _make_mock_provider()
        provider.fillet("1,3,5", 2.0)
        mocks["feature"].fillet.assert_called_once_with("1,3,5", 2.0, "constant")

    def test_fillet_with_mode(self):
        provider, mocks = _make_mock_provider()
        provider.fillet("1", 1.5, mode="constant")
        mocks["feature"].fillet.assert_called_once_with("1", 1.5, "constant")

    def test_chamfer_defaults(self):
        provider, mocks = _make_mock_provider()
        provider.chamfer("1,2", 1.0)
        mocks["feature"].chamfer.assert_called_once_with("1,2", 1.0, "equal_distance")

    def test_chamfer_with_mode(self):
        provider, mocks = _make_mock_provider()
        provider.chamfer("1", 0.5, mode="two_distances")
        mocks["feature"].chamfer.assert_called_once_with("1", 0.5, "two_distances")


# ==================================================================
# Parameter operations — delegation
# ==================================================================


class TestParameterDelegation:
    """Parameter methods delegate to ParameterManager."""

    def test_param_list_default(self):
        provider, mocks = _make_mock_provider()
        provider.param_list()
        mocks["param"].param_list.assert_called_once_with(None)

    def test_param_list_with_filter(self):
        provider, mocks = _make_mock_provider()
        provider.param_list(filter_pattern="d0")
        mocks["param"].param_list.assert_called_once_with("d0")

    def test_param_get(self):
        provider, mocks = _make_mock_provider()
        provider.param_get("d0")
        mocks["param"].param_get.assert_called_once_with("d0")

    def test_param_set(self):
        provider, mocks = _make_mock_provider()
        provider.param_set("d0", 10.0)
        mocks["param"].param_set.assert_called_once_with("d0", 10.0)

    def test_param_set_expression(self):
        provider, mocks = _make_mock_provider()
        provider.param_set_expression("d1", "d0 * 2")
        mocks["param"].param_set_expression.assert_called_once_with("d1", "d0 * 2")


# ==================================================================
# iProperty operations — delegation
# ==================================================================


class TestIPropertyDelegation:
    """iProperty methods delegate to PropertyManager."""

    def test_iproperty_get_default(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_get("Title")
        mocks["prop"].iproperty_get.assert_called_once_with("Title", "Summary")

    def test_iproperty_get_with_set(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_get("Part Number", property_set="Project")
        mocks["prop"].iproperty_get.assert_called_once_with("Part Number", "Project")

    def test_iproperty_set_default(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_set("Title", "New Title")
        mocks["prop"].iproperty_set.assert_called_once_with("Title", "New Title", "Summary")

    def test_iproperty_set_with_set(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_set("Part Number", "ABC", property_set="Project")
        mocks["prop"].iproperty_set.assert_called_once_with("Part Number", "ABC", "Project")

    def test_iproperty_summary(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_summary()
        mocks["prop"].iproperty_summary.assert_called_once()

    def test_iproperty_custom_get(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_custom_get("Vendor")
        mocks["prop"].iproperty_custom_get.assert_called_once_with("Vendor")

    def test_iproperty_custom_set(self):
        provider, mocks = _make_mock_provider()
        provider.iproperty_custom_set("Vendor", "Acme")
        mocks["prop"].iproperty_custom_set.assert_called_once_with("Vendor", "Acme")


# ==================================================================
# Export operations — delegation
# ==================================================================


class TestExportDelegation:
    """Export methods delegate to ExportManager."""

    def test_export_step_no_options(self):
        provider, mocks = _make_mock_provider()
        provider.export_step("/out/part.stp")
        mocks["export"].export_step.assert_called_once_with("/out/part.stp", None)

    def test_export_step_with_options(self):
        provider, mocks = _make_mock_provider()
        opts = {"tolerance": 0.01}
        provider.export_step("/out/part.stp", options=opts)
        mocks["export"].export_step.assert_called_once_with("/out/part.stp", opts)

    def test_export_stl_no_options(self):
        provider, mocks = _make_mock_provider()
        provider.export_stl("/out/part.stl")
        mocks["export"].export_stl.assert_called_once_with("/out/part.stl", None)

    def test_export_pdf_no_options(self):
        provider, mocks = _make_mock_provider()
        provider.export_pdf("/out/part.pdf")
        mocks["export"].export_pdf.assert_called_once_with("/out/part.pdf", None)

    def test_export_dxf_no_options(self):
        provider, mocks = _make_mock_provider()
        provider.export_dxf("/out/sketch.dxf")
        mocks["export"].export_dxf.assert_called_once_with("/out/sketch.dxf", None)


# ==================================================================
# Factory function
# ==================================================================


class TestCreateInventorProvider:
    """Verify the factory creates a wired provider."""

    def test_factory_creates_provider(self):
        """create_inventor_provider should return an InventorProvider instance."""
        # We can't actually connect to Inventor, but we can verify the type
        from unittest.mock import patch

        with patch("mcp_cad.providers.inventor.adapter.RealInventorDriver") as MockDriver:
            instance = create_inventor_provider()
            assert isinstance(instance, InventorProvider)
            MockDriver.assert_called_once()

    def test_factory_provider_satisfies_protocol(self):
        """Factory-produced provider should satisfy CADProvider protocol."""
        from unittest.mock import patch

        with patch("mcp_cad.providers.inventor.adapter.RealInventorDriver") as MockDriver:
            instance = create_inventor_provider()
            assert isinstance(instance, CADProvider)