"""Unit tests for Inventor iProperty operations.

All tests run on plain mocks — no real Inventor installation required.
Follows the same patterns as test_document.py and test_sketch.py.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.providers.inventor.property import PropertyManager
from tests.conftest import make_mock_driver


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_property_manager(mock_inventor: MagicMock) -> PropertyManager:
    """Wire up a PropertyManager with the given mock COM object."""
    driver = make_mock_driver(mock_inventor)
    return PropertyManager(driver)


def _make_disconnected_manager() -> PropertyManager:
    """Wire up a PropertyManager with no COM reference."""
    driver = make_mock_driver(None)
    return PropertyManager(driver)


def _setup_active_document_with_properties(
    mock_inventor: MagicMock,
) -> dict[str, MagicMock]:
    """Configure mock_inventor with an active document and PropertySets.

    Returns a dict with mock objects for assertion access.
    """
    mock_doc = MagicMock()
    mock_prop_sets = MagicMock()

    mock_inventor.ActiveDocument = mock_doc
    mock_doc.PropertySets = mock_prop_sets

    # Set up the three standard property sets
    mock_summary_set = MagicMock()
    mock_project_set = MagicMock()
    mock_custom_set = MagicMock()

    mock_prop_sets.Item.side_effect = lambda name: {
        "Inventor Summary Information": mock_summary_set,
        "Inventor Document Summary Information": mock_project_set,
        "Design Tracking Properties": mock_custom_set,
    }[name]

    return {
        "doc": mock_doc,
        "prop_sets": mock_prop_sets,
        "summary_set": mock_summary_set,
        "project_set": mock_project_set,
        "custom_set": mock_custom_set,
    }


def _make_mock_property(name: str, value: object) -> MagicMock:
    """Build a mock Inventor iProperty COM object."""
    prop = MagicMock()
    prop.Name = name
    prop.Value = value
    return prop


# ==================================================================
# iproperty_get
# ==================================================================


class TestIPropertyGet:
    """Tests for iproperty_get()."""

    def test_get_summary_property(self, mock_inventor: MagicMock):
        """Should get a property from the Summary set (default)."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Title", "My Part")
        mocks["summary_set"].Item.return_value = prop

        result = mgr.iproperty_get("Title")

        assert result["success"] is True
        assert result["name"] == "Title"
        assert result["value"] == "My Part"
        assert result["property_set"] == "Summary"
        mocks["summary_set"].Item.assert_called_once_with("Title")

    def test_get_project_property(self, mock_inventor: MagicMock):
        """Should get a property from the Project set."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Part Number", "ABC-123")
        mocks["project_set"].Item.return_value = prop

        result = mgr.iproperty_get("Part Number", property_set="Project")

        assert result["success"] is True
        assert result["name"] == "Part Number"
        assert result["value"] == "ABC-123"
        assert result["property_set"] == "Project"

    def test_get_custom_property(self, mock_inventor: MagicMock):
        """Should get a property from the Custom set."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Material", "Steel")
        mocks["custom_set"].Item.return_value = prop

        result = mgr.iproperty_get("Material", property_set="Custom")

        assert result["success"] is True
        assert result["value"] == "Steel"
        assert result["property_set"] == "Custom"

    def test_get_invalid_property_set(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid property set."""
        mgr = _make_property_manager(mock_inventor)

        with pytest.raises(InventorCOMError, match="Invalid property set"):
            mgr.iproperty_get("Title", property_set="Invalid")

    def test_get_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when property not found."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        mocks["summary_set"].Item.side_effect = Exception("Not found")

        with pytest.raises(InventorCOMError, match="Failed to get iProperty"):
            mgr.iproperty_get("Nonexistent")

    def test_get_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_property_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.iproperty_get("Title")

    def test_get_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.iproperty_get("Title")


# ==================================================================
# iproperty_set
# ==================================================================


class TestIPropertySet:
    """Tests for iproperty_set()."""

    def test_set_summary_property(self, mock_inventor: MagicMock):
        """Should set a property in the Summary set."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Title", "Old Title")
        mocks["summary_set"].Item.return_value = prop

        result = mgr.iproperty_set("Title", "New Title")

        assert result["success"] is True
        assert result["name"] == "Title"
        assert result["value"] == "New Title"
        assert result["property_set"] == "Summary"
        assert prop.Value == "New Title"

    def test_set_project_property(self, mock_inventor: MagicMock):
        """Should set a property in the Project set."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Part Number", "OLD")
        mocks["project_set"].Item.return_value = prop

        result = mgr.iproperty_set("Part Number", "NEW-001", property_set="Project")

        assert result["success"] is True
        assert result["value"] == "NEW-001"
        assert result["property_set"] == "Project"

    def test_set_invalid_property_set(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError for invalid property set."""
        mgr = _make_property_manager(mock_inventor)

        with pytest.raises(InventorCOMError, match="Invalid property set"):
            mgr.iproperty_set("Title", "Value", property_set="BadSet")

    def test_set_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        mocks["summary_set"].Item.side_effect = Exception("COM error")

        with pytest.raises(InventorCOMError, match="Failed to set iProperty"):
            mgr.iproperty_set("BadProp", "value")

    def test_set_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_property_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.iproperty_set("Title", "Value")

    def test_set_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.iproperty_set("Title", "Value")


