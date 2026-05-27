"""Tab: Sketch → Panel: Constrain — geometric constraints.

Modes: coincident, collinear, concentric, parallel, perpendicular,
tangent, horizontal, vertical, equal, midpoint, symmetric, smooth.

Examples:
    # Make entity 2 parallel to entity 1
    skill_constraint(mode="parallel", entity1="1", entity2="2")

    # Make entity 1 horizontal
    skill_constraint(mode="horizontal", entity1="1")

    # Symmetric: entities 1 and 2 about symmetry line 3
    skill_constraint(mode="symmetric", entity1="1", entity2="2", sym_line="3")
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError


def skill_constraint(
    provider: CADProvider,
    mode: str = "parallel",
    entity1: str = "1",
    entity2: str = "",
    sym_line: str = "",
    axis: str = "major",
) -> dict[str, Any]:
    """Add a geometric constraint between sketch entities.

    Args:
        mode: Constraint type.
        entity1: First entity index (1-based).
        entity2: Second entity index (two-entity constraints).
        sym_line: Symmetry line index (symmetric mode only).
        axis: "major" (default) or "minor" for ellipse constraints.

    Examples:
        skill_constraint(mode="parallel", entity1="1", entity2="2")
        skill_constraint(mode="horizontal", entity1="1")
        skill_constraint(mode="symmetric", entity1="1", entity2="2", sym_line="3")
    """
    try:
        return provider.sketch_constraint(mode, entity1, entity2, sym_line, axis)
    except (InventorDisconnectedError, InventorCOMError) as exc:
        return {"success": False, "error": str(exc)}
    except Exception as exc:
        return {"success": False, "error": str(exc)}
