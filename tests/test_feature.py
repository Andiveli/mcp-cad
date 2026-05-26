"""Unit tests for Inventor 3D feature operations.

All tests run on plain mocks — no real Inventor installation required.
Follows the same patterns as test_document.py and test_sketch.py.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.inventor.feature import FeatureManager
from tests.conftest import make_mock_driver


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_feature_manager(mock_inventor: MagicMock) -> FeatureManager:
    """Wire up a FeatureManager with the given mock COM object."""
    driver = make_mock_driver(mock_inventor)
    return FeatureManager(driver)


def _setup_active_document_with_features(
    mock_inventor: MagicMock,
) -> dict[str, MagicMock]:
    """Configure mock_inventor with an active document and feature collections.

    Returns a dict with all mock objects for assertion access.
    """
    mock_doc = MagicMock()
    mock_comp_def = MagicMock()
    mock_features = MagicMock()
    mock_extrude_features = MagicMock()
    mock_revolve_features = MagicMock()
    mock_fillet_features = MagicMock()
    mock_chamfer_features = MagicMock()
    mock_edges = MagicMock()
    mock_surface_body = MagicMock()

    mock_inventor.ActiveDocument = mock_doc
    mock_doc.ComponentDefinition = mock_comp_def
    mock_comp_def.Features = mock_features
    mock_features.ExtrudeFeatures = mock_extrude_features
    mock_features.RevolveFeatures = mock_revolve_features
    mock_features.FilletFeatures = mock_fillet_features
    mock_features.ChamferFeatures = mock_chamfer_features

    mock_surface_body.Edges = mock_edges
    mock_comp_def.SurfaceBodies = MagicMock()
    mock_comp_def.SurfaceBodies.Item.return_value = mock_surface_body
    mock_surface_body.Edges = mock_edges

    return {
        "doc": mock_doc,
        "comp_def": mock_comp_def,
        "features": mock_features,
        "extrude_features": mock_extrude_features,
        "revolve_features": mock_revolve_features,
        "fillet_features": mock_fillet_features,
        "chamfer_features": mock_chamfer_features,
        "edges": mock_edges,
    }


# ==================================================================
# extrude
# ==================================================================


class TestExtrude:
    """Tests for extrude()."""

    def test_extrude_success(self, mock_inventor: MagicMock):
        """Should create an extrude feature and return metadata."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude(mock_profile, distance=10.0)

        assert result["success"] is True
        assert result["feature_type"] == "extrude"
        assert result["distance"] == 10.0
        assert result["direction"] == "positive"
        assert result["operation"] == "join"
        assert result["taper"] == 0.0
        mocks["extrude_features"].CreateExtrudeDefinition.assert_called_once_with(
            mock_profile, 10.0
        )
        mocks["extrude_features"].Add.assert_called_once_with(mock_extrude_def)
        mock_extrude_def.Direction = 0  # positive
        mock_extrude_def.Operation = 0  # join

    def test_extrude_with_profile_name(self, mock_inventor: MagicMock):
        """Should resolve a string profile name via Profiles.Item."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        # Set up sketch resolution chain
        mock_sketch = MagicMock()
        mock_sketches = MagicMock()
        mock_sketches.Count = 1
        mock_sketches.Item.return_value = mock_sketch
        mock_profile = MagicMock()
        mock_profiles = MagicMock()
        mock_profiles.Item.return_value = mock_profile
        mock_sketch.Profiles = mock_profiles
        mocks["comp_def"].Sketches = mock_sketches

        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude("Profile1", distance=5.0)

        assert result["success"] is True
        mock_sketches.Item.assert_called_once_with(1)
        mock_profiles.Item.assert_called_once_with("Profile1")

    def test_extrude_negative_direction(self, mock_inventor: MagicMock):
        """Should accept negative direction."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude(mock_profile, distance=10.0, direction="negative")

        assert result["direction"] == "negative"
        mock_extrude_def.Direction = 1  # negative

    def test_extrude_both_directions(self, mock_inventor: MagicMock):
        """Should accept both directions."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude(mock_profile, distance=5.0, direction="both")

        assert result["direction"] == "both"
        mock_extrude_def.Direction = 2  # both

    def test_extrude_cut_operation(self, mock_inventor: MagicMock):
        """Should accept cut operation."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude(mock_profile, distance=5.0, operation="cut")

        assert result["operation"] == "cut"
        mock_extrude_def.Operation = 1  # cut

    def test_extrude_with_taper(self, mock_inventor: MagicMock):
        """Should set taper angle when provided."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude(mock_profile, distance=10.0, taper=0.15)

        assert result["taper"] == 0.15
        mock_extrude_def.TaperAngle = 0.15

    def test_extrude_invalid_direction(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid direction."""
        mgr = _make_feature_manager(mock_inventor)
        mock_profile = MagicMock()

        with pytest.raises(InventorCOMError, match="Invalid direction"):
            mgr.extrude(mock_profile, distance=10.0, direction="sideways")

    def test_extrude_invalid_operation(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid operation."""
        mgr = _make_feature_manager(mock_inventor)
        mock_profile = MagicMock()

        with pytest.raises(InventorCOMError, match="Invalid operation"):
            mgr.extrude(mock_profile, distance=10.0, operation="merge")

    def test_extrude_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_feature_manager(mock_inventor)
        _setup_active_document_with_features(mock_inventor)

        # Make CreateExtrudeDefinition raise
        mock_inventor.ActiveDocument.ComponentDefinition.Features.ExtrudeFeatures.CreateExtrudeDefinition.side_effect = (
            Exception("COM error")
        )

        mock_profile = MagicMock()
        with pytest.raises(InventorCOMError, match="Failed to extrude"):
            mgr.extrude(mock_profile, distance=10.0)

    def test_extrude_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_feature_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.extrude(MagicMock(), distance=10.0)

    def test_extrude_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        driver = make_mock_driver(None)
        mgr = FeatureManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.extrude(MagicMock(), distance=10.0)


# ==================================================================
# revolve
# ==================================================================


class TestRevolve:
    """Tests for revolve()."""

    def test_revolve_success(self, mock_inventor: MagicMock):
        """Should create a revolve feature and return metadata."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_axis = MagicMock()
        mock_revolve_def = MagicMock()
        mocks["revolve_features"].CreateRevolveDefinition.return_value = mock_revolve_def

        result = mgr.revolve(mock_profile, axis=mock_axis, angle=270.0)

        assert result["success"] is True
        assert result["feature_type"] == "revolve"
        assert result["angle"] == 270.0
        assert result["operation"] == "join"
        mocks["revolve_features"].CreateRevolveDefinition.assert_called_once_with(
            mock_profile, mock_axis
        )
        mocks["revolve_features"].Add.assert_called_once_with(mock_revolve_def)
        mock_revolve_def.Angle = 270.0
        mock_revolve_def.Operation = 0  # join

    def test_revolve_default_full_angle(self, mock_inventor: MagicMock):
        """Should default to 360 degrees (full revolution)."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_axis = MagicMock()
        mock_revolve_def = MagicMock()
        mocks["revolve_features"].CreateRevolveDefinition.return_value = mock_revolve_def

        result = mgr.revolve(mock_profile, axis=mock_axis)

        assert result["angle"] == 360.0
        mock_revolve_def.Angle = 360.0

    def test_revolve_cut_operation(self, mock_inventor: MagicMock):
        """Should accept cut operation."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_axis = MagicMock()
        mock_revolve_def = MagicMock()
        mocks["revolve_features"].CreateRevolveDefinition.return_value = mock_revolve_def

        result = mgr.revolve(mock_profile, axis=mock_axis, operation="cut")

        assert result["operation"] == "cut"
        mock_revolve_def.Operation = 1  # cut

    def test_revolve_invalid_operation(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid operation."""
        mgr = _make_feature_manager(mock_inventor)
        mock_profile = MagicMock()
        mock_axis = MagicMock()

        with pytest.raises(InventorCOMError, match="Invalid operation"):
            mgr.revolve(mock_profile, axis=mock_axis, operation="merge")

    def test_revolve_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_feature_manager(mock_inventor)
        _setup_active_document_with_features(mock_inventor)

        mock_inventor.ActiveDocument.ComponentDefinition.Features.RevolveFeatures.CreateRevolveDefinition.side_effect = (
            Exception("COM error")
        )

        mock_profile = MagicMock()
        mock_axis = MagicMock()
        with pytest.raises(InventorCOMError, match="Failed to revolve"):
            mgr.revolve(mock_profile, axis=mock_axis)

    def test_revolve_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_feature_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.revolve(MagicMock(), axis=MagicMock())

    def test_revolve_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        driver = make_mock_driver(None)
        mgr = FeatureManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.revolve(MagicMock(), axis=MagicMock())


# ==================================================================
# fillet
# ==================================================================


class TestFillet:
    """Tests for fillet()."""

    def test_fillet_success(self, mock_inventor: MagicMock):
        """Should create a fillet feature and return metadata."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_fillet_def = MagicMock()
        mocks["fillet_features"].CreateFilletDefinition.return_value = mock_fillet_def

        result = mgr.fillet(edges=MagicMock(), radius=2.5)

        assert result["success"] is True
        assert result["feature_type"] == "fillet"
        assert result["radius"] == 2.5
        assert result["mode"] == "constant"
        mocks["fillet_features"].Add.assert_called_once_with(mock_fillet_def)
        mock_fillet_def.Radius = 2.5

    def test_fillet_variable_mode(self, mock_inventor: MagicMock):
        """Should accept variable fillet mode."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_fillet_def = MagicMock()
        mocks["fillet_features"].CreateFilletDefinition.return_value = mock_fillet_def

        result = mgr.fillet(edges=MagicMock(), radius=3.0, mode="variable")

        assert result["mode"] == "variable"
        mocks["fillet_features"].CreateFilletDefinition.assert_called_once()

    def test_fillet_invalid_mode(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid fillet mode."""
        mgr = _make_feature_manager(mock_inventor)

        with pytest.raises(InventorCOMError, match="Invalid fillet mode"):
            mgr.fillet(edges=MagicMock(), radius=2.0, mode="weird")

    def test_fillet_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_feature_manager(mock_inventor)
        _setup_active_document_with_features(mock_inventor)

        mock_inventor.ActiveDocument.ComponentDefinition.Features.FilletFeatures.CreateFilletDefinition.side_effect = (
            Exception("COM error")
        )

        with pytest.raises(InventorCOMError, match="Failed to create fillet"):
            mgr.fillet(edges=MagicMock(), radius=2.0)

    def test_fillet_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_feature_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.fillet(edges=MagicMock(), radius=2.0)

    def test_fillet_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        driver = make_mock_driver(None)
        mgr = FeatureManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.fillet(edges=MagicMock(), radius=2.0)


# ==================================================================
# chamfer
# ==================================================================


class TestChamfer:
    """Tests for chamfer()."""

    def test_chamfer_success(self, mock_inventor: MagicMock):
        """Should create a chamfer feature and return metadata."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_chamfer_def = MagicMock()
        mocks["chamfer_features"].CreateChamferDefinition.return_value = mock_chamfer_def

        result = mgr.chamfer(edges=MagicMock(), distance=1.5)

        assert result["success"] is True
        assert result["feature_type"] == "chamfer"
        assert result["distance"] == 1.5
        assert result["mode"] == "equal_distance"
        mocks["chamfer_features"].Add.assert_called_once_with(mock_chamfer_def)

    def test_chamfer_two_distances_mode(self, mock_inventor: MagicMock):
        """Should accept two_distances chamfer mode."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_chamfer_def = MagicMock()
        mocks["chamfer_features"].CreateChamferDefinition.return_value = mock_chamfer_def

        result = mgr.chamfer(edges=MagicMock(), distance=2.0, mode="two_distances")

        assert result["mode"] == "two_distances"
        mocks["chamfer_features"].CreateChamferDefinition.assert_called_once()

    def test_chamfer_invalid_mode(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid chamfer mode."""
        mgr = _make_feature_manager(mock_inventor)

        with pytest.raises(InventorCOMError, match="Invalid chamfer mode"):
            mgr.chamfer(edges=MagicMock(), distance=1.0, mode="asymmetric")

    def test_chamfer_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_feature_manager(mock_inventor)
        _setup_active_document_with_features(mock_inventor)

        mock_inventor.ActiveDocument.ComponentDefinition.Features.ChamferFeatures.CreateChamferDefinition.side_effect = (
            Exception("COM error")
        )

        with pytest.raises(InventorCOMError, match="Failed to create chamfer"):
            mgr.chamfer(edges=MagicMock(), distance=1.0)

    def test_chamfer_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_feature_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.chamfer(edges=MagicMock(), distance=1.0)

    def test_chamfer_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        driver = make_mock_driver(None)
        mgr = FeatureManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.chamfer(edges=MagicMock(), distance=1.0)


# ==================================================================
# Profile resolution edge-cases
# ==================================================================


class TestFeatureProfileResolution:
    """Tests for profile name resolution."""

    def test_resolve_string_profile_success(self, mock_inventor: MagicMock):
        """Should resolve a string profile name to a COM object."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_sketch = MagicMock()
        mock_sketches = MagicMock()
        mock_sketches.Count = 1
        mock_sketches.Item.return_value = mock_sketch
        mock_profile = MagicMock()
        mock_profiles = MagicMock()
        mock_profiles.Item.return_value = mock_profile
        mock_sketch.Profiles = mock_profiles
        mocks["comp_def"].Sketches = mock_sketches

        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        result = mgr.extrude("MyProfile", distance=5.0)
        assert result["success"] is True

    def test_resolve_string_profile_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when a string profile name can't be resolved."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_sketch = MagicMock()
        mock_sketches = MagicMock()
        mock_sketches.Count = 1
        mock_sketches.Item.return_value = mock_sketch
        mock_sketch.Profiles.Item.side_effect = Exception("Not found")
        mocks["comp_def"].Sketches = mock_sketches

        with pytest.raises(InventorCOMError, match="Failed to resolve profile"):
            mgr.extrude("BadProfile", distance=5.0)

    def test_passthrough_com_profile(self, mock_inventor: MagicMock):
        """Should pass a COM profile object through without resolution."""
        mgr = _make_feature_manager(mock_inventor)
        mocks = _setup_active_document_with_features(mock_inventor)

        mock_profile = MagicMock()
        mock_extrude_def = MagicMock()
        mocks["extrude_features"].CreateExtrudeDefinition.return_value = mock_extrude_def

        # mock_profile is already a MagicMock (i.e. a COM object), not a string
        result = mgr.extrude(mock_profile, distance=10.0)
        assert result["success"] is True
        # Verify CreateExtrudeDefinition was called with the mock_profile directly
        mocks["extrude_features"].CreateExtrudeDefinition.assert_called_once_with(
            mock_profile, 10.0
        )