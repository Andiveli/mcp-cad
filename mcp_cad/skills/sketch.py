"""Tab: Sketch — creates or activates a sketch on a work plane.

Panel: (base operation)

Examples:
    skill_sketch("XY")     # sketch on XY plane
    skill_sketch("XZ")     # sketch on XZ plane
    skill_sketch("YZ")     # sketch on YZ plane
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_sketch(
    provider: CADProvider,
    plane: str = "XY",
) -> dict[str, Any]:
    """Create or activate a sketch on the specified work plane.

    Call this before any draw skill (skill_line, skill_circle, etc.)
    to ensure a sketch is active.

    Args:
        plane: Work plane — "XY" (default), "XZ", or "YZ".

    Returns:
        dict with ``success``, ``plane``, and sketch metadata.

    Examples:
        skill_sketch("XY")
        skill_sketch("XZ")
    """
    try:
        return provider.sketch_create(plane)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
