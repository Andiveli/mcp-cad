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
        return doc

    def _resolve_profile(self, profile: Any) -> Any:
        """Resolve a profile reference from a name string or pass through.

        If *profile* is a string that looks like an integer, convert it
        to int and use it as a 1-based index into the Profiles collection.
        If profiles are empty, call AddForSolid() to force profile creation.
        Otherwise, treat it as a profile name string.
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
                    profiles.AddForSolid()
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

    def _resolve_axis(self, axis_ref: Any, comp_def: Any = None) -> Any:
        """Resolve an axis reference from name string.

        Tries: WorkAxes, then edges from SurfaceBodies.
        If already a COM object, return unchanged.
        """
        if isinstance(axis_ref, str):
            doc = self._ensure_active_document()
            if comp_def is None:
                comp_def = doc.ComponentDefinition
            try:
                # Try integer index as edge
                try:
                    index = int(axis_ref)
                    sb = comp_def.SurfaceBodies.Item(1)
                    return sb.Edges.Item(index)
                except ValueError:
                    pass
                # Try WorkAxes
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

        Inventor 2025+ uses ``AddFull`` (360° sweep) or ``AddByAngle``
        (partial sweep) instead of the old ``CreateRevolveDefinition``.

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
            "join", "cut", "intersect", or "new_body" (default: "join").

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

        # Resolve axis entity (sketch line in the same sketch as the profile)
        resolved_axis = self._resolve_axis(axis, comp_def)

        try:
            rf = comp_def.Features.RevolveFeatures

            if angle >= 360.0 - 0.001:
                rf.AddFull(resolved, resolved_axis, op_value)
            else:
                dir_value = _DIRECTION_MAP.get(direction.lower())
                if dir_value is None:
                    raise InventorCOMError(
                        f"Invalid direction '{direction}'."
                    )
                # AddByAngle angle is in radians
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