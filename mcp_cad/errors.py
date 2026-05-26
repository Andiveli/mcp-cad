"""Custom exception hierarchy for Inventor COM errors."""


class InventorError(Exception):
    """Base exception for all Inventor-related errors."""


class InventorConnectionError(InventorError):
    """Raised when connection to Inventor fails."""


class InventorDisconnectedError(InventorError):
    """Raised when an operation is attempted without an active connection."""


class InventorNotFoundError(InventorConnectionError):
    """Raised when Inventor is not installed on the system."""


class InventorPermissionError(InventorConnectionError):
    """Raised when the user lacks permissions to access Inventor."""


class InventorCOMError(InventorError):
    """Raised for generic COM errors."""
