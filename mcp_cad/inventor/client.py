"""COM connection lifecycle for Autodesk Inventor."""

from __future__ import annotations

import logging
from abc import ABC, abstractmethod
from typing import Any

import pythoncom
import win32com.client

from mcp_cad.errors import (
    InventorCOMError,
    InventorConnectionError,
    InventorDisconnectedError,
    InventorNotFoundError,
    InventorPermissionError,
)

log = logging.getLogger(__name__)


class InventorDriver(ABC):
    """Abstract base class for Inventor COM interaction.

    Can be mocked in tests to avoid requiring a real Inventor installation.
    """

    @abstractmethod
    def connect(self) -> dict[str, Any]:
        """Connect to a running Inventor instance or launch a new one.

        Returns:
            Dict with connection status metadata.

        Raises:
            InventorNotFoundError: Inventor is not installed.
            InventorPermissionError: Access denied.
            InventorConnectionError: Other connection failures.
        """

    @abstractmethod
    def disconnect(self) -> dict[str, Any]:
        """Release the COM reference to Inventor without closing the application.

        Returns:
            Dict with disconnection status.
        """

    @property
    @abstractmethod
    def inventor(self) -> Any:
        """Return the COM Inventor Application object, or None if not connected."""

    @abstractmethod
    def health(self) -> dict[str, Any]:
        """Check connection health and document state.

        Returns:
            Dict with connection and document status.
        """


class RealInventorDriver(InventorDriver):
    """Concrete implementation using pywin32 COM Dispatch.

    Initialises COM apartment on connect and tears it down on disconnect.
    """

    def __init__(self) -> None:
        self._inventor: Any = None
        self._com_initialized: bool = False

    @property
    def inventor(self) -> Any:
        """Return the COM Inventor Application object, or None if not connected."""
        return self._inventor

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def connect(self) -> dict[str, Any]:
        """Connect to Inventor via COM dispatch.

        Idempotent — returns the existing connection if already connected
        (but performs a health probe to detect stale references).
        """
        if self._inventor is not None:
            health = self._health_check()
            if health.get("connected"):
                return health

        self._init_com()
        self._dispatch_inventor()
        return self._health_check()

    def disconnect(self) -> dict[str, Any]:
        """Release the COM reference and uninitialize COM.

        Idempotent — safe to call when not connected.
        """
        self._inventor = None
        if self._com_initialized:
            try:
                pythoncom.CoUninitialize()
            except Exception:
                log.exception("COM uninitialize warning")
            finally:
                self._com_initialized = False
        return {"status": "disconnected"}

    def health(self) -> dict[str, Any]:
        """Return current health state.

        Always safe to call — never raises.
        """
        if self._inventor is None:
            return self._disconnected_health()
        return self._health_check()

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    def _init_com(self) -> None:
        """Initialize COM apartment for the current thread."""
        try:
            pythoncom.CoInitializeEx(pythoncom.COINIT_APARTMENTTHREADED)
            self._com_initialized = True
        except Exception as exc:
            raise InventorConnectionError(
                f"COM initialization failed: {exc}"
            ) from exc

    def _dispatch_inventor(self) -> None:
        """Create the Inventor Application COM dispatch object."""
        try:
            self._inventor = win32com.client.Dispatch("Inventor.Application")
        except Exception as exc:
            self._inventor = None
            msg = str(exc).lower()
            if "permission" in msg:
                raise InventorPermissionError(
                    f"Permission denied accessing Inventor: {exc}"
                ) from exc
            if "class not registered" in msg or "invalid class" in msg:
                raise InventorNotFoundError(
                    f"Inventor not installed or COM class not registered: {exc}"
                ) from exc
            raise InventorConnectionError(
                f"Failed to connect to Inventor: {exc}"
            ) from exc

    def _health_check(self) -> dict[str, Any]:
        """Query the COM object for current state.

        Detects stale references when Inventor has been closed externally.
        """
        try:
            version = self._inventor.SoftwareVersion
            docs_count = self._inventor.Documents.Count
            active_doc: str | None = None
            try:
                active_doc = self._inventor.ActiveDocument.FullFileName
            except Exception:
                active_doc = None

            return {
                "connected": True,
                "inventor_version": str(version),
                "documents_open": int(docs_count),
                "active_document": active_doc,
            }
        except Exception:
            log.warning("Inventor COM reference is stale — reconnection needed")
            self._inventor = None
            return self._disconnected_health()

    @staticmethod
    def _disconnected_health() -> dict[str, Any]:
        return {
            "connected": False,
            "inventor_version": None,
            "documents_open": 0,
            "active_document": None,
        }

    def __del__(self) -> None:
        """Best-effort cleanup on garbage collection."""
        try:
            if self._com_initialized:
                pythoncom.CoUninitialize()
        except Exception:
            pass
