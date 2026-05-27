"""Generic CAD tool registration.

Provides ``register_tools(mcp_instance, provider)`` which registers all 32
MCP tool functions on a FastMCP instance.  Each tool receives a ``CADProvider``
instance and delegates to the appropriate protocol method with error handling.

This module has **zero** imports from ``mcp_cad.providers.*`` or
``mcp_cad.inventor.*`` — it depends only on the protocol and error types.
"""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.core.protocol import CADProvider
from mcp_cad.tools.connection import connect as tool_connect
from mcp_cad.tools.connection import disconnect as tool_disconnect
from mcp_cad.tools.connection import health as tool_health
from mcp_cad.tools.documents import (
    doc_close as tool_doc_close,
    doc_new_assembly as tool_doc_new_assembly,
    doc_new_part as tool_doc_new_part,
    doc_open as tool_doc_open,
    doc_save as tool_doc_save,
    doc_save_as as tool_doc_save_as,
)
from mcp_cad.tools.export import (
    export_dxf as tool_export_dxf,
    export_pdf as tool_export_pdf,
    export_step as tool_export_step,
    export_stl as tool_export_stl,
)
from mcp_cad.tools.features import (
    chamfer as tool_chamfer,
    extrude as tool_extrude,
    fillet as tool_fillet,
    revolve as tool_revolve,
)
from mcp_cad.tools.parameters import (
    param_get as tool_param_get,
    param_list as tool_param_list,
    param_set as tool_param_set,
    param_set_expression as tool_param_set_expression,
)
from mcp_cad.tools.properties import (
    iproperty_custom_get as tool_iproperty_custom_get,
    iproperty_custom_set as tool_iproperty_custom_set,
    iproperty_get as tool_iproperty_get,
    iproperty_set as tool_iproperty_set,
    iproperty_summary as tool_iproperty_summary,
)
from mcp_cad.tools.sketches import (
    sketch_arc as tool_sketch_arc,
    sketch_circle as tool_sketch_circle,
    sketch_create as tool_sketch_create,
    sketch_dimension as tool_sketch_dimension,
    sketch_ellipse as tool_sketch_ellipse,
    sketch_line as tool_sketch_line,
    sketch_point as tool_sketch_point,
    sketch_rectangle as tool_sketch_rectangle,
    sketch_spline as tool_sketch_spline,
)


