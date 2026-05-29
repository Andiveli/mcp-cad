"""3D feature operations for Autodesk Inventor.

Provides extrude, revolve, fillet, and chamfer features via COM automation.
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
from mcp_cad.providers.inventor.attributes import TagStore
from mcp_cad.providers.inventor.attributes import inspect_sketch as _inspect_sketch
from mcp_cad.providers.inventor.attributes import bridge_revolve as _bridge_revolve
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)

# ------------------------------------------------------------------
# COM enumeration constants for Inventor 2025+
#
# These values come from the Inventor type library via early binding:
#     from win32com.client import gencache, constants
#     app = gencache.EnsureDispatch("Inventor.Application")
#     print(constants.kNewBodyOperation)  # → 20485
#
# Late-bound Dispatch requires hard-coded values.
# NOTE: EnsureDispatch corrupts the cache for regular Dispatch;
#       clear ``%LOCALAPPDATA%\\Temp\\gen_py`` after using it.
# ------------------------------------------------------------------

# PartFeatureOperationEnum (Inventor 2025+)
_NEW_BODY_OPERATION = 20485
_JOIN_OPERATION = 20481
_CUT_OPERATION = 20482
_INTERSECT_OPERATION = 20483

_OPERATION_MAP: dict[str, int] = {
    "new_body": _NEW_BODY_OPERATION,
    "join": _JOIN_OPERATION,
    "cut": _CUT_OPERATION,
    "intersect": _INTERSECT_OPERATION,
}

# PartFeatureExtentDirectionEnum (Inventor 2025+)
_DIRECTION_MAP: dict[str, int] = {
    "positive": 20993,  # kPositiveExtentDirection
    "negative": 20994,  # kNegativeExtentDirection
    "both": 20995,      # kSymmetricExtentDirection
}


class FeatureManager:
    """Manages 3D feature operations: extrude, revolve, fillet, chamfer.

    Receives the driver and accesses its ``inventor`` property dynamically,
    so that a late ``connect()`` call is reflected immediately.
    """

    def __init__(self, driver: InventorDriver) -> None:
        self._driver = driver

    # ------------------------------------------------------------------
    # Internal guards
    # ------------------------------------------------------------------

    def _ensure_connected(self) -> None:
        """Verify that the COM reference is still alive."""
        if self._driver.inventor is None:
            raise InventorDisconnectedError(
                "Not connected to Inventor. Call connect() first."
            )

    def _ensure_active_document(self) -> Any:
        """Return the active document COM object or raise."""
        self._ensure_connected()
        doc = self._driver.inventor.ActiveDocument
        if doc is None:
            raise InventorCOMError("No active document.")
        try:
            import win32com.client
            doc = win32com.client.Dispatch(doc)
        except Exception:
            pass
        return doc

    def _resolve_profile(self, profile: Any) -> Any:
        """Resolve a profile reference from a name string or pass through.

        If *profile* is a string that looks like an integer, convert it
        to int and use it as a 1-based index into the Profiles collection.
        If profiles are empty, auto-constrains sketch lines to close open
        loops before calling AddForSolid().
        If it is already a COM object, return it unchanged.
        """
        if isinstance(profile, str):
            doc = self._ensure_active_document()
            try:
                comp_def = doc.ComponentDefinition
                # Use the last sketch (most likely the one just drawn on)
                sketches = comp_def.Sketches
                sketch = sketches.Item(sketches.Count)
                profiles = sketch.Profiles
                # Force profile creation if the collection is empty
                if profiles.Count == 0:
                    # Try auto-constraining first (close gaps from manual drawing)
                    self._auto_constrain_sketch(sketch)
                    # Try surface first — manual lines may form a surface profile
                    # before AddForSolid can detect them.
                    try:
                        profiles.AddForSurface()
                    except Exception:
                        pass
                    try:
                        profiles.AddForSolid()
                    except Exception:
                        pass
                # Try integer index first (most reliable for COM bridge)
                try:
                    index = int(profile)
                    return profiles.Item(index)
                except ValueError:
                    return profiles.Item(profile)
            except Exception as exc:
                raise InventorCOMError(
                    f"Failed to resolve profile '{profile}': {exc}"
                ) from exc
        return profile

    @staticmethod
    def _auto_constrain_sketch(sketch: Any) -> None:
        """Add coincident constraints to close sketch lines into a profile.

        Constrains each line's endpoint to the next line's start point
        (sequential adjacency), then closes the loop.  Best-effort.
        """
        try:
            lines = sketch.SketchLines
            gc = sketch.GeometricConstraints
            count = lines.Count

            # Sequential adjacency: line[i].end → line[i+1].start
            for i in range(1, count):
                try:
                    ei = lines.Item(i).EndSketchPoint
                    sj = lines.Item(i + 1).StartSketchPoint
                    gc.AddCoincident(ei, sj)
                except Exception:
                    pass

            # Close the loop: line[1].start → line[count-1].end
            if count >= 3:
                try:
                    s1 = lines.Item(1).StartSketchPoint
                    en = lines.Item(count - 1).EndSketchPoint
                    gc.AddCoincident(s1, en)
                except Exception:
                    pass
        except Exception:
            pass

    def _resolve_feature(self, feature_ref: Any, comp_def: Any = None) -> Any:
        """Resolve a feature reference from name string or pass through.

        If *feature_ref* is a string, tries to find it in the Features
        collection.  If it's already a COM object, return it unchanged.
        """
        if isinstance(feature_ref, str):
            doc = self._ensure_active_document()
            if comp_def is None:
                comp_def = doc.ComponentDefinition
            try:
                features = comp_def.Features
                try:
                    index = int(feature_ref)
                    return features.Item(index)
                except ValueError:
                    return features.Item(feature_ref)
            except Exception as exc:
                raise InventorCOMError(
                    f"Failed to resolve feature '{feature_ref}': {exc}"
                ) from exc
        return feature_ref

    @staticmethod
    def _find_sketchline_by_entity(sketch: Any, entity_idx: int) -> Any:
        """Find a SketchLine that matches a given SketchEntities index."""
        try:
            target = sketch.SketchEntities.Item(entity_idx)
            # Check if it's a SketchLine directly
            try:
                import win32com.client
                cast = getattr(win32com.client, "CastTo", None)
                if cast is not None:
                    return cast(target, "SketchLine")
            except Exception:
                pass
            # Fallback: search SketchLines for matching geometry
            count = sketch.SketchLines.Count
            for i in range(1, count + 1):
                line = sketch.SketchLines.Item(i)
                # Compare by Dispatch identity — crude but effective
                try:
                    if line.StartSketchPoint.Geometry.X == target.StartSketchPoint.Geometry.X and \
                       line.StartSketchPoint.Geometry.Y == target.StartSketchPoint.Geometry.Y:
                        return line
                except Exception:
                    pass
            return None
        except Exception:
            return None

    def _resolve_axis(self, axis_ref: Any, comp_def: Any = None) -> Any:
        """Resolve an axis reference from name string or index.

        Resolution order (revolve axis is a sketch line, not a 3D edge):
        1. Sketch line in the most recent sketch (revolve use case)
        2. 3D edge from SurfaceBodies (circular_pattern use case)
        3. WorkAxes
        4. Features

        If already a COM object, return unchanged.
        """
        if isinstance(axis_ref, str):
            doc = self._ensure_active_document()
            if comp_def is None:
                comp_def = doc.ComponentDefinition

            # Tag-based resolution: @name → check in-memory store
            if axis_ref.startswith("@"):
                sketches = comp_def.Sketches
                sketch_idx = sketches.Count
                entity_idx = TagStore.resolve(sketch_idx, axis_ref[1:])
                if entity_idx is not None:
                    sketch = sketches.Item(sketch_idx)
                    # Find the SketchLine that matches entity_idx
                    # (entity index ≠ SketchLine index due to points etc.)
                    ent = self._find_sketchline_by_entity(sketch, entity_idx)
                    if ent is not None:
                        try:
                            import win32com.client
                            ent = win32com.client.Dispatch(ent)
                        except Exception:
                            pass
                        return ent
                raise InventorCOMError(
                    "No entity tagged '@" + axis_ref[1:] + "' found "
                    "in the active sketch. Use sketch_line(tag=\"...\") "
                    "or sketch_circle(tag=\"...\") to tag."
                )

            try:
                # "last" → last SketchLine in the most recent sketch
                if axis_ref.lower() == "last":
                    sketches = comp_def.Sketches
                    sketch = sketches.Item(sketches.Count)
                    ent = sketch.SketchLines.Item(sketch.SketchLines.Count)
                    try:
                        import win32com.client
                        ent = win32com.client.Dispatch(ent)
                    except Exception:
                        pass
                    return ent
                # Try integer index as a SketchLine (revolve axis).
                # AddFull/AddByAngle require a SketchLine, not a generic
                # SketchEntity.  Use SketchLines index, not entity index.
                try:
                    index = int(axis_ref)
                    sketches = comp_def.Sketches
                    sketch = sketches.Item(sketches.Count)
                    ent = sketch.SketchLines.Item(index)
                    try:
                        import win32com.client
                        ent = win32com.client.Dispatch(ent)
                    except Exception:
                        pass
                    return ent
                except ValueError:
                    pass
                # Try integer index as 3D edge (circular_pattern use case)
                try:
                    index = int(axis_ref)
                    sb = comp_def.SurfaceBodies.Item(1)
                    return sb.Edges.Item(index)
                except (ValueError, Exception):
                    pass
                # Try WorkAxes by name
                try:
                    return comp_def.WorkAxes.Item(axis_ref)
                except Exception:
                    pass
                # Fallback: try Features (user might pass an axis feature)
                return comp_def.Features.Item(axis_ref)
            except Exception as exc:
                raise InventorCOMError(
                    f"Failed to resolve axis '{axis_ref}': {exc}"
                ) from exc
        return axis_ref

    @staticmethod
    def _parse_edge_indices(edges: str) -> list[int]:
        """Parse a comma-separated string of 1-based edge indices.

        Accepts strings like ``"1"``, ``"1,3,5"``, or ``"1, 3, 5"``.
        Returns a list of integers. Raises ``InventorCOMError`` if any
        value is not a positive integer.
        """
        indices: list[int] = []
        for part in edges.split(","):
            stripped = part.strip()
            if stripped == "":
                continue
            try:
                idx = int(stripped)
            except ValueError:
                raise InventorCOMError(
                    f"Invalid edge index '{stripped}'. Must be a positive integer."
                )
            if idx < 1:
                raise InventorCOMError(
                    f"Edge index must be >= 1, got {idx}."
                )
            indices.append(idx)
        return indices

    def _build_edge_collection(self, edges: Any, comp_def: Any) -> Any:
        """Build an Inventor EdgeCollection from the *edges* parameter.

        - ``None`` / empty string → all edges of SurfaceBodies.Item(1).
        - ``str`` like ``"1"`` or ``"1,3,5"`` → specific edge indices.
        - Any other value (COM EdgeCollection) → passed through directly.
        """
        to = self._driver.inventor.TransientObjects
        edge_col = to.CreateEdgeCollection()
        sb = comp_def.SurfaceBodies.Item(1)

        if edges is None or (isinstance(edges, str) and edges.strip() == ""):
            # Default: all edges of the first surface body
            for i in range(1, sb.Edges.Count + 1):
                edge_col.Add(sb.Edges.Item(i))
        elif isinstance(edges, str):
            for idx in self._parse_edge_indices(edges):
                edge_col.Add(sb.Edges.Item(idx))
        else:
            # COM EdgeCollection or single edge — pass through
            edge_col.Add(edges)

        return edge_col

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def extrude(
        self,
        profile: Any,
        distance: float,
        direction: str = "positive",
        taper: float = 0.0,
        operation: str = "new_body",
    ) -> dict[str, Any]:
        """Extrude a sketch profile to create a 3D feature.

        Parameters
        ----------
        profile:
            Sketch profile name (str) or COM profile reference.
        distance:
            Extrusion distance (cm in Inventor's internal units).
        direction:
            "positive", "negative", or "both" (default: "positive").
        taper:
            Taper angle in radians (default: 0).
        operation:
            "new_body", "join", "cut", or "intersect" (default: "new_body").
            Use "new_body" for the first feature on a part; "join" / "cut"
            / "intersect" modify an existing body.

        Returns
        -------
        dict with feature metadata.
        """
        self._ensure_connected()

        dir_value = _DIRECTION_MAP.get(direction.lower())
        if dir_value is None:
            raise InventorCOMError(
                f"Invalid direction '{direction}'. Must be one of: "
                f"{', '.join(sorted(_DIRECTION_MAP))}"
            )

        op_value = _OPERATION_MAP.get(operation.lower())
        if op_value is None:
            raise InventorCOMError(
                f"Invalid operation '{operation}'. Must be one of: "
                f"{', '.join(sorted(_OPERATION_MAP))}"
            )

        resolved = self._resolve_profile(profile)
        doc = self._ensure_active_document()

        try:
            comp_def = doc.ComponentDefinition
            features = comp_def.Features

            # Inventor 2025 API: second arg is PartFeatureOperationEnum,
            # NOT the distance — distance is set below via SetDistanceExtent.
            extrude_def = features.ExtrudeFeatures.CreateExtrudeDefinition(
                resolved, op_value
            )
            # SetDistanceExtent replaces the deprecated .Direction property.
            # Direction values are PartFeatureExtentDirectionEnum (20993+).
            extrude_def.SetDistanceExtent(distance, dir_value)
            # TaperAngle expects a string with unit suffix (e.g. "2 deg").
            extrude_def.TaperAngle = f"{taper} deg"
            features.ExtrudeFeatures.Add(extrude_def)
            return {
                "success": True,
                "feature_type": "extrude",
                "distance": distance,
                "direction": direction.lower(),
                "operation": operation.lower(),
                "taper": taper,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to extrude: {exc}") from exc

    def revolve(
        self,
        profile: Any,
        axis: Any,
        angle: float = 360.0,
        direction: str = "positive",
        operation: str = "join",
    ) -> dict[str, Any]:
        """Revolve a profile around an axis to create a 3D feature.

        Uses ``AddFull`` (360°) or ``AddByAngle`` (partial).  The axis
        **must** be a sketch line in the same sketch that generated the
        profile.  Valid operations: ``join``, ``cut``, ``intersect``
        (``new_body`` is not supported by RevolveFeatures).

        Parameters
        ----------
        profile:
            Sketch profile name (str) or COM profile reference.
        axis:
            Axis of revolution — sketch line index (str) or COM object.
        angle:
            Revolution angle in degrees (default: 360 for full revolve).
        direction:
            "positive", "negative", or "both" (AddByAngle only).
        operation:
            "join", "cut", or "intersect" (default: "join").
            Note: ``new_body`` is not valid for revolve — use ``join``
            for the first feature on a part.

        Returns
        -------
        dict with feature metadata.
        """
        import math

        self._ensure_connected()

        op_value = _OPERATION_MAP.get(operation.lower())
        if op_value is None:
            raise InventorCOMError(
                f"Invalid operation '{operation}'. Must be one of: "
                f"{', '.join(sorted(_OPERATION_MAP))}"
            )

        resolved = self._resolve_profile(profile)
        doc = self._ensure_active_document()
        comp_def = doc.ComponentDefinition

        # Resolve axis entity — must be a SketchLine from the same sketch
        resolved_axis = self._resolve_axis(axis, comp_def)

        try:
            # Try C# bridge first — AddForSolid works correctly there
            sketch_idx = comp_def.Sketches.Count
            # Resolve axis as SketchLine index
            axis_ref = axis
            if isinstance(axis_ref, str) and axis_ref.startswith("@"):
                ent_idx = TagStore.resolve(sketch_idx, axis_ref[1:])
                if ent_idx is not None:
                    try:
                        # Use the matching SketchLine index
                        sketch = comp_def.Sketches.Item(sketch_idx)
                        for k in range(1, sketch.SketchLines.Count + 1):
                            try:
                                sl = sketch.SketchLines.Item(k)
                                se = sketch.SketchEntities.Item(ent_idx)
                                if (sl.StartSketchPoint.Geometry.X == se.StartSketchPoint.Geometry.X and
                                    sl.StartSketchPoint.Geometry.Y == se.StartSketchPoint.Geometry.Y):
                                    axis_ref = str(k)
                                    break
                            except Exception:
                                pass
                    except Exception:
                        pass

            if isinstance(axis_ref, str):
                try:
                    axis_int = int(axis_ref)
                    result = _bridge_revolve(
                        sketch_idx, axis_int, angle, operation,
                    )
                    if result.get("ok") and result.get("data", {}).get("success"):
                        return result["data"]
                except Exception:
                    pass  # Bridge unavailable — fall through to Python COM

            # Python COM path (fallback)
            rf = comp_def.Features.RevolveFeatures
            try:
                import win32com.client
                rf = win32com.client.Dispatch(rf)
            except Exception:
                pass

            if angle >= 360.0 - 0.001:
                rf.AddFull(resolved, resolved_axis, op_value)
            else:
                dir_value = _DIRECTION_MAP.get(direction.lower())
                if dir_value is None:
                    raise InventorCOMError(
                        f"Invalid direction '{direction}'."
                    )
                angle_rad = math.radians(angle)
                rf.AddByAngle(resolved, resolved_axis, angle_rad, dir_value, op_value)

            return {
                "success": True,
                "feature_type": "revolve",
                "angle": angle,
                "operation": operation.lower(),
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to revolve: {exc}") from exc

    def circular_pattern(
        self,
        profile: Any,
        axis: Any,
        count: int,
        angle: float = 360.0,
        fit_within_angle: bool = True,
        natural_direction: bool = True,
    ) -> dict[str, Any]:
        """Create a circular pattern of a feature around an axis.

        Uses ``CastTo`` to fix pywin32 ObjectCollection marshaling
        when passing features to ``CreateDefinition``.

        Parameters
        ----------
        profile:
            Feature to pattern — resolved via ``_resolve_profile``.
        axis:
            Axis entity — a linear edge, work axis, or face.
            Resolved like profile (name str or COM object).
        count:
            Number of instances including the original.
        angle:
            Total sweep angle or offset angle in degrees.
        fit_within_angle:
            True → angle is total sweep (instances spaced evenly).
            False → angle is offset between each instance.
        natural_direction:
            Use natural axis direction (default True).
        """
        self._ensure_connected()
        doc = self._ensure_active_document()

        try:
            comp_def = doc.ComponentDefinition
            features = comp_def.Features
            to = self._driver.inventor.TransientObjects

            # Resolve parent feature to pattern
            resolved_feature = self._resolve_feature(profile, comp_def)

            # Build ObjectCollection with CastTo
            col = to.CreateObjectCollection()
            if _CAST_TO is not None:
                col = _CAST_TO(col, "ObjectCollection")
                parent = _CAST_TO(resolved_feature, "PartFeature")
                col.Add(parent)
            else:
                col.Add(resolved_feature)

            # Resolve axis entity
            resolved_axis = self._resolve_axis(axis, comp_def)

            # Use CastTo on the axis entity too
            if _CAST_TO is not None:
                resolved_axis = _CAST_TO(resolved_axis, "Object")

            # Create the pattern definition
            cp_features = features.CircularPatternFeatures
            pattern_def = cp_features.CreateDefinition(
                col,
                resolved_axis,
                natural_direction,
                count,
                angle,
                fit_within_angle,
            )
            cp_features.Add(pattern_def)

            return {
                "success": True,
                "feature_type": "circular_pattern",
                "count": count,
                "angle": angle,
                "fit_within_angle": fit_within_angle,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to create circular pattern: {exc}") from exc

    def fillet(
        self,
        edges: Any,
        radius: float,
        mode: str = "constant",
    ) -> dict[str, Any]:
        """Apply a fillet to the specified edges.

        In Inventor 2025+, fillets use AddSimple for constant-radius fillets
        instead of the old CreateFilletDefinition + Add pattern.

        Parameters
        ----------
        edges:
            Edge or edge collection COM object, a string of comma-separated
            1-based edge indices (e.g. "1,3,5"), or None/empty to apply to
            all edges of the first surface body.
        radius:
            Fillet radius (cm in Inventor's internal units).
        mode:
            "constant" (default). "variable" not yet supported in 2025+ API.

        Returns
        -------
        dict with feature metadata including ``edges_applied`` count.
        """
        self._ensure_connected()

        if mode not in ("constant",):
            raise InventorCOMError(
                f"Invalid fillet mode '{mode}'. Must be one of: constant"
            )

        doc = self._ensure_active_document()

        try:
            comp_def = doc.ComponentDefinition
            features = comp_def.Features

            # Build an EdgeCollection via TransientObjects
            edge_col = self._build_edge_collection(edges, comp_def)

            feature = features.FilletFeatures.AddSimple(edge_col, radius)
            return {
                "success": True,
                "feature_type": "fillet",
                "radius": radius,
                "mode": mode,
                "edges_applied": edge_col.Count,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to create fillet: {exc}") from exc

    def chamfer(
        self,
        edges: Any,
        distance: float,
        mode: str = "equal_distance",
    ) -> dict[str, Any]:
        """Apply a chamfer to the specified edges.

        In Inventor 2025+, chamfers use direct convenience methods
        (AddUsingDistance / AddUsingTwoDistances) instead of the old
        CreateChamferDefinition + Add pattern.

        Parameters
        ----------
        edges:
            Edge or edge collection COM object, a string of comma-separated
            1-based edge indices (e.g. "1,3,5"), or None/empty to apply to
            all edges of the first surface body.
        distance:
            Chamfer distance (cm in Inventor's internal units).
        mode:
            "equal_distance" or "two_distances" (default: "equal_distance").

        Returns
        -------
        dict with feature metadata including ``edges_applied`` count.
        """
        self._ensure_connected()

        if mode not in ("equal_distance", "two_distances"):
            raise InventorCOMError(
                f"Invalid chamfer mode '{mode}'. Must be one of: "
                "equal_distance, two_distances"
            )

        doc = self._ensure_active_document()

        try:
            comp_def = doc.ComponentDefinition
            features = comp_def.Features

            # Build an EdgeCollection via TransientObjects
            edge_col = self._build_edge_collection(edges, comp_def)

            if mode == "equal_distance":
                feature = features.ChamferFeatures.AddUsingDistance(
                    edge_col, distance
                )
            else:
                # two_distances — use same distance for both sides
                feature = features.ChamferFeatures.AddUsingTwoDistances(
                    edge_col, distance, distance
                )

            return {
                "success": True,
                "feature_type": "chamfer",
                "distance": distance,
                "mode": mode,
                "edges_applied": edge_col.Count,
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to create chamfer: {exc}") from exc