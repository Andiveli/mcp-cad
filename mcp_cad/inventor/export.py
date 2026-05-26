"""Export operations for Autodesk Inventor.

Provides STEP, STL, PDF, and DXF export via Inventor's TranslatorAddIn
COM interface.
"""

from __future__ import annotations

import logging
from typing import Any

from mcp_cad.inventor.client import InventorDriver
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)

# Known Translator AddIn GUIDs for Inventor
_STEP_ADDIN_GUID = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}"

# Map of file-extension → friendly name for name-based translator lookup
_EXPORT_FORMAT_MAP: dict[str, str] = {
    ".stp": "STEP",
    ".step": "STEP",
    ".stl": "STL",
    ".pdf": "PDF",
    ".dxf": "DXF",
}


class ExportManager:
    """Manages Inventor export operations: STEP, STL, PDF, DXF.

    Receives the driver and accesses its ``inventor`` property dynamically,
    so that a late ``connect()`` call is reflected immediately.
    """

    def __init__(self, driver: InventorDriver) -> None:
        self._driver = driver

    # ------------------------------------------------------------------
    # Internal guards
    # ------------------------------------------------------------------

    def _ensure_connected(self) -> None:
        """Verify that the COM reference is still alive."""
        if self._driver.inventor is None:
            raise InventorDisconnectedError(
                "Not connected to Inventor. Call connect() first."
            )

    def _ensure_active_document(self) -> Any:
        """Return the active document COM object or raise."""
        self._ensure_connected()
        doc = self._driver.inventor.ActiveDocument
        if doc is None:
            raise InventorCOMError("No active document.")
        return doc

    def _get_translator_by_guid(self, addin_guid: str) -> Any:
        """Look up a TranslatorAddIn by its class ID string.

        Parameters
        ----------
        addin_guid:
            The COM class GUID of the translator AddIn (e.g. STEP GUID).

        Returns
        -------
        The TranslatorAddIn COM object.

        Raises
        ------
        InventorCOMError
            If the translator is not found.
        """
        try:
            translators = self._driver.inventor.ApplicationAddIns
            for i in range(1, translators.Count + 1):
                addin = translators.Item(i)
                if addin.ClassIdString.upper() == addin_guid.upper():
                    return addin
            raise InventorCOMError(
                f"Translator AddIn with GUID '{addin_guid}' not found."
            )
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to find translator AddIn: {exc}"
            ) from exc

    def _get_translator_by_name(self, name: str) -> Any:
        """Look up a TranslatorAddIn by its display name.

        Uses Inventor's ``FileManager.GetTranslatorByName`` when available,
        falling back to iterating ApplicationAddIns.

        Parameters
        ----------
        name:
            The translator display name (e.g. "STEP", "STL").

        Returns
        -------
        The TranslatorAddIn COM object.

        Raises
        ------
        InventorCOMError
            If the translator is not found.
        """
        # Try the FileManager route first
        try:
            fm = self._driver.inventor.FileManager
            translator = fm.GetTranslatorByName(name)
            if translator is not None:
                return translator
        except AttributeError:
            pass  # GetTranslatorByName not available on older versions

        # Fallback: iterate ApplicationAddIns looking for a matching name
        try:
            translators = self._driver.inventor.ApplicationAddIns
            for i in range(1, translators.Count + 1):
                addin = translators.Item(i)
                addin_name = getattr(addin, "DisplayName", "") or ""
                if name.upper() in addin_name.upper():
                    return addin
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to find translator by name '{name}': {exc}"
            ) from exc

        raise InventorCOMError(
            f"Translator AddIn for '{name}' not found."
        )

    def _run_export(
        self,
        doc: Any,
        path: str,
        translator: Any,
        context: Any | None = None,
    ) -> dict[str, Any]:
        """Execute an export via the TranslatorAddIn.SaveCopyAs method.

        Parameters
        ----------
        doc:
            The document COM object to export.
        path:
            Destination file path.
        translator:
            The TranslatorAddIn COM object.
        context:
            Optional TranslationContext (for format-specific options).

        Returns
        -------
        dict with export result.
        """
        try:
            # Build a DataIO for the destination file
            # Some translators need context, others just the path
            if context is not None:
                translator.SaveCopyAs(doc, context, path)
            else:
                translator.SaveCopyAs(doc, None, path)
            return {"success": True, "path": path}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Export failed for '{path}': {exc}"
            ) from exc

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def export_step(
        self, path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to STEP format (.stp/.step).

        Parameters
        ----------
        path:
            Destination file path.
        options:
            Optional export settings (reserved for future use).

        Returns
        -------
        dict with ``success`` and ``path`` keys.
        """
        self._ensure_connected()
        doc = self._ensure_active_document()
        translator = self._get_translator_by_guid(_STEP_ADDIN_GUID)
        return self._run_export(doc, path, translator)

    def export_stl(
        self, path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to STL format (.stl).

        Parameters
        ----------
        path:
            Destination file path.
        options:
            Optional export settings. Supported keys:
            - ``quality`` (int): STL quality level (reserved).

        Returns
        -------
        dict with ``success`` and ``path`` keys.
        """
        self._ensure_connected()
        doc = self._ensure_active_document()
        translator = self._get_translator_by_name("STL")
        return self._run_export(doc, path, translator)

    def export_pdf(
        self, path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to PDF format.

        For drawing documents, uses ``SaveAs`` with PDF file format.
        For part/assembly documents, uses the PDF TranslatorAddIn.

        Parameters
        ----------
        path:
            Destination file path.
        options:
            Optional export settings (reserved for future use).

        Returns
        -------
        dict with ``success`` and ``path`` keys.
        """
        self._ensure_connected()
        doc = self._ensure_active_document()
        # Drawing documents use SaveAs with file format flag
        try:
            doc_type = doc.DocumentType
            # Drawing document type constant = 12293
            if doc_type == 12293:
                doc.SaveAs(path, True)
                return {"success": True, "path": path}
        except Exception:
            pass  # fall through to translator approach

        translator = self._get_translator_by_name("PDF")
        return self._run_export(doc, path, translator)

    def export_dxf(
        self, path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document's sketch or flat pattern to DXF format.

        Parameters
        ----------
        path:
            Destination file path.
        options:
            Optional export settings (reserved for future use).

        Returns
        -------
        dict with ``success`` and ``path`` keys.
        """
        self._ensure_connected()
        doc = self._ensure_active_document()
        translator = self._get_translator_by_name("DXF")
        return self._run_export(doc, path, translator)