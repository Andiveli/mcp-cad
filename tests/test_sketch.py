"""Unit tests for Inventor 2D sketch operations.

All tests run on plain mocks — no real Inventor installation required.
Follows the same patterns as test_client.py.
"""

from __future__ import annotations

from unittest.mock import MagicMock, call

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.inventor.sketch import SketchManager
from tests.conftest import make_mock_driver


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_sketch_manager(mock_inventor: MagicMock) -> SketchManager:
    """Wire up a SketchManager with the given mock COM object."""
    driver = make_mock_driver(mock_inventor)
    return SketchManager(driver)


def _setup_active_document_with_sketch(
    mock_inventor: MagicMock, plane: str = "XY"
) -> MagicMock:
    """Configure mock_inventor to have an active document with a sketch.

    Creates the full COM mock chain:
        inventor.ActiveDocument.ComponentDefinition.WorkPlanes.Item(i)
        inventor.ActiveDocument.ComponentDefinition.Sketches.Add(plane)
        inventor.TransientGeometry.CreatePoint2d(x, y)

    Returns the mock sketch object.
    """
    mock_doc = MagicMock()
    mock_comp_def = MagicMock()
    mock_work_planes = MagicMock()
    mock_sketches = MagicMock()
    mock_sketch = MagicMock()
    mock_sketch.Name = "Sketch1"
    mock_tg = MagicMock()

    mock_inventor.ActiveDocument = mock_doc
    mock_doc.ComponentDefinition = mock_comp_def
    mock_comp_def.WorkPlanes = mock_work_planes
    mock_comp_def.Sketches = mock_sketches
    mock_sketches.Add.return_value = mock_sketch
    mock_inventor.TransientGeometry = mock_tg

    # CreatePoint2d returns a fresh mock point each time
    mock_tg.CreatePoint2d.side_effect = lambda x, y: MagicMock(
        name=f"Point2d({x},{y})"
    )

    return mock_sketch


# ==================================================================
# sketch_create
# ==================================================================


