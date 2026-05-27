"""2D sketch geometry creation for Autodesk Inventor.

Provides sketch creation on work planes and drawing primitives
(line, circle, arc, rectangle) plus dimension constraints.
"""

from __future__ import annotations

import logging
from typing import Any

try:
    import win32com.client as _win32com
    _CAST_TO = getattr(_win32com, "CastTo", None)
except ImportError:
    _CAST_TO = None

from mcp_cad.providers.inventor.client import InventorDriver
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
            sketch.SketchLines.AddByTwoPoints(start, end)
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

    def sketch_point(
        self, x: float, y: float
    ) -> dict[str, Any]:
        """Draw a point in the active sketch."""
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            pt = tg.CreatePoint2d(x, y)
            sketch.SketchPoints.Add(pt)
            return {"success": True, "entity_type": "point", "x": x, "y": y}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw point: {exc}") from exc

    def sketch_spline(
        self,
        points: list[tuple[float, float]],
        fit_method: str = "sweet",
    ) -> dict[str, Any]:
        """Draw a spline through fit points."""
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            to = self._driver.inventor.TransientObjects
            col = to.CreateObjectCollection()
            for px, py in points:
                col.Add(tg.CreatePoint2d(px, py))

            # SplineFitMethodEnum values
            methods: dict[str, int] = {
                "smooth": 26369,
                "sweet": 26370,
                "autocad": 26371,
            }
            if fit_method not in methods:
                raise InventorCOMError(
                    f"Unknown fit method '{fit_method}'. Use: {', '.join(methods)}"
                )

            sketch.SketchSplines.Add(col, methods[fit_method])
            return {
                "success": True,
                "entity_type": "spline",
                "points": len(points),
                "fit_method": fit_method,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw spline: {exc}") from exc

    def sketch_ellipse(
        self,
        cx: float,
        cy: float,
        major_radius: float,
        minor_radius: float,
        major_axis_angle: float = 0.0,
    ) -> dict[str, Any]:
        """Draw an ellipse in the active sketch.

        Parameters
        ----------
        cx, cy:
            Center point.
        major_radius:
            Major axis radius in cm.
        minor_radius:
            Minor axis radius in cm.
        major_axis_angle:
            Angle of major axis in degrees (0° = +X axis).
        """
        sketch = self._ensure_active_sketch()
        try:
            import math
            tg = self._transient_geometry()
            center = tg.CreatePoint2d(cx, cy)
            rad = math.radians(major_axis_angle)
            axis_vec = tg.CreateUnitVector2d(math.cos(rad), math.sin(rad))
            sketch.SketchEllipses.Add(center, axis_vec, major_radius, minor_radius)
            return {
                "success": True,
                "entity_type": "ellipse",
                "cx": cx,
                "cy": cy,
                "major_radius": major_radius,
                "minor_radius": minor_radius,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to draw ellipse: {exc}") from exc

    def sketch_circular_pattern(
        self,
        entities: str,
        axis: str,
        count: int,
        angle: float = 360.0,
        fitted: bool = True,
        symmetric: bool = False,
    ) -> dict[str, Any]:
        """Create a circular pattern of sketch entities.

        Uses ``CastTo`` for ObjectCollection to fix pywin32 marshaling.

        Parameters
        ----------
        entities:
            Comma-separated entity indices (e.g. "1,2,3").
        axis:
            Axis entity reference — sketch point, work point, etc.
        count:
            Number of instances including the original.
        angle:
            Angle between instances (or total sweep if fitted=True).
        fitted:
            True → angle is total sweep. False → offset between instances.
        symmetric:
            Distribute on both sides of original geometry.
        """
        sketch = self._ensure_active_sketch()
        try:
            to = self._driver.inventor.TransientObjects

            # Collect sketch entities into ObjectCollection
            col = to.CreateObjectCollection()
            if _CAST_TO is not None:
                col = _CAST_TO(col, "ObjectCollection")
            for idx_str in entities.split(","):
                idx_str = idx_str.strip()
                if not idx_str:
                    continue
                ent = sketch.SketchEntities.Item(int(idx_str))
                if _CAST_TO is not None:
                    ent = _CAST_TO(ent, "Object")
                col.Add(ent)

            # Resolve axis
            if isinstance(axis, str):
                try:
                    axis_index = int(axis)
                    axis_entity = sketch.SketchPoints.Item(axis_index)
                except ValueError:
                    axis_entity = sketch.SketchPoints.Item(axis)
            else:
                axis_entity = axis
            if _CAST_TO is not None:
                axis_entity = _CAST_TO(axis_entity, "Object")

            # Create definition, set properties, add
            cp = sketch.CircularPatterns
            definition = cp.CreateDefinition()
            definition.Geometries = col
            definition.AxisEntity = axis_entity
            definition.Count = count
            definition.Angle = angle
            definition.Fitted = fitted
            definition.Symmetric = symmetric
            cp.Add(definition)

            return {
                "success": True,
                "pattern_type": "sketch_circular",
                "count": count,
                "angle": angle,
                "fitted": fitted,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to create sketch circular pattern: {exc}") from exc
