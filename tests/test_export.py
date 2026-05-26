"""Unit tests for Inventor export operations.

All tests run on plain mocks — no real Inventor installation required.
"""

from __future__ import annotations

from unittest.mock import MagicMock, PropertyMock

import pytest

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.inventor.export import ExportManager
from tests.conftest import make_mock_driver


# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------


def _make_export_manager(mock_inventor: MagicMock) -> ExportManager:
    """Wire up an ExportManager with the given mock COM object."""
    driver = make_mock_driver(mock_inventor)
    return ExportManager(driver)


def _make_disconnected_manager() -> ExportManager:
    """Wire up an ExportManager with no COM reference."""
    driver = make_mock_driver(None)
    return ExportManager(driver)


def _make_mock_doc(doc_type: int = 12290) -> MagicMock:
    """Build a mock Inventor document COM object."""
    doc = MagicMock()
    doc.DocumentType = doc_type
    doc.FullFileName = r"C:\Models\Part1.ipt"
    return doc


def _make_mock_translator() -> MagicMock:
    """Build a mock TranslatorAddIn COM object."""
    translator = MagicMock()
    translator.SaveCopyAs = MagicMock()
    return translator


def _make_mock_addins_collection(
    step_translator: MagicMock | None = None,
) -> MagicMock:
    """Build a mock ApplicationAddIns collection with a STEP translator."""
    addins = MagicMock()
    addins.Count = 1

    if step_translator is None:
        step_translator = _make_mock_translator()
        step_translator.ClassIdString = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"

    addins.Item = MagicMock(return_value=step_translator)
    return addins


# ==================================================================
# export_step
# ==================================================================


class TestExportStep:
    """Happy path, error handling, and edge cases for export_step()."""

    def test_step_success(self, mock_inventor: MagicMock):
        """Should export active document to STEP format."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        # Set up translator
        step_translator = _make_mock_translator()
        step_translator.ClassIdString = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"
        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=step_translator)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        result = mgr.export_step(r"C:\Exports\part.stp")

        assert result["success"] is True
        assert result["path"] == r"C:\Exports\part.stp"
        step_translator.SaveCopyAs.assert_called_once()

    def test_step_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.export_step(r"C:\Exports\part.stp")

    def test_step_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.export_step(r"C:\Exports\part.stp")

    def test_step_translator_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when STEP translator is not found."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        # AddIns collection with no matching GUID
        other_addin = MagicMock()
        other_addin.ClassIdString = "{SOME-OTHER-GUID}"
        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=other_addin)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="not found"):
            mgr.export_step(r"C:\Exports\part.stp")

    def test_step_savecopyas_failure(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when SaveCopyAs fails."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        step_translator = _make_mock_translator()
        step_translator.ClassIdString = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"
        step_translator.SaveCopyAs.side_effect = Exception("Export error")
        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=step_translator)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="Export failed"):
            mgr.export_step(r"C:\Exports\part.stp")


# ==================================================================
# export_stl
# ==================================================================


class TestExportStl:
    """Happy path and error handling for export_stl()."""

    def test_stl_success(self, mock_inventor: MagicMock):
        """Should export active document to STL format."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        # Set up STL translator via FileManager
        stl_translator = _make_mock_translator()
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName = MagicMock(return_value=stl_translator)
        mock_inventor.FileManager = mock_fm

        mgr = _make_export_manager(mock_inventor)
        result = mgr.export_stl(r"C:\Exports\part.stl")

        assert result["success"] is True
        assert result["path"] == r"C:\Exports\part.stl"
        stl_translator.SaveCopyAs.assert_called_once()
        mock_fm.GetTranslatorByName.assert_called_once_with("STL")

    def test_stl_fallback_to_addins(self, mock_inventor: MagicMock):
        """Should fall back to ApplicationAddIns when FileManager fails."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        # FileManager.GetTranslatorByName not available (raises AttributeError)
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName.side_effect = AttributeError("Not available")
        mock_inventor.FileManager = mock_fm

        # AddIns fallback
        stl_translator = _make_mock_translator()
        stl_translator.DisplayName = "STL Export AddIn"
        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=stl_translator)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        result = mgr.export_stl(r"C:\Exports\part.stl")

        assert result["success"] is True
        assert result["path"] == r"C:\Exports\part.stl"

    def test_stl_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.export_stl(r"C:\Exports\part.stl")

    def test_stl_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.export_stl(r"C:\Exports\part.stl")


# ==================================================================
# export_pdf
# ==================================================================


class TestExportPdf:
    """Happy path and error handling for export_pdf()."""

    def test_pdf_drawing_saveas(self, mock_inventor: MagicMock):
        """Should use SaveAs for drawing documents."""
        mock_doc = _make_mock_doc(doc_type=12293)  # Drawing doc type
        mock_inventor.ActiveDocument = mock_doc

        mgr = _make_export_manager(mock_inventor)
        result = mgr.export_pdf(r"C:\Exports\drawing.pdf")

        assert result["success"] is True
        assert result["path"] == r"C:\Exports\drawing.pdf"
        mock_doc.SaveAs.assert_called_once_with(r"C:\Exports\drawing.pdf", True)

    def test_pdf_part_via_translator(self, mock_inventor: MagicMock):
        """Should use TranslatorAddIn for non-drawing documents."""
        mock_doc = _make_mock_doc(doc_type=12290)  # Part doc type
        mock_inventor.ActiveDocument = mock_doc

        # Set up PDF translator via FileManager
        pdf_translator = _make_mock_translator()
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName = MagicMock(return_value=pdf_translator)
        mock_inventor.FileManager = mock_fm

        mgr = _make_export_manager(mock_inventor)
        result = mgr.export_pdf(r"C:\Exports\part.pdf")

        assert result["success"] is True
        assert result["path"] == r"C:\Exports\part.pdf"
        pdf_translator.SaveCopyAs.assert_called_once()

    def test_pdf_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.export_pdf(r"C:\Exports\part.pdf")


# ==================================================================
# export_dxf
# ==================================================================


class TestExportDxf:
    """Happy path and error handling for export_dxf()."""

    def test_dxf_success(self, mock_inventor: MagicMock):
        """Should export active document to DXF format."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        # Set up DXF translator via FileManager
        dxf_translator = _make_mock_translator()
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName = MagicMock(return_value=dxf_translator)
        mock_inventor.FileManager = mock_fm

        mgr = _make_export_manager(mock_inventor)
        result = mgr.export_dxf(r"C:\Exports\sketch.dxf")

        assert result["success"] is True
        assert result["path"] == r"C:\Exports\sketch.dxf"
        dxf_translator.SaveCopyAs.assert_called_once()

    def test_dxf_not_connected(self):
        """Should raise InventorDisconnectedError when not connected."""
        mgr = _make_disconnected_manager()
        with pytest.raises(InventorDisconnectedError):
            mgr.export_dxf(r"C:\Exports\sketch.dxf")

    def test_dxf_no_active_document(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when no active document."""
        mock_inventor.ActiveDocument = None
        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="No active document"):
            mgr.export_dxf(r"C:\Exports\sketch.dxf")

    def test_dxf_translator_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when DXF translator not found."""
        mock_doc = _make_mock_doc()
        mock_inventor.ActiveDocument = mock_doc

        # FileManager returns None, AddIns iteration finds no match
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName = MagicMock(return_value=None)
        mock_inventor.FileManager = mock_fm

        other_addin = MagicMock()
        other_addin.DisplayName = "Something Else"
        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=other_addin)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="not found"):
            mgr.export_dxf(r"C:\Exports\sketch.dxf")