# ==================================================================
# iproperty_summary
# ==================================================================


class TestIPropertySummary:
    """Tests for iproperty_summary()."""

    def test_summary_success(self, mock_inventor: MagicMock):
        """Should return all Summary iProperties."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        p1 = _make_mock_property("Title", "My Part")
        p2 = _make_mock_property("Subject", "Test subject")
        mocks["summary_set"].Count = 2
        mocks["summary_set"].Item.side_effect = lambda i: [p1, p2][i - 1]

        result = mgr.iproperty_summary()

        assert result["success"] is True
        assert len(result["properties"]) == 2
        assert result["properties"][0]["name"] == "Title"
        assert result["properties"][0]["value"] == "My Part"
        assert result["properties"][1]["name"] == "Subject"
        assert result["properties"][1]["value"] == "Test subject"

    def test_summary_empty(self, mock_inventor: MagicMock):
        """Should return empty list when no summary properties."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        mocks["summary_set"].Count = 0

        result = mgr.iproperty_summary()

        assert result["success"] is True
        assert result["properties"] == []

    def test_summary_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        mocks["summary_set"].Count = 1
        mocks["summary_set"].Item.side_effect = Exception("COM error")

        with pytest.raises(InventorCOMError, match="Failed to get summary"):
            mgr.iproperty_summary()

    def test_summary_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_property_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.iproperty_summary()

    def test_summary_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.iproperty_summary()


# ==================================================================
# iproperty_custom_get
# ==================================================================


class TestIPropertyCustomGet:
    """Tests for iproperty_custom_get()."""

    def test_custom_get_success(self, mock_inventor: MagicMock):
        """Should get a custom iProperty."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Vendor", "Acme Corp")
        mocks["custom_set"].Item.return_value = prop

        result = mgr.iproperty_custom_get("Vendor")

        assert result["success"] is True
        assert result["name"] == "Vendor"
        assert result["value"] == "Acme Corp"
        mocks["custom_set"].Item.assert_called_once_with("Vendor")

    def test_custom_get_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when custom property not found."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        mocks["custom_set"].Item.side_effect = Exception("Not found")

        with pytest.raises(InventorCOMError, match="Failed to get custom iProperty"):
            mgr.iproperty_custom_get("Nonexistent")

    def test_custom_get_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_property_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.iproperty_custom_get("Vendor")

    def test_custom_get_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.iproperty_custom_get("Vendor")


# ==================================================================
# iproperty_custom_set
# ==================================================================


class TestIPropertyCustomSet:
    """Tests for iproperty_custom_set()."""

    def test_custom_set_existing(self, mock_inventor: MagicMock):
        """Should update an existing custom iProperty."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        prop = _make_mock_property("Vendor", "Old Vendor")
        mocks["custom_set"].Item.return_value = prop

        result = mgr.iproperty_custom_set("Vendor", "New Vendor")

        assert result["success"] is True
        assert result["name"] == "Vendor"
        assert result["value"] == "New Vendor"
        assert prop.Value == "New Vendor"

    def test_custom_set_new_property(self, mock_inventor: MagicMock):
        """Should create a custom iProperty when it doesn't exist."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        # Item() raises (property doesn't exist), Add() succeeds
        mocks["custom_set"].Item.side_effect = Exception("Not found")
        mocks["custom_set"].Add = MagicMock()

        result = mgr.iproperty_custom_set("NewProp", "value1")

        assert result["success"] is True
        assert result["name"] == "NewProp"
        assert result["value"] == "value1"
        mocks["custom_set"].Add.assert_called_once_with("NewProp", "value1")

    def test_custom_set_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mgr = _make_property_manager(mock_inventor)
        mocks = _setup_active_document_with_properties(mock_inventor)

        # Both Item and Add fail
        mocks["custom_set"].Item.side_effect = Exception("COM error")
        mocks["custom_set"].Add.side_effect = Exception("COM error")

        with pytest.raises(InventorCOMError, match="Failed to set custom iProperty"):
            mgr.iproperty_custom_set("BadProp", "value")

    def test_custom_set_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mgr = _make_property_manager(mock_inventor)
        mock_inventor.ActiveDocument = None

        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.iproperty_custom_set("Vendor", "value")

    def test_custom_set_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.iproperty_custom_set("Vendor", "value")