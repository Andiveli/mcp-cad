"""Model parameter read/write for Autodesk Inventor.

Provides listing, reading, and writing of model parameters via COM.
"""

from __future__ import annotations

import logging
from typing import Any

from mcp_cad.providers.inventor.client import InventorDriver
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)


class ParameterManager:
    """Manages Inventor model parameters: list, get, set, expressions.

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
        try:
            import win32com.client
            doc = win32com.client.Dispatch(doc)
        except Exception:
            pass
        return doc

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def param_list(self, filter_pattern: str | None = None) -> dict[str, Any]:
        """List model parameters, optionally filtered by name pattern.

        Parameters
        ----------
        filter_pattern:
            Optional case-insensitive substring to filter parameter names.

        Returns
        -------
        dict with a list of parameter dicts under the ``parameters`` key.
        Each parameter dict has ``name``, ``value``, and ``expression`` keys.
        """
        doc = self._ensure_active_document()
        try:
            params = doc.ComponentDefinition.Parameters
            result: list[dict[str, Any]] = []
            for i in range(1, params.Count + 1):
                p = params.Item(i)
                name = p.Name
                if filter_pattern and filter_pattern.lower() not in name.lower():
                    continue
                result.append({
                    "name": name,
                    "value": p.Value,
                    "expression": p.Expression,
                })
            return {"success": True, "parameters": result}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to list parameters: {exc}") from exc

    def param_get(self, name: str) -> dict[str, Any]:
        """Get a specific parameter by name.

        Parameters
        ----------
        name:
            The parameter name (case-sensitive match as Inventor stores it).

        Returns
        -------
        dict with ``name``, ``value``, and ``expression`` keys.
        """
        doc = self._ensure_active_document()
        try:
            params = doc.ComponentDefinition.Parameters
            p = params.Item(name)
            return {
                "success": True,
                "name": p.Name,
                "value": p.Value,
                "expression": p.Expression,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to get parameter '{name}': {exc}"
            ) from exc

    def param_set(self, name: str, value: float | int) -> dict[str, Any]:
        """Set a parameter value by name.

        Parameters
        ----------
        name:
            The parameter name.
        value:
            The numeric value to assign.

        Returns
        -------
        dict with ``name`` and ``value`` confirming the update.
        """
        doc = self._ensure_active_document()
        try:
            params = doc.ComponentDefinition.Parameters
            p = params.Item(name)
            p.Value = value
            return {
                "success": True,
                "name": name,
                "value": p.Value,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to set parameter '{name}': {exc}"
            ) from exc

    def param_set_expression(
        self, name: str, expression: str
    ) -> dict[str, Any]:
        """Set a parameter using an expression (e.g. "d0 * 2").

        Parameters
        ----------
        name:
            The parameter name.
        expression:
            The Inventor parameter expression string.

        Returns
        -------
        dict with ``name`` and ``expression`` confirming the update.
        """
        doc = self._ensure_active_document()
        try:
            params = doc.ComponentDefinition.Parameters
            p = params.Item(name)
            p.Expression = expression
            return {
                "success": True,
                "name": name,
                "expression": p.Expression,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to set expression for parameter '{name}': {exc}"
            ) from exc