# ==================================================================
# _get_translator_by_guid
# ==================================================================


class TestGetTranslatorByGuid:
    """Unit tests for the GUID-based translator lookup."""

    def test_guid_lookup_success(self, mock_inventor: MagicMock):
        """Should find a translator by its class ID GUID."""
        step_addin = MagicMock()
        step_addin.ClassIdString = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"

        other_addin = MagicMock()
        other_addin.ClassIdString = "{SOME-OTHER-GUID}"

        mock_addins = MagicMock()
        mock_addins.Count = 2
        mock_addins.Item = MagicMock(side_effect=[other_addin, step_addin])
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        result = mgr._get_translator_by_guid(
            "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"
        )

        assert result is step_addin

    def test_guid_case_insensitive(self, mock_inventor: MagicMock):
        """Should match GUIDs case-insensitively."""
        addin = MagicMock()
        addin.ClassIdString = "{90af7f40-0c01-11d5-8e83-0010b541cd80}"

        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=addin)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        result = mgr._get_translator_by_guid(
            "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"
        )
        assert result is addin

    def test_guid_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when GUID not found."""
        other_addin = MagicMock()
        other_addin.ClassIdString = "{NOT-THE-RIGHT-GUID}"

        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=other_addin)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="not found"):
            mgr._get_translator_by_guid(
                "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"
            )


# ==================================================================
# _get_translator_by_name
# ==================================================================


class TestGetTranslatorByName:
    """Unit tests for the name-based translator lookup."""

    def test_file_manager_lookup(self, mock_inventor: MagicMock):
        """Should use FileManager.GetTranslatorByName first."""
        translator = MagicMock()
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName = MagicMock(return_value=translator)
        mock_inventor.FileManager = mock_fm

        mgr = _make_export_manager(mock_inventor)
        result = mgr._get_translator_by_name("STEP")

        assert result is translator
        mock_fm.GetTranslatorByName.assert_called_once_with("STEP")

    def test_fallback_to_addins_by_name(self, mock_inventor: MagicMock):
        """Should fall back to AddIns iteration when FileManager fails."""
        # FileManager.GetTranslatorByName not available
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName.side_effect = AttributeError("Not available")
        mock_inventor.FileManager = mock_fm

        # Matching AddIn in ApplicationAddIns
        step_addin = MagicMock()
        step_addin.DisplayName = "STEP File Translator"
        other_addin = MagicMock()
        other_addin.DisplayName = "Some Other Translator"

        mock_addins = MagicMock()
        mock_addins.Count = 2
        mock_addins.Item = MagicMock(side_effect=[other_addin, step_addin])
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        result = mgr._get_translator_by_name("STEP")

        assert result is step_addin

    def test_name_not_found(self, mock_inventor: MagicMock):
        """Should raise InventorCOMError when translator name not found."""
        mock_fm = MagicMock()
        mock_fm.GetTranslatorByName = MagicMock(return_value=None)
        mock_inventor.FileManager = mock_fm

        other_addin = MagicMock()
        other_addin.DisplayName = "Some Other Translator"
        mock_addins = MagicMock()
        mock_addins.Count = 1
        mock_addins.Item = MagicMock(return_value=other_addin)
        mock_inventor.ApplicationAddIns = mock_addins

        mgr = _make_export_manager(mock_inventor)
        with pytest.raises(InventorCOMError, match="not found"):
            mgr._get_translator_by_name("STEP")