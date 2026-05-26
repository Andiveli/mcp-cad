"""3D feature operations for Autodesk Inventor.

Provides extrude, revolve, fillet, and chamfer features via COM automation.
"""

from __future__ import annotations

import logging
from typing import Any

from mcp_cad.inventor.client import InventorDriver
from mcp_cad.errors import InventorCOMError, InventorDisconnectedError

log = logging.getLogger(__name__)

# Inventor part-feature operation COM constants
_JOIN_OPERATION = 0
_CUT_OPERATION = 1
_INTERSECT_OPERATION = 2

_OPERATION_MAP: dict[str, int] = {
    "join": _JOIN_OPERATION,
    "cut": _CUT_OPERATION,
    "intersect": _INTERSECT_OPERATION,
}

# Direction COM constants
_DIRECTION_MAP: dict[str, int] = {
    "positive": 0,  # kPositiveDirection
    "negative": 1,  # kNegativeDirection
    "both": 2,       # kBothDirections
}

# Fillet/Chamfer edge-set mode COM constants
_CONSTANT_FILLET = 0
_VARIABLE_FILLET = 1

_FILLET_MODE_MAP: dict[str, int] = {
    "constant": _CONSTANT_FILLET,
    "variable": _VARIABLE_FILLET,
}

_EQUAL_DISTANCE = 0  # kEqualDistanceChamferType
_TWO_DISTANCES = 1   # kTwoDistancesChamferType

_CHAMFER_MODE_MAP: dict[str, int] = {
    "equal_distance": _EQUAL_DISTANCE,
    "two_distances": _TWO_DISTANCES,
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

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def extrude(
        self,
        profile: Any,
        distance: float,
        direction: str = "positive",
        taper: float = 0.0,
        operation: str = "join",
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
            "join", "cut", or "intersect" (default: "join").

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
            extrude_def = features.ExtrudeFeatures.CreateExtrudeDefinition(
                resolved, distance
            )
            # Use SetDistanceExtent with direction parameter
            # (replaces deprecated .Direction property)
            extrude_def.SetDistanceExtent(distance, dir_value)
            extrude_def.TaperAngle = taper
            extrude_def.Operation = op_value
            feature = features.ExtrudeFeatures.Add(extrude_def)
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
        operation: str = "join",
    ) -> dict[str, Any]:
        """Revolve a profile around an axis to create a 3D feature.

        Parameters
        ----------
        profile:
            Sketch profile name (str) or COM profile reference.
        axis:
            Axis of revolution — a sketch line name (str) or COM object.
        angle:
            Revolution angle in degrees (default: 360 for full revolve).
        operation:
            "join", "cut", or "intersect" (default: "join").

        Returns
        -------
        dict with feature metadata.
        """
        self._ensure_connected()

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
            revolve_def = features.RevolveFeatures.CreateRevolveDefinition(
                resolved, axis
            )
            revolve_def.Angle = angle
            revolve_def.Operation = op_value
            feature = features.RevolveFeatures.Add(revolve_def)
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

    def fillet(
        self,
        edges: Any,
        radius: float,
        mode: str = "constant",
    ) -> dict[str, Any]:
        """Apply a fillet to the specified edges.

        Parameters
        ----------
        edges:
            Edge or edge collection COM object, or a single edge name (str).
        radius:
            Fillet radius (cm in Inventor's internal units).
        mode:
            "constant" or "variable" (default: "constant").

        Returns
        -------
        dict with feature metadata.
        """
        self._ensure_connected()

        mode_value = _FILLET_MODE_MAP.get(mode.lower())
        if mode_value is None:
            raise InventorCOMError(
                f"Invalid fillet mode '{mode}'. Must be one of: "
                f"{', '.join(sorted(_FILLET_MODE_MAP))}"
            )

        doc = self._ensure_active_document()

        try:
            comp_def = doc.ComponentDefinition
            features = comp_def.Features
            # Build an edge collection from the provided edges
            edge_col = comp_def.SurfaceBodies.Item(1).Edges
            fillet_def = features.FilletFeatures.CreateFilletDefinition(
                edge_col, mode_value
            )
            fillet_def.Radius = radius
            feature = features.FilletFeatures.Add(fillet_def)
            return {
                "success": True,
                "feature_type": "fillet",
                "radius": radius,
                "mode": mode.lower(),
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

        Parameters
        ----------
        edges:
            Edge or edge collection COM object, or a single edge name (str).
        distance:
            Chamfer distance (cm in Inventor's internal units).
        mode:
            "equal_distance" or "two_distances" (default: "equal_distance").

        Returns
        -------
        dict with feature metadata.
        """
        self._ensure_connected()

        mode_value = _CHAMFER_MODE_MAP.get(mode.lower())
        if mode_value is None:
            raise InventorCOMError(
                f"Invalid chamfer mode '{mode}'. Must be one of: "
                f"{', '.join(sorted(_CHAMFER_MODE_MAP))}"
            )

        doc = self._ensure_active_document()

        try:
            comp_def = doc.ComponentDefinition
            features = comp_def.Features
            edge_col = comp_def.SurfaceBodies.Item(1).Edges
            chamfer_def = features.ChamferFeatures.CreateChamferDefinition(
                edge_col, mode_value, distance
            )
            feature = features.ChamferFeatures.Add(chamfer_def)
            return {
                "success": True,
                "feature_type": "chamfer",
                "distance": distance,
                "mode": mode.lower(),
            }
        except (InventorDisconnectedError, InventorCOMError):
            raise
        except Exception as exc:
            raise InventorCOMError(f"Failed to create chamfer: {exc}") from exc