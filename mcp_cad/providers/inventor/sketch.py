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
        mode: str,
        entity1: str,
        entity2: str = "",
        value: float | None = None,
        orientation: str = "aligned",
        position_x: float | None = None,
        position_y: float | None = None,
    ) -> dict[str, Any]:
        """Add a dimension constraint to the active sketch.

        Parameters
        ----------
        mode:
            "linear" (two points), "radius", "diameter", "angle" (two lines).
        entity1:
            First entity index (1-based).
        entity2:
            Second entity index (linear needs two points, angle needs two lines).
        value:
            Optional dimension value. If None, uses the current measured value.
        orientation:
            For linear: "aligned" (default), "horizontal", "vertical".
        position_x, position_y:
            Optional text placement position.
        """
        sketch = self._ensure_active_sketch()
        try:
            dc = sketch.DimensionConstraints
            tg = self._transient_geometry()
            e1 = self._resolve_entity(sketch, entity1)

            # Determine text position
            if position_x is not None and position_y is not None:
                text_pt = tg.CreatePoint2d(position_x, position_y)
            else:
                # Default: use entity1's geometry midpoint
                g = e1.Geometry
                try:
                    mx = (g.StartPoint.X + g.EndPoint.X) / 2.0 + 2.0
                    my = (g.StartPoint.Y + g.EndPoint.Y) / 2.0 + 2.0
                except Exception:
                    mx, my = 5.0, 5.0
                text_pt = tg.CreatePoint2d(mx, my)

            if mode == "linear":
                pt1 = sketch.SketchPoints.Item(int(entity1))
                pt2 = sketch.SketchPoints.Item(int(entity2)) if entity2 else sketch.SketchPoints.Item(int(entity1) + 1)
                orient_map = {"aligned": 19203, "horizontal": 19201, "vertical": 19202}
                orient = orient_map.get(orientation.lower(), 19203)
                dim = dc.AddTwoPointDistance(pt1, pt2, orient, text_pt)
            elif mode == "radius":
                dim = dc.AddRadius(e1, text_pt, False)
            elif mode == "diameter":
                dim = dc.AddDiameter(e1, text_pt, False)
            elif mode == "angle":
                e2 = self._resolve_entity(sketch, entity2) if entity2 else None
                if e2 is None:
                    return {"success": False, "error": "Angle mode needs entity2 (second line)"}
                dim = dc.AddTwoLineAngle(e1, e2, text_pt)
            else:
                return {"success": False, "error": f"Unknown dimension mode '{mode}'"}

            if value is not None:
                dim.Parameter.Value = value

            return {
                "success": True,
                "entity_type": "dimension",
                "mode": mode,
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
            col = self._build_entity_collection(sketch, entities)

            # Resolve center point
            try:
                axis_index = int(axis)
                center_pt = sketch.SketchPoints.Item(axis_index)
            except ValueError:
                center_pt = sketch.SketchPoints.Item(axis)

            # Get Point2d geometry for center
            cg = center_pt.Geometry
            tg = self._transient_geometry()
            cpt = tg.CreatePoint2d(cg.X, cg.Y)

            sketch.CreateCircularPattern(
                col, cpt, count, angle, fitted, symmetric,
            )

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

    def sketch_rectangular_pattern(
        self,
        entities: str,
        x_axis: str,
        x_count: int,
        x_spacing: float,
        y_axis: str = "",
        y_count: int = 1,
        y_spacing: float = 0.0,
    ) -> dict[str, Any]:
        """Create a rectangular pattern of sketch entities.

        Uses ``CastTo`` for ObjectCollection.

        Parameters
        ----------
        entities:
            Comma-separated entity indices (e.g. "1,2,3").
        x_axis:
            Linear sketch entity index for X direction.
        x_count:
            Number of instances in X direction.
        x_spacing:
            Spacing in X direction (cm).
        y_axis:
            Linear sketch entity index for Y direction (optional).
        y_count:
            Number of instances in Y direction.
        y_spacing:
            Spacing in Y direction (cm).
        """
        sketch = self._ensure_active_sketch()
        try:
            col = self._build_entity_collection(sketch, entities)

            # Resolve direction entities with Dispatch
            x_dir = sketch.SketchEntities.Item(int(x_axis))
            try:
                import win32com.client
                x_dir = win32com.client.Dispatch(x_dir)
            except Exception:
                pass

            rp = sketch.RectangularPatterns

            if y_axis:
                y_dir = sketch.SketchEntities.Item(int(y_axis))
                try:
                    import win32com.client
                    y_dir = win32com.client.Dispatch(y_dir)
                except Exception:
                    pass
                definition = rp.CreateDefinition(
                    col, x_dir, x_count,
                    None,  # NaturalXDirection (default True)
                    None,  # XDirectionSymmetric
                    x_spacing,
                    y_dir, y_count,
                    None,  # NaturalYDirection
                    None,  # YDirectionSymmetric
                    y_spacing,
                )
            else:
                definition = rp.CreateDefinition(
                    col, x_dir, x_count,
                    None, None, x_spacing,
                )

            rp.Add(definition)

            return {
                "success": True,
                "pattern_type": "sketch_rectangular",
                "x_count": x_count,
                "y_count": y_count,
                "x_spacing": x_spacing,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to create sketch rectangular pattern: {exc}") from exc

    def _build_entity_collection(self, sketch: Any, entities: str) -> Any:
        """Build an ObjectCollection from comma-separated indices.

        Uses ``Dispatch()`` on each entity to force late binding
        and avoid gen_py cache corruption.
        """
        to = self._driver.inventor.TransientObjects
        col = to.CreateObjectCollection()
        for idx_str in entities.split(","):
            idx_str = idx_str.strip()
            if not idx_str:
                continue
            ent = sketch.SketchEntities.Item(int(idx_str))
            try:
                import win32com.client
                ent = win32com.client.Dispatch(ent)
            except Exception:
                pass
            col.Add(ent)
        return col

    def sketch_offset(
        self,
        entities: str,
        distance: float,
        natural_direction: bool = True,
    ) -> dict[str, Any]:
        """Offset sketch entities using a point for direction.

        Uses ``OffsetSketchEntitiesUsingPoint`` — the first entity's
        geometry midpoint serves as the base point.
        """
        sketch = self._ensure_active_sketch()
        try:
            col = self._build_entity_collection(sketch, entities)

            # Use first entity's geometry midpoint as base point
            first = sketch.SketchEntities.Item(
                int(entities.split(",")[0].strip())
            )
            g = first.Geometry
            mid_x = (g.StartPoint.X + g.EndPoint.X) / 2.0
            mid_y = (g.StartPoint.Y + g.EndPoint.Y) / 2.0
            tg = self._transient_geometry()
            base_pt = tg.CreatePoint2d(mid_x, mid_y)

            sketch.OffsetSketchEntitiesUsingPoint(
                col, base_pt, distance, natural_direction,
            )
            return {"success": True, "operation": "offset", "distance": distance}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to offset sketch: {exc}") from exc

    def sketch_move(
        self,
        entities: str,
        dx: float,
        dy: float,
        copy: bool = False,
    ) -> dict[str, Any]:
        """Move sketch entities by a vector."""
        sketch = self._ensure_active_sketch()
        try:
            col = self._build_entity_collection(sketch, entities)
            tg = self._transient_geometry()
            vec = tg.CreateVector2d(dx, dy)
            sketch.MoveSketchObjects(col, vec, copy, False)
            return {"success": True, "operation": "move", "dx": dx, "dy": dy, "copy": copy}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to move sketch: {exc}") from exc

    def sketch_rotate(
        self,
        entities: str,
        cx: float,
        cy: float,
        angle: float,
        copy: bool = False,
    ) -> dict[str, Any]:
        """Rotate sketch entities around a center point."""
        sketch = self._ensure_active_sketch()
        try:
            import math
            col = self._build_entity_collection(sketch, entities)
            tg = self._transient_geometry()
            center = tg.CreatePoint2d(cx, cy)
            sketch.RotateSketchObjects(col, center, math.radians(angle), copy, False)
            return {"success": True, "operation": "rotate", "angle": angle, "cx": cx, "cy": cy, "copy": copy}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to rotate sketch: {exc}") from exc

    def sketch_delete(self) -> dict[str, Any]:
        """Delete the active sketch. Only valid for sketches not used by a feature."""
        sketch = self._ensure_active_sketch()
        try:
            sketch.Delete()
            return {"success": True, "operation": "delete_sketch"}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to delete sketch: {exc}") from exc

    def sketch_trim(
        self,
        entity: str,
        cutting_entity: str,
        side: str = "end",
    ) -> dict[str, Any]:
        """Trim a sketch entity to its intersection with another entity.

        Uses ``Geometry.Intersect()`` to find the intersection point,
        then moves the specified endpoint to that point.

        Parameters
        ----------
        entity:
            Index of the entity to trim (1-based sketch entity).
        cutting_entity:
            Index of the entity to trim against.
        side:
            Which endpoint to move — ``"start"`` or ``"end"``.
        """
        sketch = self._ensure_active_sketch()
        try:
            ent = sketch.SketchEntities.Item(int(entity))
            cut = sketch.SketchEntities.Item(int(cutting_entity))

            # Get 2D geometries and intersect
            geo1 = ent.Geometry
            geo2 = cut.Geometry
            pts = geo1.Intersect(geo2)

            if pts is None or pts.Count == 0:
                return {"success": False, "error": "Entities do not intersect"}

            pt = pts.Item(1)
            tg = self._transient_geometry()
            target = tg.CreatePoint2d(pt.X, pt.Y)

            # Move the specified endpoint
            if side == "start":
                ent.StartSketchPoint.MoveTo(target)
            else:
                ent.EndSketchPoint.MoveTo(target)

            return {"success": True, "operation": "trim", "side": side}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to trim: {exc}") from exc

    def sketch_scale(
        self,
        entities: str,
        cx: float,
        cy: float,
        factor: float,
    ) -> dict[str, Any]:
        """Scale sketch entities around a center point.

        Uses ``Geometry`` to read current positions, computes scaled
        coordinates, and moves endpoints via ``MoveTo``.

        Parameters
        ----------
        entities:
            Comma-separated entity indices.
        cx, cy:
            Scale center point.
        factor:
            Scale factor (>1 = enlarge, <1 = shrink).
        """
        sketch = self._ensure_active_sketch()
        try:
            tg = self._transient_geometry()
            for idx_str in entities.split(","):
                idx_str = idx_str.strip()
                if not idx_str:
                    continue
                ent = sketch.SketchEntities.Item(int(idx_str))
                start = ent.StartSketchPoint
                end = ent.EndSketchPoint

                sg = start.Geometry  # Point2d
                eg = end.Geometry

                nx1 = cx + (sg.X - cx) * factor
                ny1 = cy + (sg.Y - cy) * factor
                nx2 = cx + (eg.X - cx) * factor
                ny2 = cy + (eg.Y - cy) * factor

                start.MoveTo(tg.CreatePoint2d(nx1, ny1))
                end.MoveTo(tg.CreatePoint2d(nx2, ny2))

            return {"success": True, "operation": "scale", "factor": factor, "cx": cx, "cy": cy}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to scale: {exc}") from exc

    def sketch_mirror(
        self,
        entities: str,
        mirror_entity: str,
    ) -> dict[str, Any]:
        """Mirror sketch entities across a mirror line.

        Uses ``Geometry`` to read positions, computes reflected
        coordinates across the mirror line, and moves endpoints.

        Parameters
        ----------
        entities:
            Comma-separated entity indices to mirror.
        mirror_entity:
            Index of the line entity used as the mirror axis.
        """
        sketch = self._ensure_active_sketch()
        try:
            mirror_line = sketch.SketchEntities.Item(int(mirror_entity))

            # Get mirror axis line endpoints
            mg = mirror_line.Geometry  # LineSegment2d
            ax1, ay1 = mg.StartPoint.X, mg.StartPoint.Y
            ax2, ay2 = mg.EndPoint.X, mg.EndPoint.Y

            # Direction vector of the mirror line
            vx = ax2 - ax1
            vy = ay2 - ay1
            vlen2 = vx * vx + vy * vy

            if vlen2 < 1e-12:
                return {"success": False, "error": "Mirror line has zero length"}

            tg = self._transient_geometry()

            for idx_str in entities.split(","):
                idx_str = idx_str.strip()
                if not idx_str:
                    continue
                ent = sketch.SketchEntities.Item(int(idx_str))
                start = ent.StartSketchPoint
                end = ent.EndSketchPoint

                sg = start.Geometry  # Point2d
                eg = end.Geometry

                # Reflect start point
                dx, dy = sg.X - ax1, sg.Y - ay1
                t = (dx * vx + dy * vy) / vlen2
                cx, cy = ax1 + t * vx, ay1 + t * vy
                rx1, ry1 = 2.0 * cx - sg.X, 2.0 * cy - sg.Y

                # Reflect end point
                dx, dy = eg.X - ax1, eg.Y - ay1
                t = (dx * vx + dy * vy) / vlen2
                cx, cy = ax1 + t * vx, ay1 + t * vy
                rx2, ry2 = 2.0 * cx - eg.X, 2.0 * cy - eg.Y

                start.MoveTo(tg.CreatePoint2d(rx1, ry1))
                end.MoveTo(tg.CreatePoint2d(rx2, ry2))

            return {"success": True, "operation": "mirror"}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to mirror: {exc}") from exc

    def _resolve_entity(self, sketch: Any, ref: str) -> Any:
        """Resolve a sketch entity from a 1-based index string.

        Wraps in ``win32com.client.Dispatch()`` to force a fresh
        late-bound wrapper, then casts to ``SketchEntity``.
        """
        ent = sketch.SketchEntities.Item(int(ref.strip()))
        try:
            import win32com.client
            ent = win32com.client.Dispatch(ent)
            ent = win32com.client.CastTo(ent, "SketchEntity")
        except Exception:
            pass
        return ent

    def sketch_constraint(
        self,
        mode: str,
        entity1: str,
        entity2: str = "",
        sym_line: str = "",
        axis: str = "major",
    ) -> dict[str, Any]:
        """Add a geometric constraint between sketch entities.

        Parameters
        ----------
        mode:
            Constraint type: coincident, collinear, concentric, parallel,
            perpendicular, tangent, horizontal, vertical, equal,
            midpoint, symmetric, smooth.
        entity1:
            First entity index (1-based).
        entity2:
            Second entity index (for two-entity constraints).
        axis:
            For ellipses: "major" (default) or "minor".
        """
        sketch = self._ensure_active_sketch()
        try:
            gc = sketch.GeometricConstraints
            e1 = self._resolve_entity(sketch, entity1)
            e2 = None
            if entity2:
                e2 = self._resolve_entity(sketch, entity2)

            use_major = axis.lower() != "minor"

            if mode == "coincident":
                gc.AddCoincident(e1, e2)
            elif mode == "collinear":
                gc.AddCollinear(e1, e2, use_major, use_major)
            elif mode == "concentric":
                gc.AddConcentric(e1, e2)
            elif mode == "parallel":
                gc.AddParallel(e1, e2, use_major, use_major)
            elif mode == "perpendicular":
                gc.AddPerpendicular(e1, e2, use_major, use_major)
            elif mode == "tangent":
                gc.AddTangent(e1, e2)
            elif mode == "equal":
                gc.AddEqualLength(e1, e2)
            elif mode == "midpoint":
                gc.AddMidpoint(e1, e2)
            elif mode == "symmetric":
                if not sym_line:
                    return {"success": False, "error": "Symmetric constraint needs sym_line parameter"}
                sym_entity = self._resolve_entity(sketch, sym_line)
                gc.AddSymmetry(e1, e2, sym_entity)
            elif mode == "smooth":
                gc.AddSmooth(e1, e2)
            elif mode == "horizontal":
                gc.AddHorizontal(e1, use_major)
            elif mode == "vertical":
                gc.AddVertical(e1, use_major)
            else:
                return {"success": False, "error": f"Unknown constraint mode '{mode}'"}

            return {"success": True, "constraint": mode}
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to add {mode} constraint: {exc}") from exc
