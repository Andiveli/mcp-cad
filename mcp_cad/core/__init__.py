"""Core protocol and data models for the CAD provider abstraction.

This package defines the backend-agnostic interface (``CADProvider`` protocol)
and the shared data models (``Point2D``, ``Plane``, ``ExtrudeDef``, etc.)
that every CAD backend must satisfy.

The ``core`` package has **zero** dependencies on Inventor COM — it imports
only from the Python standard library and ``typing``.
"""

from mcp_cad.core.models import (
    Arc,
    ChamferDef,
    Circle,
    ExportOptions,
    ExtrudeDef,
    FilletDef,
    Line,
    Plane,
    Point2D,
    Point3D,
    Rectangle,
    RevolveDef,
)
from mcp_cad.core.protocol import CADProvider

__all__ = [
    # Protocol
    "CADProvider",
    # Geometric primitives
    "Point2D",
    "Point3D",
    "Line",
    "Circle",
    "Arc",
    "Rectangle",
    # Enums
    "Plane",
    # Feature definitions
    "ExtrudeDef",
    "RevolveDef",
    "FilletDef",
    "ChamferDef",
    # Export
    "ExportOptions",
]