"""Unit tests for the Inventor COM connection lifecycle.

All tests run on plain mocks — no real Inventor installation required.
"""

from __future__ import annotations

from unittest.mock import MagicMock, PropertyMock

import pytest

from mcp_cad.errors import (
    InventorConnectionError,
    InventorNotFoundError,
    InventorPermissionError,
)


class TestRealInventorDriverConnect:
    """Happy path, error handling, and edge cases for ``connect()``."""

    # ----------------------------------------------------------------
    # Happy path
    # ----------------------------------------------------------------

    def test_connect_success(
        self,
        driver,
        mock_dispatch: MagicMock,
        mock_inventor: MagicMock,
    ):
        """Should initialise COM, dispatch Inventor, and return health."""
        result = driver.connect()

        assert result["connected"] is True
        assert result["inventor_version"] == "28.0"
        assert result["documents_open"] == 0
        assert result["active_document"] is None

    def test_connect_with_active_document(
        self,
        driver,
        mock_dispatch: MagicMock,
    ):
        """Should report active document when one is open."""
        from tests.conftest import make_mock_inventor

        inv = make_mock_inventor(active_doc_path=r"C:\model\part1.ipt")
        mock_dispatch.return_value = inv

        result = driver.connect()
        assert result["active_document"] == r"C:\model\part1.ipt"

    # ----------------------------------------------------------------
    # Error scenarios
    # ----------------------------------------------------------------

    def test_connect_class_not_registered(self, driver, mock_dispatch):
        """Should raise ``InventorNotFoundError`` when class not registered."""
        mock_dispatch.side_effect = Exception("Invalid class string")
        with pytest.raises(InventorNotFoundError, match="not installed"):
            driver.connect()

    def test_connect_permission_denied(self, driver, mock_dispatch):
        """Should raise ``InventorPermissionError`` on access denied."""
        mock_dispatch.side_effect = Exception("Permission denied")
        with pytest.raises(InventorPermissionError, match="Permission denied"):
            driver.connect()

    def test_connect_generic_com_error(self, driver, mock_dispatch):
        """Should raise ``InventorConnectionError`` for other COM failures."""
        mock_dispatch.side_effect = Exception("RPC server unavailable")
        with pytest.raises(InventorConnectionError, match="Failed to connect"):
            driver.connect()

    def test_connect_com_init_failure(self, driver):
        """Should raise when ``CoInitializeEx`` itself fails."""
        from tests.conftest import get_pythoncom_mock

        get_pythoncom_mock().CoInitializeEx.side_effect = Exception(
            "COM init failed"
        )
        with pytest.raises(InventorConnectionError, match="COM initialization"):
            driver.connect()

    # ----------------------------------------------------------------
    # Edge cases
    # ----------------------------------------------------------------

    def test_connect_idempotent(
        self,
        driver,
        mock_dispatch: MagicMock,
        mock_inventor: MagicMock,
    ):
        """Second call should return same health without re-dispatching."""
        first = driver.connect()
        mock_dispatch.reset_mock()
        second = driver.connect()

        assert first == second
        mock_dispatch.assert_not_called()

    def test_connect_after_stale_reference(
        self,
        driver,
        mock_dispatch: MagicMock,
        mock_inventor: MagicMock,
    ):
        """Connecting after a stale reference should re-dispatch."""
        # Connect once
        driver.connect()
        # Force stale — make SoftwareVersion raise
        type(mock_inventor).SoftwareVersion = PropertyMock(
            side_effect=RuntimeError("Server execution failed")
        )
        # This connect call should detect staleness and re-dispatch
        mock_dispatch.reset_mock()
        # Restore health on the *new* mock that dispatch returns
        fresh_inventor = MagicMock()
        fresh_inventor.SoftwareVersion = "28.0"
        fresh_docs = MagicMock()
        fresh_docs.Count = 0
        fresh_inventor.Documents = fresh_docs
        fresh_inventor.ActiveDocument = None
        mock_dispatch.return_value = fresh_inventor

        result = driver.connect()
        assert result["connected"] is True
        mock_dispatch.assert_called_once()


class TestRealInventorDriverDisconnect:
    """Happy path and edge cases for ``disconnect()``."""

    def test_disconnect_after_connect(
        self,
        driver,
        mock_dispatch: MagicMock,
    ):
        """Should release COM reference and return disconnected status."""
        driver.connect()
        result = driver.disconnect()
        assert result["status"] == "disconnected"

    def test_disconnect_when_not_connected(self, driver):
        """Should be idempotent — always returns disconnected."""
        result = driver.disconnect()
        assert result["status"] == "disconnected"

    def test_disconnect_idempotent(self, driver):
        """Calling disconnect twice should be harmless."""
        driver.disconnect()
        result = driver.disconnect()
        assert result["status"] == "disconnected"


class TestRealInventorDriverHealth:
    """Happy path and edge cases for ``health()``."""

    def test_health_when_connected(
        self,
        driver,
        mock_dispatch: MagicMock,
        mock_inventor: MagicMock,
    ):
        """Should reflect the current connection state."""
        driver.connect()
        health = driver.health()
        assert health["connected"] is True
        assert health["inventor_version"] == "28.0"

    def test_health_when_disconnected(self, driver):
        """Should return disconnected state without raising."""
        health = driver.health()
        assert health["connected"] is False
        assert health["inventor_version"] is None

    def test_health_detects_stale_reference(
        self,
        driver,
        mock_dispatch: MagicMock,
        mock_inventor: MagicMock,
    ):
        """When Inventor is killed externally, health should detect staleness."""
        driver.connect()
        type(mock_inventor).SoftwareVersion = PropertyMock(
            side_effect=RuntimeError("Server execution failed")
        )
        health = driver.health()
        assert health["connected"] is False
        assert health["inventor_version"] is None
        # Internally the reference should have been cleared
        assert driver._inventor is None

    def test_health_with_documents(
        self,
        driver,
        mock_dispatch: MagicMock,
    ):
        """Should report open document counts accurately."""
        from tests.conftest import make_mock_inventor

        inv = make_mock_inventor(docs_open=3)
        mock_dispatch.return_value = inv

        driver.connect()
        health = driver.health()
        assert health["documents_open"] == 3

    def test_health_never_raises(self, driver):
        """Calling health on a fresh driver should never raise."""
        health = driver.health()
        assert isinstance(health, dict)
        assert "connected" in health
