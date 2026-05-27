"""Unit tests for Inventor document operations.

All tests run on plain mocks — no real Inventor installation required.
Follows the same patterns as test_client.py.
"""

from __future__ import annotations

from unittest.mock import MagicMock

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.inventor.document import DocumentManager
from tests.conftest import make_mock_driver


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_doc_manager(mock_inventor: MagicMock) -> DocumentManager:
    """Wire up a DocumentManager with the given mock COM object."""
    driver = make_mock_driver(mock_inventor)
    return DocumentManager(driver)


def _make_disconnected_manager() -> DocumentManager:
    """Wire up a DocumentManager with no COM reference (driver.inventor is None)."""
    driver = make_mock_driver(None)
    return DocumentManager(driver)


def _make_mock_doc(
    fullfilename: str = r"C:\Models\Part1.ipt",
    doc_type: int = 12290,
) -> MagicMock:
    """Build a mock Inventor document COM object."""
    doc = MagicMock()
    doc.FullFileName = fullfilename
    doc.DocumentType = doc_type
    return doc


# ==================================================================
# doc_open
# ==================================================================


class TestDocOpen:
    """Happy path, error handling, and edge cases for doc_open()."""

    def test_open_success(self, mock_inventor: MagicMock):
        """Should open a document and return metadata."""
        mock_doc = _make_mock_doc(r"C:\Models\bracket.ipt")
        mock_inventor.Documents.Open.return_value = mock_doc
        mgr = _make_doc_manager(mock_inventor)

        result = mgr.doc_open(r"C:\Models\bracket.ipt")

        assert result["success"] is True
        assert result["document"] == r"C:\Models\bracket.ipt"
        mock_inventor.Documents.Open.assert_called_once_with(
            r"C:\Models\bracket.ipt"
        )

    def test_open_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mock_inventor.Documents.Open.side_effect = Exception("File not found")
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to open"):
            mgr.doc_open(r"C:\Models\nothing.ipt")

    def test_open_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.doc_open(r"C:\Models\part.ipt")


# ==================================================================
# doc_new_part
# ==================================================================


class TestDocNewPart:
    """Tests for doc_new_part()."""

    def test_new_part_success(self, mock_inventor: MagicMock):
        """Should create a part document and return metadata."""
        mock_doc = _make_mock_doc(doc_type=12290)
        mock_inventor.Documents.Add.return_value = mock_doc
        mock_inventor.FileManager.GetTemplateFile.return_value = r"C:\Templates\Standard.ipt"
        mgr = _make_doc_manager(mock_inventor)
        result = mgr.doc_new_part()

        assert result["success"] is True
        assert result["document_type"] == 12290
        mock_inventor.FileManager.GetTemplateFile.assert_called_once_with(12290)
        mock_inventor.Documents.Add.assert_called_once_with(12290, r"C:\Templates\Standard.ipt", True)

    def test_new_part_with_template(self, mock_inventor: MagicMock):
        """Should pass custom template path to Documents.Add."""
        mock_doc = _make_mock_doc()
        mock_inventor.Documents.Add.return_value = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        mgr.doc_new_part(template=r"C:\Templates\SheetMetal.ipt")

        mock_inventor.FileManager.GetTemplateFile.assert_not_called()
        mock_inventor.Documents.Add.assert_called_once_with(
            12290, r"C:\Templates\SheetMetal.ipt", True
        )

    def test_new_part_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mock_inventor.Documents.Add.side_effect = Exception("Template missing")
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to create part"):
            mgr.doc_new_part()

    def test_new_part_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.doc_new_part()


# ==================================================================
# doc_new_assembly
# ==================================================================


class TestDocNewAssembly:
    """Tests for doc_new_assembly()."""

    def test_new_assembly_success(self, mock_inventor: MagicMock):
        """Should create an assembly document and return metadata."""
        mock_doc = _make_mock_doc(doc_type=12291)
        mock_inventor.Documents.Add.return_value = mock_doc
        mock_inventor.FileManager.GetTemplateFile.return_value = r"C:\Templates\Standard.iam"
        mgr = _make_doc_manager(mock_inventor)
        result = mgr.doc_new_assembly()

        assert result["success"] is True
        assert result["document_type"] == 12291
        mock_inventor.FileManager.GetTemplateFile.assert_called_once_with(12291)
        mock_inventor.Documents.Add.assert_called_once_with(12291, r"C:\Templates\Standard.iam", True)

    def test_new_assembly_with_template(self, mock_inventor: MagicMock):
        """Should pass custom template path to Documents.Add."""
        mock_doc = _make_mock_doc()
        mock_inventor.Documents.Add.return_value = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        mgr.doc_new_assembly(template=r"C:\Templates\Weldment.iam")

        mock_inventor.FileManager.GetTemplateFile.assert_not_called()
        mock_inventor.Documents.Add.assert_called_once_with(
            12291, r"C:\Templates\Weldment.iam", True
        )

    def test_new_assembly_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when COM call fails."""
        mock_inventor.Documents.Add.side_effect = Exception("COM error")
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to create assembly"):
            mgr.doc_new_assembly()

    def test_new_assembly_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.doc_new_assembly()


# ==================================================================
# doc_save
# ==================================================================


class TestDocSave:
    """Tests for doc_save()."""

    def test_save_success(self, mock_inventor: MagicMock):
        """Should save the active document."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        result = mgr.doc_save()

        assert result["success"] is True
        assert result["document"] == r"C:\Models\Part1.ipt"
        mock_doc.Save.assert_called_once()

    def test_save_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.doc_save()

    def test_save_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when Save() fails."""
        mock_doc = _make_mock_doc()
        mock_doc.Save.side_effect = Exception("Write protected")
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to save"):
            mgr.doc_save()

    def test_save_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.doc_save()


# ==================================================================
# doc_save_as
# ==================================================================


class TestDocSaveAs:
    """Tests for doc_save_as()."""

    def test_save_as_success(self, mock_inventor: MagicMock):
        """Should save the active document to a new path."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        result = mgr.doc_save_as(r"C:\Models\copy.ipt")

        assert result["success"] is True
        mock_doc.SaveAs.assert_called_once_with(r"C:\Models\copy.ipt", True)

    def test_save_as_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.doc_save_as(r"C:\Models\copy.ipt")

    def test_save_as_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when SaveAs() fails."""
        mock_doc = _make_mock_doc()
        mock_doc.SaveAs.side_effect = Exception("Invalid path")
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to save document as"):
            mgr.doc_save_as(r"C:\bad\path.ipt")

    def test_save_as_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.doc_save_as(r"C:\Models\copy.ipt")


# ==================================================================
# doc_close
# ==================================================================


class TestDocClose:
    """Tests for doc_close()."""

    def test_close_with_save(self, mock_inventor: MagicMock):
        """Should save and then close the active document."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        result = mgr.doc_close(save=True)

        assert result["success"] is True
        assert result["document"] == r"C:\Models\Part1.ipt"
        mock_doc.Save.assert_called_once()
        mock_doc.Close.assert_called_once()

    def test_close_without_save(self, mock_inventor: MagicMock):
        """Should close without saving when save=False."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        result = mgr.doc_close(save=False)

        assert result["success"] is True
        mock_doc.Save.assert_not_called()
        mock_doc.Close.assert_called_once()

    def test_close_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.doc_close()

    def test_close_com_error(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when Close() fails."""
        mock_doc = _make_mock_doc()
        mock_doc.Close.side_effect = Exception("COM error")
        mock_inventor.ActiveDocument = mock_doc
        mgr = _make_doc_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Failed to close"):
            mgr.doc_close(save=False)

    def test_close_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.doc_close()
