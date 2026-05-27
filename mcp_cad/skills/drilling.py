"""Drilling pattern skill — creates a linear array of drilled holes.

Composes provider operations: sketch_create → sketch_circle → extrude(cut).
Works with ANY CADProvider implementation — no Inventor-specific code.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def crear_patron_taladros(
    provider: CADProvider,
    diametro: float,
    profundidad: float,
    espaciado: float,
    cantidad: int,
    x_centro: float = 0,
    y_centro: float = 0,
) -> dict[str, Any]:
    """Create a linear pattern of drilled holes.

    Creates a sketch on the XY plane, draws ``cantidad`` circles at
    evenly spaced positions, then extrude-cuts them to the specified depth.

    Args:
        provider: A CADProvider instance.
        diametro: Hole diameter (cm).
        profundidad: Cut depth (cm).
        espaciado: Spacing between hole centers (cm).
        cantidad: Number of holes in the pattern.
        x_centro: Pattern origin X offset (cm, default 0).
        y_centro: Pattern origin Y offset (cm, default 0).

    Returns:
        Dict with success status, message, and hole details.
    """
    try:
        # 1. Create sketch on XY plane
        provider.sketch_create("XY")

        # 2. Draw circles at evenly spaced positions
        radius = diametro / 2.0
        holes_created = 0
        for i in range(cantidad):
            x = x_centro + i * espaciado
            y = y_centro
            provider.sketch_circle(x, y, radius)
            holes_created += 1

        # 3. Extrude-cut all profiles to depth
        provider.extrude("1", profundidad, direction="positive", operation="cut")

        return {
            "success": True,
            "message": f"Created {cantidad} holes in linear pattern",
            "holes": holes_created,
            "diametro": diametro,
            "profundidad": profundidad,
            "espaciado": espaciado,
            "cantidad": cantidad,
        }

    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {
            "success": False,
            "error": str(exc),
            "holes_created": holes_created if "holes_created" in dir() else 0,
        }
    except Exception as exc:
        return {
            "success": False,
            "error": str(exc),
            "holes_created": holes_created if "holes_created" in dir() else 0,
        }