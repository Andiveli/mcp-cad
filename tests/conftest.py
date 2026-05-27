"""Shared fixtures and mock factories for Inventor COM tests.

All tests in this project MUST work on Linux where pywin32 and the mcp
package are unavailable. This conftest module-level code mocks the
Windows-only COM modules **and** the ``mcp`` package in ``sys.modules``
**before** any test file imports the production code, so that imports like
``import pythoncom`` and ``from mcp.server.fastmcp import FastMCP`` resolve
to MagicMock objects instead of raising ``ModuleNotFoundError``.
"""

from __future__ import annotations

import sys
from unittest.mock import MagicMock, PropertyMock

import pytest

# ------------------------------------------------------------------
# Module-level COM mocking (runs at conftest load time, BEFORE tests)
# ------------------------------------------------------------------

_pythoncom = MagicMock(name="pythoncom")
_pythoncom.COINIT_APARTMENTTHREADED = 2  # actual COM constant
_pythoncom.CoInitializeEx = MagicMock(name="CoInitializeEx")
_pythoncom.CoUninitialize = MagicMock(name="CoUninitialize")

_win32com = MagicMock(name="win32com")
_win32com_client = MagicMock(name="win32com.client")
_win32com_client.Dispatch = MagicMock(name="Dispatch")

sys.modules["pythoncom"] = _pythoncom
sys.modules["win32com"] = _win32com
sys.modules["win32com.client"] = _win32com_client

# ------------------------------------------------------------------
# Module-level MCP mocking (FastMCP may not be installed on Linux)
# ------------------------------------------------------------------

_FastMCP = MagicMock(name="FastMCP")
_mcp_server_fastmcp = MagicMock(name="mcp.server.fastmcp")
_mcp_server_fastmcp.FastMCP = _FastMCP

sys.modules.setdefault("mcp", MagicMock(name="mcp"))
sys.modules.setdefault("mcp.server", MagicMock(name="mcp.server"))
sys.modules["mcp.server.fastmcp"] = _mcp_server_fastmcp

# CRITICAL: when ``import win32com.client`` runs inside production code,
# Python sets ``win32com.client`` as an attribute on the parent package.
# Because the parent is a MagicMock, it auto-creates a *different* child
# mock — so ``get_dispatch_mock()`` and ``win32com.client.Dispatch`` in
# ``client.py`` end up pointing to different objects.
# This explicit assignment unifies the mock chain so both paths reach the
# same configured ``Dispatch`` mock.
_win32com.client = _win32com_client


# ------------------------------------------------------------------
# Test-lifecycle cleanup (resets shared mocks between tests)
# ------------------------------------------------------------------


@pytest.fixture(autouse=True)
def _reset_com_mocks():
    """Reset all shared COM mocks before each test.

    Without this a test that sets ``.side_effect`` on the shared
    ``CoInitializeEx`` or ``Dispatch`` mock will poison subsequent tests.

    Also cleans up class-level ``PropertyMock`` modifications made to the
    ``MagicMock`` class itself (e.g. ``SoftwareVersion``) so they don't
    leak across test classes.
    """
    _pythoncom.CoInitializeEx.reset_mock()
    _pythoncom.CoInitializeEx.side_effect = None
    _pythoncom.CoUninitialize.reset_mock()
    _pythoncom.CoUninitialize.side_effect = None
    _win32com_client.Dispatch.reset_mock()
    _win32com_client.Dispatch.side_effect = None
    yield
    # Teardown: revert class-level modifications so stale references
    # and health-detection tests that set ``PropertyMock`` on the
    # ``MagicMock`` class don't pollute subsequent test classes.
    try:
        del MagicMock.SoftwareVersion
    except AttributeError:
        pass


# ------------------------------------------------------------------
# Accessors for shared mocks
# ------------------------------------------------------------------


def get_dispatch_mock() -> MagicMock:
    """Return the shared ``win32com.client.Dispatch`` mock."""
    return _win32com_client.Dispatch


def get_pythoncom_mock() -> MagicMock:
    """Return the shared ``pythoncom`` module mock."""
    return _pythoncom


# ------------------------------------------------------------------
# Convenience factories
# ------------------------------------------------------------------


def make_mock_inventor(
    version: str = "28.0",
    docs_open: int = 0,
    active_doc_path: str | None = None,
) -> MagicMock:
    """Build a mock Inventor Application COM object.

    Parameters
    ----------
    version:
        Value returned by ``.SoftwareVersion``.
    docs_open:
        Value returned by ``.Documents.Count``.
    active_doc_path:
        Value returned by ``.ActiveDocument.FullFileName``.
        When ``None``, ``.ActiveDocument`` itself is ``None`` (no open doc).
    """
    inventor = MagicMock()
    inventor.SoftwareVersion = version

    mock_docs = MagicMock()
    mock_docs.Count = docs_open
    inventor.Documents = mock_docs

    if active_doc_path is not None:
        mock_doc = MagicMock()
        mock_doc.FullFileName = active_doc_path
        inventor.ActiveDocument = mock_doc
    else:
        inventor.ActiveDocument = None

    return inventor


def make_mock_driver(inventor: MagicMock | None = None) -> MagicMock:
    """Build a mock driver with an ``.inventor`` property that returns the given mock.

    Use this to construct managers that access the COM object via
    ``driver.inventor`` — the property ensures managers see updates
    when the driver's COM reference changes.
    """
    driver = MagicMock()
    type(driver).inventor = PropertyMock(return_value=inventor)
    # Also mock common driver methods so server tests don't need to configure them
    driver.connect.return_value = {"connected": True, "inventor_version": "28.0", "documents_open": 0, "active_document": None}
    driver.health.return_value = {"connected": True, "inventor_version": "28.0", "documents_open": 0, "active_document": None}
    driver.disconnect.return_value = {"status": "disconnected"}
    return driver


# ------------------------------------------------------------------
# Standard test fixtures
# ------------------------------------------------------------------


@pytest.fixture
def mock_inventor() -> MagicMock:
    """Basic healthy Inventor Application mock (version 28.0, no docs)."""
    return make_mock_inventor()


@pytest.fixture
def mock_dispatch(mock_inventor: MagicMock) -> MagicMock:
    """Configure ``win32com.client.Dispatch`` to return *mock_inventor*."""
    dispatch = get_dispatch_mock()
    dispatch.return_value = mock_inventor
    return dispatch


@pytest.fixture
def driver():
    """Return a fresh ``RealInventorDriver`` pre-loaded with COM mocks."""
    from mcp_cad.providers.inventor.client import RealInventorDriver

    return RealInventorDriver()
