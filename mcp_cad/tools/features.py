"""3D feature operation tools — extrude, revolve, fillet, chamfer.

Each function accepts a ``CADProvider`` and delegates to the corresponding
protocol method.  No Inventor-specific logic lives here — this is pure
delegation with error translation.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def extrude(
    provider: CADProvider,
    profile: str,
    distance: float,
    direction: str = "positive",
    taper: float = 0.0,
    operation: str = "new_body",
) -> dict[str, Any]:
    """Extrude a sketch profile to create a 3D feature."""
    try:
        return provider.extrude(profile, distance, direction, taper, operation)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def revolve(
    provider: CADProvider,
    profile: str,
    axis: str,
    angle: float = 360.0,
    operation: str = "join",
) -> dict[str, Any]:
    """Revolve a profile around an axis to create a 3D feature."""
    try:
        return provider.revolve(profile, axis, angle, operation)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def fillet(
    provider: CADProvider,
    edges: str,
    radius: float,
    mode: str = "constant",
) -> dict[str, Any]:
    """Apply a fillet to the specified edges."""
    try:
        return provider.fillet(edges, radius, mode)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def chamfer(
    provider: CADProvider,
    edges: str,
    distance: float,
    mode: str = "equal_distance",
) -> dict[str, Any]:
    """Apply a chamfer to the specified edges."""
    try:
        return provider.chamfer(edges, distance, mode)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}


def circular_pattern(
    provider: CADProvider,
    profile: str,
    axis: str,
    count: int,
    angle: float = 360.0,
    fit_within_angle: bool = True,
    natural_direction: bool = True,
) -> dict[str, Any]:
    """Create a circular pattern of a feature around an axis."""
    try:
        return provider.circular_pattern(
            profile, axis, count, angle, fit_within_angle, natural_direction,
        )
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}