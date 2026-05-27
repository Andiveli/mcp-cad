"""Document operations for Autodesk Inventor.

Provides open, create, save, and close operations on Inventor documents
via the COM automation interface.
"""

from __future__ import annotations

import logging
from typing import Any

from mcp_cad.providers.inventor.client import InventorDriver
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)

# Inventor document-type COM constants
PART_DOCUMENT = 12290
ASSEMBLY_DOCUMENT = 12291


class DocumentManager:
    """Manages Inventor document operations: open, create, save, close.

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

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def doc_open(self, path: str) -> dict[str, Any]:
        """Open an existing Inventor document.

        Parameters
        ----------
        path:
            Full file path to the document.

        Returns
        -------
        dict with document metadata.
        """
        self._ensure_connected()
        try:
            doc = self._driver.inventor.Documents.Open(path)
            return {
                "success": True,
                "document": doc.FullFileName,
                "document_type": doc.DocumentType,
            }
        except InventorDisconnectedError:
            raise
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to open document '{path}': {exc}"
            ) from exc

    def doc_new_part(self, template: str = "") -> dict[str, Any]:
        """Create a new part document.

        Uses ``FileManager.GetTemplateFile`` for fast, deterministic template
        resolution.  When *template* is empty, the one-argument overload is
        called and returns the system-default part template path.

        Parameters
        ----------
        template:
            Optional full path to a template file (default: "" → default template).

        Returns
        -------
        dict with document metadata.
        """
        self._ensure_connected()
        try:
            inv = self._driver.inventor
            if template == "" or template.isspace():
                template_path = inv.FileManager.GetTemplateFile(PART_DOCUMENT)
            else:
                template_path = template
            doc = inv.Documents.Add(PART_DOCUMENT, template_path, True)
            return {
                "success": True,
                "document": doc.FullFileName or "",
                "document_type": doc.DocumentType,
            }
        except InventorDisconnectedError:
            raise
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to create part document: {exc}"
            ) from exc

    def doc_new_assembly(self, template: str = "") -> dict[str, Any]:
        """Create a new assembly document.

        Parameters
        ----------
        template:
            Optional full path to a template file (default: "" → default template).

        Returns
        -------
        dict with document metadata.
        """
        self._ensure_connected()
        try:
            inv = self._driver.inventor
            if template == "" or template.isspace():
                template_path = inv.FileManager.GetTemplateFile(ASSEMBLY_DOCUMENT)
            else:
                template_path = template
            doc = inv.Documents.Add(ASSEMBLY_DOCUMENT, template_path, True)
            return {
                "success": True,
                "document": doc.FullFileName or "",
                "document_type": doc.DocumentType,
            }
        except InventorDisconnectedError:
            raise
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to create assembly document: {exc}"
            ) from exc

    def doc_save(self) -> dict[str, Any]:
        """Save the active document.

        Returns
        -------
        dict with save status.
        """
        doc = self._ensure_active_document()
        try:
            doc.Save()
            return {"success": True, "document": doc.FullFileName}
        except InventorDisconnectedError:
            raise
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to save document: {exc}"
            ) from exc

    def doc_save_as(self, path: str) -> dict[str, Any]:
        """Save the active document to a new path.

        Parameters
        ----------
        path:
            Full file path for the new document.

        Returns
        -------
        dict with save-as status.
        """
        doc = self._ensure_active_document()
        try:
            doc.SaveAs(path, True)
            return {"success": True, "document": doc.FullFileName}
        except InventorDisconnectedError:
            raise
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to save document as '{path}': {exc}"
            ) from exc

    def doc_close(self, save: bool = True) -> dict[str, Any]:
        """Close the active document.

        Parameters
        ----------
        save:
            Whether to save before closing (default: True).

        Returns
        -------
        dict with close status.
        """
        doc = self._ensure_active_document()
        try:
            filename = doc.FullFileName
            if save:
                doc.Save()
            doc.Close()
            return {"success": True, "document": filename}
        except InventorDisconnectedError:
            raise
        except InventorCOMError:
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to close document: {exc}"
            ) from exc
