"""Abstract protocol defining the CAD provider interface.

The ``CADProvider`` protocol decouples the MCP tool layer from any specific
CAD backend (Inventor, FreeCAD, Onshape, etc.).  Every backend that wants
to serve tools must implement this protocol's methods with matching
signatures.

Signatures are derived from the current ``server.py`` tool definitions so
that existing tool registration code transfers verbatim.
"""

from __future__ import annotations

from typing import Any, Protocol, runtime_checkable


@runtime_checkable
class CADProvider(Protocol):
    """Structural protocol for a CAD backend.

    Implementations receive no base class — they only need to satisfy
    this protocol's method signatures.  Because it is
    ``@runtime_checkable``, ``isinstance(obj, CADProvider)`` works for
    duck-typed objects.
    """

    # ------------------------------------------------------------------
    # Connection lifecycle
    # ------------------------------------------------------------------

    def connect(self) -> dict[str, Any]:
        """Connect to the CAD application.

        Returns:
            Dict with connection status metadata.
        """
        ...

    def disconnect(self) -> dict[str, Any]:
        """Release the connection to the CAD application.

        Returns:
            Dict with disconnection status.
        """
        ...

    def health(self) -> dict[str, Any]:
        """Check connection health and document state.

        Returns:
            Dict with connection and document status.
        """
        ...

    # ------------------------------------------------------------------
    # Document management
    # ------------------------------------------------------------------

    def doc_open(self, path: str) -> dict[str, Any]:
        """Open an existing document."""
        ...

    def doc_new_part(self, template: str = "") -> dict[str, Any]:
        """Create a new part document."""
        ...

    def doc_new_assembly(self, template: str = "") -> dict[str, Any]:
        """Create a new assembly document."""
        ...

    def doc_save(self) -> dict[str, Any]:
        """Save the active document."""
        ...

    def doc_save_as(self, path: str) -> dict[str, Any]:
        """Save the active document to a new path."""
        ...

    def doc_close(self, save: bool = True) -> dict[str, Any]:
        """Close the active document."""
        ...

    # ------------------------------------------------------------------
    # Sketch operations
    # ------------------------------------------------------------------

    def sketch_create(self, plane: str = "XY") -> dict[str, Any]:
        """Create a new sketch on the specified work plane."""
        ...

    def sketch_line(
        self,
        x1: float,
        y1: float,
        x2: float,
        y2: float,
    ) -> dict[str, Any]:
        """Draw a line segment in the active sketch."""
        ...

    def sketch_circle(
        self,
        cx: float,
        cy: float,
        radius: float,
    ) -> dict[str, Any]:
        """Draw a circle in the active sketch."""
        ...

    def sketch_arc(
        self,
        cx: float,
        cy: float,
        radius: float,
        start_angle: float,
        end_angle: float,
    ) -> dict[str, Any]:
        """Draw an arc in the active sketch."""
        ...

    def sketch_rectangle(
        self,
        x1: float,
        y1: float,
        x2: float,
        y2: float,
    ) -> dict[str, Any]:
        """Draw a rectangle in the active sketch."""
        ...

    def sketch_dimension(
        self,
        entity: str,
        value: float,
        position_x: float | None = None,
        position_y: float | None = None,
    ) -> dict[str, Any]:
        """Add a dimension constraint to the active sketch."""
        ...

    def sketch_point(
        self,
        x: float,
        y: float,
    ) -> dict[str, Any]:
        """Draw a point in the active sketch."""
        ...

    def sketch_spline(
        self,
        points: list[tuple[float, float]],
        fit_method: str = "sweet",
    ) -> dict[str, Any]:
        """Draw a spline through fit points.

        Args:
            points: List of (x, y) tuples defining the fit points.
            fit_method: ``"smooth"``, ``"sweet"``, or ``"autocad"``.
        """
        ...

    def sketch_ellipse(
        self,
        cx: float,
        cy: float,
        major_radius: float,
        minor_radius: float,
        major_axis_angle: float = 0.0,
    ) -> dict[str, Any]:
        """Draw an ellipse in the active sketch."""
        ...

    def sketch_circular_pattern(
        self,
        entities: str,
        axis: str,
        count: int,
        angle: float = 360.0,
        fitted: bool = True,
        symmetric: bool = False,
    ) -> dict[str, Any]:
        """Create a circular pattern of sketch entities."""
        ...

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
        """Create a rectangular pattern of sketch entities."""
        ...

    # ------------------------------------------------------------------
    # 3D feature operations
    # ------------------------------------------------------------------

    def extrude(
        self,
        profile: str,
        distance: float,
        direction: str = "positive",
        taper: float = 0.0,
        operation: str = "new_body",
    ) -> dict[str, Any]:
        """Extrude a sketch profile to create a 3D feature."""
        ...

    def revolve(
        self,
        profile: str,
        axis: str,
        angle: float = 360.0,
        operation: str = "join",
    ) -> dict[str, Any]:
        """Revolve a profile around an axis to create a 3D feature."""
        ...

    def fillet(
        self,
        edges: str,
        radius: float,
        mode: str = "constant",
    ) -> dict[str, Any]:
        """Apply a fillet to the specified edges."""
        ...

    def chamfer(
        self,
        edges: str,
        distance: float,
        mode: str = "equal_distance",
    ) -> dict[str, Any]:
        """Apply a chamfer to the specified edges."""
        ...

    def circular_pattern(
        self,
        profile: str,
        axis: str,
        count: int,
        angle: float = 360.0,
        fit_within_angle: bool = True,
        natural_direction: bool = True,
    ) -> dict[str, Any]:
        """Create a circular pattern of a feature around an axis."""
        ...

    # ------------------------------------------------------------------
    # Parameter management
    # ------------------------------------------------------------------

    def param_list(self, filter_pattern: str | None = None) -> dict[str, Any]:
        """List model parameters, optionally filtered by name pattern."""
        ...

    def param_get(self, name: str) -> dict[str, Any]:
        """Get a specific model parameter by name."""
        ...

    def param_set(self, name: str, value: float) -> dict[str, Any]:
        """Set a model parameter value by name."""
        ...

    def param_set_expression(self, name: str, expression: str) -> dict[str, Any]:
        """Set a model parameter using an expression (e.g. 'd0 * 2')."""
        ...

    # ------------------------------------------------------------------
    # iProperty management
    # ------------------------------------------------------------------

    def iproperty_get(
        self,
        name: str,
        property_set: str = "Summary",
    ) -> dict[str, Any]:
        """Get an iProperty value by name."""
        ...

    def iproperty_set(
        self,
        name: str,
        value: Any,
        property_set: str = "Summary",
    ) -> dict[str, Any]:
        """Set an iProperty value by name."""
        ...

    def iproperty_summary(self) -> dict[str, Any]:
        """Get all Summary iProperties."""
        ...

    def iproperty_custom_get(self, name: str) -> dict[str, Any]:
        """Get a custom iProperty by name."""
        ...

    def iproperty_custom_set(self, name: str, value: Any) -> dict[str, Any]:
        """Set a custom iProperty. Creates it if it doesn't exist."""
        ...

    # ------------------------------------------------------------------
    # Export operations
    # ------------------------------------------------------------------

    def export_step(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document to STEP format."""
        ...

    def export_stl(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document to STL format."""
        ...

    def export_pdf(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document to PDF format."""
        ...

    def export_dxf(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document's sketch or flat pattern to DXF."""
        ...