"""Protocol adapter — wraps Inventor managers behind the CADProvider interface.

The ``InventorProvider`` class is the single entry point for all Inventor
operations.  It implements the ``CADProvider`` protocol defined in
``mcp_cad.core.protocol`` by delegating each method call to the
appropriate manager instance.

No business logic lives here — this is a pure delegation layer that
maps protocol method signatures to manager method calls 1:1.
"""

from __future__ import annotations

from typing import Any

from mcp_cad.core.protocol import CADProvider
from mcp_cad.providers.inventor.client import RealInventorDriver
from mcp_cad.providers.inventor.document import DocumentManager
from mcp_cad.providers.inventor.export import ExportManager
from mcp_cad.providers.inventor.feature import FeatureManager
from mcp_cad.providers.inventor.parameter import ParameterManager
from mcp_cad.providers.inventor.property import PropertyManager
from mcp_cad.providers.inventor.sketch import SketchManager


class InventorProvider:
    """Implements ``CADProvider`` for Autodesk Inventor via COM.

    Accepts a ``RealInventorDriver`` and constructs all manager instances
    internally.  Each protocol method delegates to the corresponding manager
    with no additional logic.
    """

    def __init__(self, driver: RealInventorDriver) -> None:
        self._driver = driver
        self._doc = DocumentManager(driver)
        self._sketch = SketchManager(driver)
        self._feature = FeatureManager(driver)
        self._param = ParameterManager(driver)
        self._prop = PropertyManager(driver)
        self._export = ExportManager(driver)

    # ------------------------------------------------------------------
    # Connection lifecycle
    # ------------------------------------------------------------------

    def connect(self) -> dict[str, Any]:
        """Connect to Inventor via COM dispatch."""
        return self._driver.connect()

    def disconnect(self) -> dict[str, Any]:
        """Release the COM reference and uninitialize COM."""
        return self._driver.disconnect()

    def health(self) -> dict[str, Any]:
        """Check connection health and document state."""
        return self._driver.health()

    # ------------------------------------------------------------------
    # Document management
    # ------------------------------------------------------------------

    def doc_open(self, path: str) -> dict[str, Any]:
        """Open an existing Inventor document."""
        return self._doc.doc_open(path)

    def doc_new_part(self, template: str = "") -> dict[str, Any]:
        """Create a new part document."""
        return self._doc.doc_new_part(template)

    def doc_new_assembly(self, template: str = "") -> dict[str, Any]:
        """Create a new assembly document."""
        return self._doc.doc_new_assembly(template)

    def doc_save(self) -> dict[str, Any]:
        """Save the active document."""
        return self._doc.doc_save()

    def doc_save_as(self, path: str) -> dict[str, Any]:
        """Save the active document to a new path."""
        return self._doc.doc_save_as(path)

    def doc_close(self, save: bool = True) -> dict[str, Any]:
        """Close the active document."""
        return self._doc.doc_close(save)

    # ------------------------------------------------------------------
    # Sketch operations
    # ------------------------------------------------------------------

    def sketch_create(self, plane: str = "XY") -> dict[str, Any]:
        """Create a new sketch on the specified work plane."""
        return self._sketch.sketch_create(plane)

    def sketch_line(
        self,
        x1: float,
        y1: float,
        x2: float,
        y2: float,
    ) -> dict[str, Any]:
        """Draw a line segment in the active sketch."""
        return self._sketch.sketch_line(x1, y1, x2, y2)

    def sketch_circle(
        self,
        cx: float,
        cy: float,
        radius: float,
    ) -> dict[str, Any]:
        """Draw a circle in the active sketch."""
        return self._sketch.sketch_circle(cx, cy, radius)

    def sketch_arc(
        self,
        cx: float,
        cy: float,
        radius: float,
        start_angle: float,
        end_angle: float,
    ) -> dict[str, Any]:
        """Draw an arc in the active sketch."""
        return self._sketch.sketch_arc(cx, cy, radius, start_angle, end_angle)

    def sketch_rectangle(
        self,
        x1: float,
        y1: float,
        x2: float,
        y2: float,
    ) -> dict[str, Any]:
        """Draw a rectangle in the active sketch."""
        return self._sketch.sketch_rectangle(x1, y1, x2, y2)

    def sketch_dimension(
        self,
        entity: str,
        value: float,
        position_x: float | None = None,
        position_y: float | None = None,
    ) -> dict[str, Any]:
        """Add a dimension constraint to the active sketch.

        Converts ``position_x`` / ``position_y`` into a ``(x, y)`` tuple
        for ``SketchManager.sketch_dimension()``, or ``None`` if both are
        omitted.
        """
        if position_x is not None and position_y is not None:
            position: tuple[float, float] | None = (position_x, position_y)
        else:
            position = None
        return self._sketch.sketch_dimension(entity, value, position)

    def sketch_point(self, x: float, y: float) -> dict[str, Any]:
        """Draw a point in the active sketch."""
        return self._sketch.sketch_point(x, y)

    def sketch_spline(
        self,
        points: list[tuple[float, float]],
        fit_method: str = "sweet",
    ) -> dict[str, Any]:
        """Draw a spline through fit points."""
        return self._sketch.sketch_spline(points, fit_method)

    def sketch_ellipse(
        self,
        cx: float,
        cy: float,
        major_radius: float,
        minor_radius: float,
        major_axis_angle: float = 0.0,
    ) -> dict[str, Any]:
        """Draw an ellipse."""
        return self._sketch.sketch_ellipse(cx, cy, major_radius, minor_radius, major_axis_angle)

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
        return self._sketch.sketch_circular_pattern(
            entities, axis, count, angle, fitted, symmetric,
        )

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
        return self._feature.extrude(profile, distance, direction, taper, operation)

    def revolve(
        self,
        profile: str,
        axis: str,
        angle: float = 360.0,
        operation: str = "join",
    ) -> dict[str, Any]:
        """Revolve a profile around an axis to create a 3D feature."""
        return self._feature.revolve(profile, axis, angle, operation)

    def fillet(
        self,
        edges: str,
        radius: float,
        mode: str = "constant",
    ) -> dict[str, Any]:
        """Apply a fillet to the specified edges."""
        return self._feature.fillet(edges, radius, mode)

    def chamfer(
        self,
        edges: str,
        distance: float,
        mode: str = "equal_distance",
    ) -> dict[str, Any]:
        """Apply a chamfer to the specified edges."""
        return self._feature.chamfer(edges, distance, mode)

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
        return self._feature.circular_pattern(
            profile, axis, count, angle, fit_within_angle, natural_direction
        )

    # ------------------------------------------------------------------
    # Parameter management
    # ------------------------------------------------------------------

    def param_list(self, filter_pattern: str | None = None) -> dict[str, Any]:
        """List model parameters, optionally filtered by name pattern."""
        return self._param.param_list(filter_pattern)

    def param_get(self, name: str) -> dict[str, Any]:
        """Get a specific model parameter by name."""
        return self._param.param_get(name)

    def param_set(self, name: str, value: float) -> dict[str, Any]:
        """Set a model parameter value by name."""
        return self._param.param_set(name, value)

    def param_set_expression(self, name: str, expression: str) -> dict[str, Any]:
        """Set a model parameter using an expression (e.g. 'd0 * 2')."""
        return self._param.param_set_expression(name, expression)

    # ------------------------------------------------------------------
    # iProperty management
    # ------------------------------------------------------------------

    def iproperty_get(
        self,
        name: str,
        property_set: str = "Summary",
    ) -> dict[str, Any]:
        """Get an iProperty value by name."""
        return self._prop.iproperty_get(name, property_set)

    def iproperty_set(
        self,
        name: str,
        value: Any,
        property_set: str = "Summary",
    ) -> dict[str, Any]:
        """Set an iProperty value by name."""
        return self._prop.iproperty_set(name, value, property_set)

    def iproperty_summary(self) -> dict[str, Any]:
        """Get all Summary iProperties."""
        return self._prop.iproperty_summary()

    def iproperty_custom_get(self, name: str) -> dict[str, Any]:
        """Get a custom iProperty by name."""
        return self._prop.iproperty_custom_get(name)

    def iproperty_custom_set(self, name: str, value: Any) -> dict[str, Any]:
        """Set a custom iProperty. Creates it if it doesn't exist."""
        return self._prop.iproperty_custom_set(name, value)

    # ------------------------------------------------------------------
    # Export operations
    # ------------------------------------------------------------------

    def export_step(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document to STEP format."""
        return self._export.export_step(path, options)

    def export_stl(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document to STL format."""
        return self._export.export_stl(path, options)

    def export_pdf(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document to PDF format."""
        return self._export.export_pdf(path, options)

    def export_dxf(
        self,
        path: str,
        options: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Export the active document's sketch or flat pattern to DXF."""
        return self._export.export_dxf(path, options)


def create_inventor_provider() -> InventorProvider:
    """Factory function — create a provider backed by a new RealInventorDriver.

    This is the intended entry point for ``server.py`` and any code that
    needs a ready-to-use ``CADProvider`` instance.
    """
    driver = RealInventorDriver()
    return InventorProvider(driver)