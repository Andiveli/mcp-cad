"""iProperty read/write for Autodesk Inventor.

Provides access to summary, project, and custom iProperties via COM.
"""

from __future__ import annotations

import logging
from typing import Any

from mcp_cad.inventor.client import InventorDriver
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)

# Inventor iProperty set COM indices (1-based)
_SUMMARY_PROPERTY_SET = "Inventor Summary Information"
_PROJECT_PROPERTY_SET = "Inventor Document Summary Information"
_CUSTOM_PROPERTY_SET = "Design Tracking Properties"


class PropertyManager:
    """Manages Inventor iProperties: read, write, summary, custom.

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

    def _get_property_set(self, doc: Any, set_name: str) -> Any:
        """Retrieve a PropertySet by name from the document."""
        try:
            return doc.PropertySets.Item(set_name)
        except Exception as exc:
            raise InventorCOMError(
                f"Property set '{set_name}' not found: {exc}"
            ) from exc

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def iproperty_get(self, name: str, property_set: str = "Summary") -> dict[str, Any]:
        """Get an iProperty value by name.

        Parameters
        ----------
        name:
            The property name within the property set.
        property_set:
            The property set to search: "Summary" (default),
            "Project", or "Custom".

        Returns
        -------
        dict with ``name`` and ``value`` keys.
        """
        set_key = self._resolve_property_set(property_set)
        doc = self._ensure_active_document()
        try:
            prop_set = self._get_property_set(doc, set_key)
            prop = prop_set.Item(name)
            return {
                "success": True,
                "name": name,
                "value": prop.Value,
                "property_set": property_set,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to get iProperty '{name}': {exc}"
            ) from exc

    def iproperty_set(
        self, name: str, value: Any, property_set: str = "Summary"
    ) -> dict[str, Any]:
        """Set an iProperty value by name.

        Parameters
        ----------
        name:
            The property name within the property set.
        value:
            The value to assign.
        property_set:
            The property set: "Summary" (default),
            "Project", or "Custom".

        Returns
        -------
        dict with ``name`` and ``value`` confirming the update.
        """
        set_key = self._resolve_property_set(property_set)
        doc = self._ensure_active_document()
        try:
            prop_set = self._get_property_set(doc, set_key)
            prop = prop_set.Item(name)
            prop.Value = value
            return {
                "success": True,
                "name": name,
                "value": value,
                "property_set": property_set,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to set iProperty '{name}': {exc}"
            ) from exc

    def iproperty_summary(self) -> dict[str, Any]:
        """Get all Summary iProperties.

        Returns
        -------
        dict with ``properties`` key mapping to a list of
        ``{name, value}`` dicts.
        """
        doc = self._ensure_active_document()
        try:
            prop_set = self._get_property_set(doc, _SUMMARY_PROPERTY_SET)
            result: list[dict[str, Any]] = []
            for i in range(1, prop_set.Count + 1):
                prop = prop_set.Item(i)
                result.append({
                    "name": prop.Name,
                    "value": prop.Value,
                })
            return {"success": True, "properties": result}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to get summary iProperties: {exc}"
            ) from exc

    def iproperty_custom_get(self, name: str) -> dict[str, Any]:
        """Get a custom iProperty by name.

        Parameters
        ----------
        name:
            Name of the custom property.

        Returns
        -------
        dict with ``name`` and ``value`` keys.
        """
        doc = self._ensure_active_document()
        try:
            prop_set = self._get_property_set(doc, _CUSTOM_PROPERTY_SET)
            prop = prop_set.Item(name)
            return {
                "success": True,
                "name": prop.Name,
                "value": prop.Value,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to get custom iProperty '{name}': {exc}"
            ) from exc

    def iproperty_custom_set(self, name: str, value: Any) -> dict[str, Any]:
        """Set a custom iProperty. Creates it if it doesn't exist.

        Parameters
        ----------
        name:
            Name of the custom property.
        value:
            The value to assign.

        Returns
        -------
        dict with ``name`` and ``value`` confirming the update.
        """
        doc = self._ensure_active_document()
        try:
            prop_set = self._get_property_set(doc, _CUSTOM_PROPERTY_SET)
            try:
                # Property exists — update it
                prop = prop_set.Item(name)
                prop.Value = value
            except Exception:
                # Property doesn't exist — create it
                prop_set.Add(name, value)
            return {
                "success": True,
                "name": name,
                "value": value,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to set custom iProperty '{name}': {exc}"
            ) from exc

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _resolve_property_set(property_set: str) -> str:
        """Map a friendly property-set name to the Inventor COM identifier."""
        _SET_MAP: dict[str, str] = {
            "summary": _SUMMARY_PROPERTY_SET,
            "project": _PROJECT_PROPERTY_SET,
            "custom": _CUSTOM_PROPERTY_SET,
        }
        key = property_set.lower()
        result = _SET_MAP.get(key)
        if result is None:
            raise InventorCOMError(
                f"Invalid property set '{property_set}'. "
                f"Must be one of: {', '.join(sorted(_SET_MAP))}"
            )
        return result