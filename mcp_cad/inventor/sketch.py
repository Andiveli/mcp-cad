"""2D sketch geometry creation for Autodesk Inventor.

Provides sketch creation on work planes and drawing primitives
(line, circle, arc, rectangle) plus dimension constraints.
"""

from __future__ import annotations

import logging
from typing import Any

from mcp_cad.inventor.client import InventorDriver
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)

# Work-plane indices in Inventor (1-based COM collection)
_PLANE_MAP: dict[str, int] = {
    "XY": 1,
    "XZ": 2,
    "YZ": 3,
}


class SketchManager:
    """Manages 2D sketch operations: create, draw, dimension.

    Receives the driver and accesses its ``inventor`` property dynamically,
    so that a late ``connect()`` call is reflected immediately.
    Tracks an ``_active_sketch`` so that draw commands know which sketch
    to target.
    """

    def __init__(self, driver: InventorDriver) -> None:
        self._driver = driver
        self._active_sketch: Any = None

    # ------------------------------------------------------------------
    # Internal guards
    # ------------------------------------------------------------------

    def _ensure_connected(self) -> None:
        """Verify that the COM reference is still alive."""
        if self._driver.inventor is None:
            raise InventorDisconnectedError(
                "Not connected to Inventor. Call connect() first."
            )

    def _ensure_active_sketch(self) -> Any:
        """Return the active sketch COM object or raise."""
        self._ensure_connected()
        if self._active_sketch is None:
            raise InventorCOMError(
                "No active sketch. Call sketch_create() first."
            )
        return self._active_sketch

    def _transient_geometry(self) -> Any:
        """Return the TransientGeometry COM object for creating 2D points."""
        return self._driver.inventor.TransientGeometry

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def sketch_create(self, plane: str = "XY") -> dict[str, Any]:
        """Create a new sketch on the specified work plane.

        Parameters
        ----------
        plane:
            Work plane identifier: "XY", "XZ", or "YZ" (default: "XY").

        Returns
        -------
        dict with sketch metadata.
        """
        self._ensure_connected()
        plane_upper = plane.upper()
        plane_index = _PLANE_MAP.get(plane_upper)
        if plane_index is None:
            raise InventorCOMError(
                f"Invalid plane '{plane}'. Must be one of: "
                f"{', '.join(sorted(_PLANE_MAP))}"
            )

        try:
            doc = self._driver.inventor.ActiveDocument
            if doc is None:
                raise InventorCOMError(
                    "No active document. Open or create a document first."
                )
            comp_def = doc.ComponentDefinition
            work_plane = comp_def.WorkPlanes.Item(plane_index)
            sketch = comp_def.Sketches.Add(work_plane)
            self._active_sketch = sketch
            return {
                "success": True,
                "sketch_name": sketch.Name,
                "plane": plane_upper,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(
                f"Failed to create sketch on plane '{plane}': {exc}"
            ) from exc

    def sketch_line(
        self, x1: float, y1: float, x2: float, y2: float
    ) -> dict[str, Any]:
        """Draw a line segment in the active sketch.

        Parameters
        ----------
        x1, y1:
            Start point coordinates.
        x2, y2:
            End point coordinates.

        Returns
        -------
        dict with line metadata.
        """
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            start = tg.CreatePoint2d(x1, y1)
            end = tg.CreatePoint2d(x2, y2)
            sketch.SketchLines.AddAsTwoPoint(start, end)
            return {
                "success": True,
                "entity_type": "line",
                "start": [x1, y1],
                "end": [x2, y2],
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw line: {exc}") from exc

    def sketch_circle(
        self, cx: float, cy: float, radius: float
    ) -> dict[str, Any]:
        """Draw a circle in the active sketch.

        Parameters
        ----------
        cx, cy:
            Center point coordinates.
        radius:
            Circle radius.

        Returns
        -------
        dict with circle metadata.
        """
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            center = tg.CreatePoint2d(cx, cy)
            sketch.SketchCircles.AddByCenterRadius(center, radius)
            return {
                "success": True,
                "entity_type": "circle",
                "center": [cx, cy],
                "radius": radius,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw circle: {exc}") from exc

    def sketch_arc(
        self,
        cx: float,
        cy: float,
        radius: float,
        start_angle: float,
        end_angle: float,
    ) -> dict[str, Any]:
        """Draw an arc in the active sketch.

        Parameters
        ----------
        cx, cy:
            Center point coordinates.
        radius:
            Arc radius.
        start_angle:
            Start angle in radians.
        end_angle:
            End angle in radians.

        Returns
        -------
        dict with arc metadata.
        """
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            center = tg.CreatePoint2d(cx, cy)
            sketch.SketchArcs.AddByCenterStartEndAngle(
                center, radius, start_angle, end_angle
            )
            return {
                "success": True,
                "entity_type": "arc",
                "center": [cx, cy],
                "radius": radius,
                "start_angle": start_angle,
                "end_angle": end_angle,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw arc: {exc}") from exc

    def sketch_rectangle(
        self, x1: float, y1: float, x2: float, y2: float
    ) -> dict[str, Any]:
        """Draw a rectangle (2-corner) in the active sketch.

        Parameters
        ----------
        x1, y1:
            First corner coordinates.
        x2, y2:
            Opposite corner coordinates.

        Returns
        -------
        dict with rectangle metadata.
        """
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            corner1 = tg.CreatePoint2d(x1, y1)
            corner2 = tg.CreatePoint2d(x2, y2)
            sketch.SketchLines.AddAsTwoPointRectangle(corner1, corner2)
            return {
                "success": True,
                "entity_type": "rectangle",
                "corner1": [x1, y1],
                "corner2": [x2, y2],
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw rectangle: {exc}") from exc

    def sketch_dimension(
        self,
        entity: str,
        value: float,
        position: tuple[float, float] | None = None,
    ) -> dict[str, Any]:
        """Add a dimension constraint to the active sketch.

        Parameters
        ----------
        entity:
            Identifier of the sketch entity to dimension.
        value:
            Dimension value (length or radius).
        position:
            Optional (x, y) placement position for the dimension text.

        Returns
        -------
        dict with dimension metadata.
        """
        sketch = self._ensure_active_sketch()
        try:
            dim_constraints = sketch.DimensionConstraints
            if position is not None:
                tg = self._transient_geometry()
                text_pos = tg.CreatePoint2d(position[0], position[1])
                dim = dim_constraints.AddLinearDimension(entity, text_pos)
            else:
                dim = dim_constraints.AddLinearDimension(entity)
            dim.Parameter.Value = value
            return {
                "success": True,
                "entity_type": "dimension",
                "entity": entity,
                "value": value,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to add dimension: {exc}") from exc
