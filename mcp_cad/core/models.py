"""Provider-agnostic data models for CAD operations.

These are pure data classes with no Inventor or COM dependency.
They define the shape of geometric primitives, feature definitions,
and configuration objects used across all CAD backend implementations.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


# ---------------------------------------------------------------------------
# Geometric primitives
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class Point2D:
    """A 2D point with x and y coordinates."""

    x: float
    y: float

    def to_dict(self) -> dict[str, float]:
        return {"x": self.x, "y": self.y}


@dataclass(frozen=True)
class Point3D:
    """A 3D point with x, y, and z coordinates."""

    x: float
    y: float
    z: float

    def to_dict(self) -> dict[str, float]:
        return {"x": self.x, "y": self.y, "z": self.z}


@dataclass(frozen=True)
class Line:
    """A 2D line segment defined by two endpoints."""

    x1: float
    y1: float
    x2: float
    y2: float

    def to_dict(self) -> dict[str, float]:
        return {"x1": self.x1, "y1": self.y1, "x2": self.x2, "y2": self.y2}


@dataclass(frozen=True)
class Circle:
    """A circle defined by center and radius."""

    cx: float
    cy: float
    radius: float

    def to_dict(self) -> dict[str, float]:
        return {"cx": self.cx, "cy": self.cy, "radius": self.radius}


@dataclass(frozen=True)
class Arc:
    """An arc defined by center, radius, and angular range."""

    cx: float
    cy: float
    radius: float
    start_angle: float
    end_angle: float

    def to_dict(self) -> dict[str, float]:
        return {
            "cx": self.cx,
            "cy": self.cy,
            "radius": self.radius,
            "start_angle": self.start_angle,
            "end_angle": self.end_angle,
        }


@dataclass(frozen=True)
class Rectangle:
    """A rectangle defined by two corner points."""

    x1: float
    y1: float
    x2: float
    y2: float

    def to_dict(self) -> dict[str, float]:
        return {"x1": self.x1, "y1": self.y1, "x2": self.x2, "y2": self.y2}


# ---------------------------------------------------------------------------
# Work plane enum
# ---------------------------------------------------------------------------


class Plane(str, Enum):
    """Principal work planes in a CAD model.

    Inherits from ``str`` so that string comparisons like ``plane == "XY"``
    work naturally, while also supporting type-safe enum access like
    ``Plane.XY``.
    """

    XY = "XY"
    XZ = "XZ"
    YZ = "YZ"


# ---------------------------------------------------------------------------
# Feature definition models (with validation)
# ---------------------------------------------------------------------------

_VALID_DIRECTIONS = frozenset({"positive", "negative", "both"})
_VALID_OPERATIONS = frozenset({"new_body", "join", "cut", "intersect"})
_VALID_FILLET_MODES = frozenset({"constant"})
_VALID_CHAMFER_MODES = frozenset({"equal_distance"})


@dataclass(frozen=True)
class ExtrudeDef:
    """Definition of an extrude feature.

    Validates that direction and operation values are within the allowed set.
    """

    profile: str
    distance: float
    direction: str = "positive"
    taper: float = 0.0
    operation: str = "new_body"

    def __post_init__(self) -> None:
        if self.direction not in _VALID_DIRECTIONS:
            raise ValueError(
                f"Invalid direction '{self.direction}'. "
                f"Must be one of: {', '.join(sorted(_VALID_DIRECTIONS))}"
            )
        if self.operation not in _VALID_OPERATIONS:
            raise ValueError(
                f"Invalid operation '{self.operation}'. "
                f"Must be one of: {', '.join(sorted(_VALID_OPERATIONS))}"
            )


@dataclass(frozen=True)
class RevolveDef:
    """Definition of a revolve feature."""

    profile: str
    axis: str
    angle: float = 360.0
    operation: str = "join"

    def __post_init__(self) -> None:
        if self.operation not in _VALID_OPERATIONS:
            raise ValueError(
                f"Invalid operation '{self.operation}'. "
                f"Must be one of: {', '.join(sorted(_VALID_OPERATIONS))}"
            )


@dataclass(frozen=True)
class FilletDef:
    """Definition of a fillet feature."""

    edges: str
    radius: float
    mode: str = "constant"

    def __post_init__(self) -> None:
        if self.mode not in _VALID_FILLET_MODES:
            raise ValueError(
                f"Invalid fillet mode '{self.mode}'. "
                f"Must be one of: {', '.join(sorted(_VALID_FILLET_MODES))}"
            )


@dataclass(frozen=True)
class ChamferDef:
    """Definition of a chamfer feature."""

    edges: str
    distance: float
    mode: str = "equal_distance"

    def __post_init__(self) -> None:
        if self.mode not in _VALID_CHAMFER_MODES:
            raise ValueError(
                f"Invalid chamfer mode '{self.mode}'. "
                f"Must be one of: {', '.join(sorted(_VALID_CHAMFER_MODES))}"
            )


# ---------------------------------------------------------------------------
# Export configuration
# ---------------------------------------------------------------------------


class ExportOptions(dict):
    """Format-specific export options passed as a plain dictionary.

    This is a thin type alias over ``dict`` so that tool signatures can
    reference it by name while still accepting arbitrary key-value pairs
    (e.g. ``{"tolerance": 0.01}`` for STL, ``{"sheet": 1}`` for DXF).
    """

    pass