def register_tools(mcp_instance: FastMCP, provider: CADProvider) -> None:
    """Register all MCP tools on the FastMCP instance.

    Each tool catches provider errors and converts them to the standard
    ``{success, error}`` envelope.  Tool names and parameter signatures
    match the original server.py exactly for full backward compatibility.

    Parameters
    ----------
    mcp_instance:
        The FastMCP server instance.
    provider:
        A CADProvider implementation (e.g. InventorProvider).
    """

    # ------------------------------------------------------------------
    # Connection tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def inventor_connect() -> dict[str, Any]:
        """Connect to a running Inventor instance or launch a new one."""
        return tool_connect(provider)

    @mcp_instance.tool()
    def inventor_health() -> dict[str, Any]:
        """Check Inventor connection health and document state."""
        return tool_health(provider)

    @mcp_instance.tool()
    def inventor_disconnect() -> dict[str, Any]:
        """Disconnect from Inventor without closing the application."""
        return tool_disconnect(provider)

    # ------------------------------------------------------------------
    # Document tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def doc_open(path: str) -> dict[str, Any]:
        """Open an existing Inventor document."""
        return tool_doc_open(provider, path)

    @mcp_instance.tool()
    def doc_new_part(template: str = "") -> dict[str, Any]:
        """Create a new part document."""
        return tool_doc_new_part(provider, template)

    @mcp_instance.tool()
    def doc_new_assembly(template: str = "") -> dict[str, Any]:
        """Create a new assembly document."""
        return tool_doc_new_assembly(provider, template)

    @mcp_instance.tool()
    def doc_save() -> dict[str, Any]:
        """Save the active document."""
        return tool_doc_save(provider)

    @mcp_instance.tool()
    def doc_save_as(path: str) -> dict[str, Any]:
        """Save the active document to a new path."""
        return tool_doc_save_as(provider, path)

    @mcp_instance.tool()
    def doc_close(save: bool = True) -> dict[str, Any]:
        """Close the active document."""
        return tool_doc_close(provider, save)

    # ------------------------------------------------------------------
    # Sketch tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def sketch_create(plane: str = "XY") -> dict[str, Any]:
        """Create a new sketch on the specified work plane."""
        return tool_sketch_create(provider, plane)

    @mcp_instance.tool()
    def sketch_line(x1: float, y1: float, x2: float, y2: float) -> dict[str, Any]:
        """Draw a line segment in the active sketch."""
        return tool_sketch_line(provider, x1, y1, x2, y2)

    @mcp_instance.tool()
    def sketch_circle(cx: float, cy: float, radius: float) -> dict[str, Any]:
        """Draw a circle in the active sketch."""
        return tool_sketch_circle(provider, cx, cy, radius)

    @mcp_instance.tool()
    def sketch_arc(
        cx: float,
        cy: float,
        radius: float,
        start_angle: float,
        end_angle: float,
    ) -> dict[str, Any]:
        """Draw an arc in the active sketch."""
        return tool_sketch_arc(provider, cx, cy, radius, start_angle, end_angle)

    @mcp_instance.tool()
    def sketch_rectangle(
        x1: float, y1: float, x2: float, y2: float
    ) -> dict[str, Any]:
        """Draw a rectangle in the active sketch."""
        return tool_sketch_rectangle(provider, x1, y1, x2, y2)

    @mcp_instance.tool()
    def sketch_dimension(
        entity: str,
        value: float,
        position_x: float | None = None,
        position_y: float | None = None,
    ) -> dict[str, Any]:
        """Add a dimension constraint to the active sketch."""
        return tool_sketch_dimension(provider, entity, value, position_x, position_y)

    @mcp_instance.tool()
    def sketch_point(
        x: float,
        y: float,
    ) -> dict[str, Any]:
        """Draw a point in the active sketch."""
        return tool_sketch_point(provider, x, y)

    @mcp_instance.tool()
    def sketch_spline(
        points: str,
        fit_method: str = "sweet",
    ) -> dict[str, Any]:
        """Draw a spline through fit points.

        Args:
            points: Comma-separated coordinates: "x1,y1,x2,y2,..."
            fit_method: "sweet" (default), "smooth", or "autocad".
        """
        coords = [float(v) for v in points.split(",")]
        pts = [(coords[i], coords[i + 1]) for i in range(0, len(coords), 2)]
        return tool_sketch_spline(provider, pts, fit_method)

    @mcp_instance.tool()
    def sketch_ellipse(
        cx: float,
        cy: float,
        major_radius: float,
        minor_radius: float,
        major_axis_angle: float = 0.0,
    ) -> dict[str, Any]:
        """Draw an ellipse in the active sketch."""
        return tool_sketch_ellipse(provider, cx, cy, major_radius, minor_radius, major_axis_angle)

    # ------------------------------------------------------------------
    # Feature tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def extrude(
        profile: str,
        distance: float,
        direction: str = "positive",
        taper: float = 0.0,
        operation: str = "new_body",
    ) -> dict[str, Any]:
        """Extrude a sketch profile to create a 3D feature."""
        return tool_extrude(provider, profile, distance, direction, taper, operation)

    @mcp_instance.tool()
    def revolve(
        profile: str,
        axis: str,
        angle: float = 360.0,
        operation: str = "join",
    ) -> dict[str, Any]:
        """Revolve a profile around an axis to create a 3D feature."""
        return tool_revolve(provider, profile, axis, angle, operation)

    @mcp_instance.tool()
    def fillet(
        edges: str,
        radius: float,
        mode: str = "constant",
    ) -> dict[str, Any]:
        """Apply a fillet to the specified edges."""
        return tool_fillet(provider, edges, radius, mode)

    @mcp_instance.tool()
    def chamfer(
        edges: str,
        distance: float,
        mode: str = "equal_distance",
    ) -> dict[str, Any]:
        """Apply a chamfer to the specified edges."""
        return tool_chamfer(provider, edges, distance, mode)

    # ------------------------------------------------------------------
    # Parameter tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def param_list(filter_pattern: str | None = None) -> dict[str, Any]:
        """List model parameters, optionally filtered by name pattern."""
        return tool_param_list(provider, filter_pattern)

    @mcp_instance.tool()
    def param_get(name: str) -> dict[str, Any]:
        """Get a specific model parameter by name."""
        return tool_param_get(provider, name)

    @mcp_instance.tool()
    def param_set(name: str, value: float) -> dict[str, Any]:
        """Set a model parameter value by name."""
        return tool_param_set(provider, name, value)

    @mcp_instance.tool()
    def param_set_expression(name: str, expression: str) -> dict[str, Any]:
        """Set a model parameter using an expression (e.g. 'd0 * 2')."""
        return tool_param_set_expression(provider, name, expression)

    # ------------------------------------------------------------------
    # iProperty tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def iproperty_get(
        name: str, property_set: str = "Summary"
    ) -> dict[str, Any]:
        """Get an iProperty value by name."""
        return tool_iproperty_get(provider, name, property_set)

    @mcp_instance.tool()
    def iproperty_set(
        name: str, value: Any, property_set: str = "Summary"
    ) -> dict[str, Any]:
        """Set an iProperty value by name."""
        return tool_iproperty_set(provider, name, value, property_set)

    @mcp_instance.tool()
    def iproperty_summary() -> dict[str, Any]:
        """Get all Summary iProperties."""
        return tool_iproperty_summary(provider)

    @mcp_instance.tool()
    def iproperty_custom_get(name: str) -> dict[str, Any]:
        """Get a custom iProperty by name."""
        return tool_iproperty_custom_get(provider, name)

    @mcp_instance.tool()
    def iproperty_custom_set(name: str, value: Any) -> dict[str, Any]:
        """Set a custom iProperty. Creates it if it doesn't exist."""
        return tool_iproperty_custom_set(provider, name, value)

    # ------------------------------------------------------------------
    # Export tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def export_step(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to STEP format."""
        return tool_export_step(provider, path, options)

    @mcp_instance.tool()
    def export_stl(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to STL format."""
        return tool_export_stl(provider, path, options)

    @mcp_instance.tool()
    def export_pdf(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to PDF format."""
        return tool_export_pdf(provider, path, options)

    @mcp_instance.tool()
    def export_dxf(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document's sketch or flat pattern to DXF."""
        return tool_export_dxf(provider, path, options)