class TestSketchCreate:
    """Tests for sketch_create()."""

    def test_create_on_xy_plane(self, mock_inventor: MagicMock):
        """Should create a sketch on the XY plane (default)."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)

        result = mgr.sketch_create()

        assert result["success"] is True
        assert result["plane"] == "XY"
        assert result["sketch_name"] == "Sketch1"
        # Verify WorkPlanes.Item was called with index 1 (XY)
        mock_inventor.ActiveDocument.ComponentDefinition.WorkPlanes.Item.assert_called_once_with(
            1
        )
        # Verify Sketches.Add was called with the work plane
        mock_inventor.ActiveDocument.ComponentDefinition.Sketches.Add.assert_called_once()

    def test_create_on_xz_plane(self, mock_inventor: MagicMock):
        """Should create a sketch on the XZ plane."""
        mgr = _make_sketch_manager(mock_inventor)
        _setup_active_document_with_sketch(mock_inventor)
        result = mgr.sketch_create(plane="XZ")

        assert result["plane"] == "XZ"
        mock_inventor.ActiveDocument.ComponentDefinition.WorkPlanes.Item.assert_called_once_with(
            2
        )

    def test_create_on_yz_plane(self, mock_inventor: MagicMock):
        """Should create a sketch on the YZ plane."""
        mgr = _make_sketch_manager(mock_inventor)
        _setup_active_document_with_sketch(mock_inventor)
        result = mgr.sketch_create(plane="YZ")

        assert result["plane"] == "YZ"
        mock_inventor.ActiveDocument.ComponentDefinition.WorkPlanes.Item.assert_called_once_with(
            3
        )

    def test_create_plane_case_insensitive(self, mock_inventor: MagicMock):
        """Should accept lowercase plane names."""
        mgr = _make_sketch_manager(mock_inventor)
        _setup_active_document_with_sketch(mock_inventor)
        result = mgr.sketch_create(plane="xy")

        assert result["plane"] == "XY"

    def test_create_invalid_plane(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for an invalid plane name."""
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Invalid plane"):
            mgr.sketch_create(plane="AB")

    def test_create_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.sketch_create()

    def test_create_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mock_doc = MagicMock()
        mock_inventor.ActiveDocument = mock_doc
        mock_doc.ComponentDefinition.Sketches.Add.side_effect = Exception(
            "COM error"
        )
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to create sketch"):
            mgr.sketch_create()

    def test_create_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        driver = make_mock_driver(None)
        mgr = SketchManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.sketch_create()

    def test_create_sets_active_sketch(self, mock_inventor: MagicMock):
        """Should set the active sketch for subsequent draw calls."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mgr.sketch_create()
        assert mgr._active_sketch is sketch


# ==================================================================
# sketch_line
# ==================================================================


class TestSketchLine:
    """Tests for sketch_line()."""

    def test_line_success(self, mock_inventor: MagicMock):
        """Should draw a line using TransientGeometry.CreatePoint2d."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mgr.sketch_create()  # set active sketch

        result = mgr.sketch_line(0.0, 0.0, 10.0, 5.0)

        assert result["success"] is True
        assert result["entity_type"] == "line"
        assert result["start"] == [0.0, 0.0]
        assert result["end"] == [10.0, 5.0]
        sketch.SketchLines.AddAsTwoPoint.assert_called_once()
        # Verify CreatePoint2d was called for both points
        tg = mock_inventor.TransientGeometry
        tg.CreatePoint2d.assert_any_call(0.0, 0.0)
        tg.CreatePoint2d.assert_any_call(10.0, 5.0)

    def test_line_no_active_sketch(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active sketch."""
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active sketch"):
            mgr.sketch_line(0.0, 0.0, 10.0, 5.0)

    def test_line_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        sketch.SketchLines.AddAsTwoPoint.side_effect = Exception("COM error")
        mgr.sketch_create()

        with pytest.raises(InventorCOMError, match="Failed to draw line"):
            mgr.sketch_line(0.0, 0.0, 10.0, 5.0)


# ==================================================================
# sketch_circle
# ==================================================================


class TestSketchCircle:
    """Tests for sketch_circle()."""

    def test_circle_success(self, mock_inventor: MagicMock):
        """Should draw a circle using AddByCenterRadius."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mgr.sketch_create()

        result = mgr.sketch_circle(5.0, 5.0, 3.0)

        assert result["success"] is True
        assert result["entity_type"] == "circle"
        assert result["center"] == [5.0, 5.0]
        assert result["radius"] == 3.0
        tg = mock_inventor.TransientGeometry
        tg.CreatePoint2d.assert_any_call(5.0, 5.0)
        sketch.SketchCircles.AddByCenterRadius.assert_called_once()

    def test_circle_no_active_sketch(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active sketch."""
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active sketch"):
            mgr.sketch_circle(0.0, 0.0, 5.0)

    def test_circle_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        sketch.SketchCircles.AddByCenterRadius.side_effect = Exception(
            "COM error"
        )
        mgr.sketch_create()

        with pytest.raises(InventorCOMError, match="Failed to draw circle"):
            mgr.sketch_circle(0.0, 0.0, 5.0)


# ==================================================================
# sketch_arc
# ==================================================================


class TestSketchArc:
    """Tests for sketch_arc()."""

    def test_arc_success(self, mock_inventor: MagicMock):
        """Should draw an arc using AddByCenterStartEndAngle."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mgr.sketch_create()

        result = mgr.sketch_arc(0.0, 0.0, 5.0, 0.0, 1.5708)

        assert result["success"] is True
        assert result["entity_type"] == "arc"
        assert result["center"] == [0.0, 0.0]
        assert result["radius"] == 5.0
        assert result["start_angle"] == 0.0
        assert result["end_angle"] == 1.5708
        tg = mock_inventor.TransientGeometry
        tg.CreatePoint2d.assert_any_call(0.0, 0.0)
        sketch.SketchArcs.AddByCenterStartEndAngle.assert_called_once()

    def test_arc_no_active_sketch(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active sketch."""
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active sketch"):
            mgr.sketch_arc(0.0, 0.0, 5.0, 0.0, 3.14)

    def test_arc_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        sketch.SketchArcs.AddByCenterStartEndAngle.side_effect = Exception(
            "COM error"
        )
        mgr.sketch_create()

        with pytest.raises(InventorCOMError, match="Failed to draw arc"):
            mgr.sketch_arc(0.0, 0.0, 5.0, 0.0, 3.14)


# ==================================================================
# sketch_rectangle
# ==================================================================


class TestSketchRectangle:
    """Tests for sketch_rectangle()."""

    def test_rectangle_success(self, mock_inventor: MagicMock):
        """Should draw a rectangle using AddAsTwoPointRectangle."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mgr.sketch_create()

        result = mgr.sketch_rectangle(0.0, 0.0, 10.0, 5.0)

        assert result["success"] is True
        assert result["entity_type"] == "rectangle"
        assert result["corner1"] == [0.0, 0.0]
        assert result["corner2"] == [10.0, 5.0]
        tg = mock_inventor.TransientGeometry
        tg.CreatePoint2d.assert_any_call(0.0, 0.0)
        tg.CreatePoint2d.assert_any_call(10.0, 5.0)
        sketch.SketchLines.AddAsTwoPointRectangle.assert_called_once()

    def test_rectangle_no_active_sketch(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active sketch."""
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active sketch"):
            mgr.sketch_rectangle(0.0, 0.0, 10.0, 5.0)

    def test_rectangle_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        sketch.SketchLines.AddAsTwoPointRectangle.side_effect = Exception(
            "COM error"
        )
        mgr.sketch_create()

        with pytest.raises(InventorCOMError, match="Failed to draw rectangle"):
            mgr.sketch_rectangle(0.0, 0.0, 10.0, 5.0)


# ==================================================================
# sketch_dimension
# ==================================================================


class TestSketchDimension:
    """Tests for sketch_dimension()."""

    def test_dimension_success(self, mock_inventor: MagicMock):
        """Should add a dimension constraint with value."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mock_dim = MagicMock()
        sketch.DimensionConstraints.AddLinearDimension.return_value = mock_dim
        mgr.sketch_create()

        result = mgr.sketch_dimension("Line1", 25.0)

        assert result["success"] is True
        assert result["entity_type"] == "dimension"
        assert result["entity"] == "Line1"
        assert result["value"] == 25.0
        sketch.DimensionConstraints.AddLinearDimension.assert_called_once_with(
            "Line1"
        )
        mock_dim.Parameter.Value = 25.0

    def test_dimension_with_position(self, mock_inventor: MagicMock):
        """Should add a dimension with text position."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        mock_dim = MagicMock()
        sketch.DimensionConstraints.AddLinearDimension.return_value = mock_dim
        mgr.sketch_create()

        result = mgr.sketch_dimension("Line1", 50.0, position=(5.0, 10.0))

        assert result["success"] is True
        assert result["value"] == 50.0
        # Should have called CreatePoint2d for the position
        tg = mock_inventor.TransientGeometry
        tg.CreatePoint2d.assert_any_call(5.0, 10.0)
        # AddLinearDimension should be called with entity and text_pos
        sketch.DimensionConstraints.AddLinearDimension.assert_called_once()

    def test_dimension_no_active_sketch(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active sketch."""
        mgr = _make_sketch_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active sketch"):
            mgr.sketch_dimension("Line1", 25.0)

    def test_dimension_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_sketch_manager(mock_inventor)
        sketch = _setup_active_document_with_sketch(mock_inventor)
        sketch.DimensionConstraints.AddLinearDimension.side_effect = Exception(
            "COM error"
        )
        mgr.sketch_create()

        with pytest.raises(InventorCOMError, match="Failed to add dimension"):
            mgr.sketch_dimension("Line1", 25.0)


# ==================================================================
# Not-connected guard (applies to all methods)
# ==================================================================


class TestSketchNotConnected:
    """All sketch methods must raise when the inventor reference is None."""

    def test_line_not_connected(self):
        driver = make_mock_driver(None)
        mgr = SketchManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.sketch_line(0, 0, 1, 1)

    def test_circle_not_connected(self):
        driver = make_mock_driver(None)
        mgr = SketchManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.sketch_circle(0, 0, 5)

    def test_arc_not_connected(self):
        driver = make_mock_driver(None)
        mgr = SketchManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.sketch_arc(0, 0, 5, 0, 3.14)

    def test_rectangle_not_connected(self):
        driver = make_mock_driver(None)
        mgr = SketchManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.sketch_rectangle(0, 0, 10, 5)

    def test_dimension_not_connected(self):
        driver = make_mock_driver(None)
        mgr = SketchManager(driver)
        with pytest.raises(InventorDisconnectedError):
            mgr.sketch_dimension("Line1", 25.0)
