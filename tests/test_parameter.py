"""Unit tests for Inventor parameter operations.

All tests run on plain mocks — no real Inventor installation required.
Follows the same patterns as test_document.py and test_sketch.py.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.providers.inventor.parameter import ParameterManager
from tests.conftest import make_mock_driver


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_parameter_manager(mock_inventor: MagicMock) -> ParameterManager:
    """Wire up a ParameterManager with the given mock COM object."""
    driver = make_mock_driver(mock_inventor)
    return ParameterManager(driver)


def _setup_active_document_with_parameters(
    mock_inventor: MagicMock,
) -> dict[str, MagicMock]:
    """Configure mock_inventor with an active document and Parameters.

    Returns a dict with mock objects for assertion access.
    """
    mock_doc = MagicMock()
    mock_comp_def = MagicMock()
    mock_params = MagicMock()

    mock_inventor.ActiveDocument = mock_doc
    mock_doc.ComponentDefinition = mock_comp_def
    mock_comp_def.Parameters = mock_params

    return {
        "doc": mock_doc,
        "comp_def": mock_comp_def,
        "params": mock_params,
    }


def _make_disconnected_manager() -> ParameterManager:
    """Wire up a ParameterManager with no COM reference."""
    driver = make_mock_driver(None)
    return ParameterManager(driver)


def _make_mock_parameter(
    name: str, value: float, expression: str
) -> MagicMock:
    """Build a mock Inventor parameter COM object."""
    p = MagicMock()
    p.Name = name
    p.Value = value
    p.Expression = expression
    return p


# ==================================================================
# param_list
# ==================================================================


class TestParamList:
    """Tests for param_list()."""

    def test_list_all_parameters(self, mock_inventor: MagicMock):
        """Should list all model parameters."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        p1 = _make_mock_parameter("d0", 10.0, "10.0")
        p2 = _make_mock_parameter("d1", 5.0, "d0 / 2")
        mocks["params"].Count = 2
        mocks["params"].Item.side_effect = lambda i: [p1, p2][i - 1]

        result = mgr.param_list()

        assert result["success"] is True
        assert len(result["parameters"]) == 2
        assert result["parameters"][0]["name"] == "d0"
        assert result["parameters"][0]["value"] == 10.0
        assert result["parameters"][0]["expression"] == "10.0"
        assert result["parameters"][1]["name"] == "d1"
        assert result["parameters"][1]["expression"] == "d0 / 2"

    def test_list_with_filter(self, mock_inventor: MagicMock):
        """Should filter parameters by case-insensitive substring."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        p1 = _make_mock_parameter("Width", 10.0, "10.0")
        p2 = _make_mock_parameter("Height", 5.0, "5.0")
        p3 = _make_mock_parameter("Depth", 3.0, "3.0")
        mocks["params"].Count = 3
        mocks["params"].Item.side_effect = lambda i: [p1, p2, p3][i - 1]

        result = mgr.param_list(filter_pattern="width")

        assert result["success"] is True
        assert len(result["parameters"]) == 1
        assert result["parameters"][0]["name"] == "Width"

    def test_list_empty_parameters(self, mock_inventor: MagicMock):
        """Should return empty list when no parameters exist."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        mocks["params"].Count = 0

        result = mgr.param_list()

        assert result["success"] is True
        assert result["parameters"] == []

    def test_list_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_parameter_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.param_list()

    def test_list_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.param_list()

    def test_list_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        # Make iterating parameters fail
        mocks["params"].Count = 1
        mocks["params"].Item.side_effect = Exception("COM error")

        with pytest.raises(InventorCOMError, match="Failed to list parameters"):
            mgr.param_list()


# ==================================================================
# param_get
# ==================================================================


class TestParamGet:
    """Tests for param_get()."""

    def test_get_parameter_success(self, mock_inventor: MagicMock):
        """Should get a parameter by name."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        p = _make_mock_parameter("d0", 10.0, "10.0")
        mocks["params"].Item.return_value = p

        result = mgr.param_get("d0")

        assert result["success"] is True
        assert result["name"] == "d0"
        assert result["value"] == 10.0
        assert result["expression"] == "10.0"
        mocks["params"].Item.assert_called_once_with("d0")

    def test_get_parameter_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when parameter not found."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        mocks["params"].Item.side_effect = Exception("Parameter not found")

        with pytest.raises(InventorCOMError, match="Failed to get parameter"):
            mgr.param_get("nonexistent")

    def test_get_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_parameter_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.param_get("d0")

    def test_get_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.param_get("d0")


# ==================================================================
# param_set
# ==================================================================


class TestParamSet:
    """Tests for param_set()."""

    def test_set_parameter_success(self, mock_inventor: MagicMock):
        """Should set a parameter value and return confirmation."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        p = _make_mock_parameter("d0", 15.0, "15.0")
        mocks["params"].Item.return_value = p

        result = mgr.param_set("d0", 15.0)

        assert result["success"] is True
        assert result["name"] == "d0"
        assert result["value"] == 15.0
        # Verify the .Value was set on the parameter
        assert p.Value == 15.0

    def test_set_parameter_int_value(self, mock_inventor: MagicMock):
        """Should accept integer values."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        p = _make_mock_parameter("d0", 5, "5")
        mocks["params"].Item.return_value = p

        result = mgr.param_set("d0", 5)

        assert result["success"] is True
        assert result["value"] == 5

    def test_set_parameter_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when parameter not found."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        mocks["params"].Item.side_effect = Exception("Parameter not found")

        with pytest.raises(InventorCOMError, match="Failed to set parameter"):
            mgr.param_set("nonexistent", 5.0)

    def test_set_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_parameter_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.param_set("d0", 5.0)

    def test_set_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.param_set("d0", 5.0)


# ==================================================================
# param_set_expression
# ==================================================================


class TestParamSetExpression:
    """Tests for param_set_expression()."""

    def test_set_expression_success(self, mock_inventor: MagicMock):
        """Should set a parameter expression and return confirmation."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        p = _make_mock_parameter("d1", 5.0, "d0 * 2")
        mocks["params"].Item.return_value = p

        result = mgr.param_set_expression("d1", "d0 * 2")

        assert result["success"] is True
        assert result["name"] == "d1"
        assert result["expression"] == "d0 * 2"
        assert p.Expression == "d0 * 2"

    def test_set_expression_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when parameter not found."""
        mgr = _make_parameter_manager(mock_inventor)
        mocks = _setup_active_document_with_parameters(mock_inventor)

        mocks["params"].Item.side_effect = Exception("Parameter not found")

        with pytest.raises(InventorCOMError, match="Failed to set expression"):
            mgr.param_set_expression("nonexistent", "d0 * 2")

    def test_set_expression_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_parameter_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.param_set_expression("d0", "5.0")

    def test_set_expression_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.param_set_expression("d0", "5.0")