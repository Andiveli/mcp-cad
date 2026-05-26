"""FastMCP server instance and tool registration for Inventor MCP."""

from __future__ import annotations

import logging
from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.errors import InventorCOMError, InventorDisconnectedError
from mcp_cad.inventor.client import RealInventorDriver
from mcp_cad.inventor.document import DocumentManager
from mcp_cad.inventor.export import ExportManager
from mcp_cad.inventor.feature import FeatureManager
from mcp_cad.inventor.parameter import ParameterManager
from mcp_cad.inventor.property import PropertyManager
from mcp_cad.inventor.sketch import SketchManager

log = logging.getLogger(__name__)


def _ok(data: Any = None) -> dict[str, Any]:
    """Build a standard success response."""
    result: dict[str, Any] = {"success": True}
    if data is not None:
        result["data"] = data
    return result


def _err(exc: Exception) -> dict[str, Any]:
    """Build a standard error response from an exception."""
    return {"success": False, "error": str(exc)}


def register_tools(
    mcp_instance: FastMCP,
    driver: RealInventorDriver,
    doc_mgr: DocumentManager,
    sketch_mgr: SketchManager,
    feature_mgr: FeatureManager,
    param_mgr: ParameterManager,
    prop_mgr: PropertyManager,
    export_mgr: ExportManager,
) -> None:
    """Register all MCP tools on the FastMCP instance.

    Each tool catches ``InventorDisconnectedError`` and ``InventorCOMError``
    and converts them to the standard ``{success, data, error}`` envelope.
    """

    # ------------------------------------------------------------------
    # Connection tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def inventor_connect() -> dict[str, Any]:
        """Connect to a running Inventor instance or launch a new one."""
        try:
            return driver.connect()
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def inventor_health() -> dict[str, Any]:
        """Check Inventor connection health and document state."""
        try:
            return driver.health()
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def inventor_disconnect() -> dict[str, Any]:
        """Disconnect from Inventor without closing the application."""
        try:
            return driver.disconnect()
        except Exception as exc:
            return _err(exc)

    # ------------------------------------------------------------------
    # Document tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def doc_open(path: str) -> dict[str, Any]:
        """Open an existing Inventor document."""
        try:
            return doc_mgr.doc_open(path)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def doc_new_part(template: str = "") -> dict[str, Any]:
        """Create a new part document."""
        try:
            return doc_mgr.doc_new_part(template)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def doc_new_assembly(template: str = "") -> dict[str, Any]:
        """Create a new assembly document."""
        try:
            return doc_mgr.doc_new_assembly(template)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def doc_save() -> dict[str, Any]:
        """Save the active document."""
        try:
            return doc_mgr.doc_save()
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def doc_save_as(path: str) -> dict[str, Any]:
        """Save the active document to a new path."""
        try:
            return doc_mgr.doc_save_as(path)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def doc_close(save: bool = True) -> dict[str, Any]:
        """Close the active document."""
        try:
            return doc_mgr.doc_close(save)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    # ------------------------------------------------------------------
    # Sketch tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def sketch_create(plane: str = "XY") -> dict[str, Any]:
        """Create a new sketch on the specified work plane."""
        try:
            return sketch_mgr.sketch_create(plane)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def sketch_line(x1: float, y1: float, x2: float, y2: float) -> dict[str, Any]:
        """Draw a line segment in the active sketch."""
        try:
            return sketch_mgr.sketch_line(x1, y1, x2, y2)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def sketch_circle(cx: float, cy: float, radius: float) -> dict[str, Any]:
        """Draw a circle in the active sketch."""
        try:
            return sketch_mgr.sketch_circle(cx, cy, radius)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def sketch_arc(
        cx: float,
        cy: float,
        radius: float,
        start_angle: float,
        end_angle: float,
    ) -> dict[str, Any]:
        """Draw an arc in the active sketch."""
        try:
            return sketch_mgr.sketch_arc(cx, cy, radius, start_angle, end_angle)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def sketch_rectangle(
        x1: float, y1: float, x2: float, y2: float
    ) -> dict[str, Any]:
        """Draw a rectangle in the active sketch."""
        try:
            return sketch_mgr.sketch_rectangle(x1, y1, x2, y2)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def sketch_dimension(
        entity: str,
        value: float,
        position_x: float | None = None,
        position_y: float | None = None,
    ) -> dict[str, Any]:
        """Add a dimension constraint to the active sketch."""
        try:
            pos = None
            if position_x is not None and position_y is not None:
                pos = (position_x, position_y)
            return sketch_mgr.sketch_dimension(entity, value, pos)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

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
        try:
            return feature_mgr.extrude(
                profile, distance, direction, taper, operation
            )
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def revolve(
        profile: str,
        axis: str,
        angle: float = 360.0,
        operation: str = "join",
    ) -> dict[str, Any]:
        """Revolve a profile around an axis to create a 3D feature."""
        try:
            return feature_mgr.revolve(profile, axis, angle, operation)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def fillet(
        edges: str,
        radius: float,
        mode: str = "constant",
    ) -> dict[str, Any]:
        """Apply a fillet to the specified edges."""
        try:
            return feature_mgr.fillet(edges, radius, mode)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def chamfer(
        edges: str,
        distance: float,
        mode: str = "equal_distance",
    ) -> dict[str, Any]:
        """Apply a chamfer to the specified edges."""
        try:
            return feature_mgr.chamfer(edges, distance, mode)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    # ------------------------------------------------------------------
    # Parameter tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def param_list(filter_pattern: str | None = None) -> dict[str, Any]:
        """List model parameters, optionally filtered by name pattern."""
        try:
            return param_mgr.param_list(filter_pattern)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def param_get(name: str) -> dict[str, Any]:
        """Get a specific model parameter by name."""
        try:
            return param_mgr.param_get(name)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def param_set(name: str, value: float) -> dict[str, Any]:
        """Set a model parameter value by name."""
        try:
            return param_mgr.param_set(name, value)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def param_set_expression(name: str, expression: str) -> dict[str, Any]:
        """Set a model parameter using an expression (e.g. 'd0 * 2')."""
        try:
            return param_mgr.param_set_expression(name, expression)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    # ------------------------------------------------------------------
    # iProperty tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def iproperty_get(
        name: str, property_set: str = "Summary"
    ) -> dict[str, Any]:
        """Get an iProperty value by name."""
        try:
            return prop_mgr.iproperty_get(name, property_set)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def iproperty_set(
        name: str, value: Any, property_set: str = "Summary"
    ) -> dict[str, Any]:
        """Set an iProperty value by name."""
        try:
            return prop_mgr.iproperty_set(name, value, property_set)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def iproperty_summary() -> dict[str, Any]:
        """Get all Summary iProperties."""
        try:
            return prop_mgr.iproperty_summary()
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def iproperty_custom_get(name: str) -> dict[str, Any]:
        """Get a custom iProperty by name."""
        try:
            return prop_mgr.iproperty_custom_get(name)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def iproperty_custom_set(name: str, value: Any) -> dict[str, Any]:
        """Set a custom iProperty. Creates it if it doesn't exist."""
        try:
            return prop_mgr.iproperty_custom_set(name, value)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    # ------------------------------------------------------------------
    # Export tools
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def export_step(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to STEP format."""
        try:
            return export_mgr.export_step(path, options)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def export_stl(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to STL format."""
        try:
            return export_mgr.export_stl(path, options)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def export_pdf(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document to PDF format."""
        try:
            return export_mgr.export_pdf(path, options)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)

    @mcp_instance.tool()
    def export_dxf(
        path: str, options: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        """Export the active document's sketch or flat pattern to DXF."""
        try:
            return export_mgr.export_dxf(path, options)
        except (InventorDisconnectedError, InventorCOMError) as exc:
            return _err(exc)
        except Exception as exc:
            return _err(exc)


def main() -> None:
    """Entry point: create driver, managers, register tools, run server."""
    mcp = FastMCP("mcp-cad")

    # Share a single driver + COM reference across all managers
    driver = RealInventorDriver()

    # Share a single driver across all managers.  Managers access
    # driver.inventor dynamically, so they always see the live COM
    # reference — even if connect() hasn't been called yet.
    doc_mgr = DocumentManager(driver)
    sketch_mgr = SketchManager(driver)
    feature_mgr = FeatureManager(driver)
    param_mgr = ParameterManager(driver)
    prop_mgr = PropertyManager(driver)
    export_mgr = ExportManager(driver)

    register_tools(
        mcp_instance=mcp,
        driver=driver,
        doc_mgr=doc_mgr,
        sketch_mgr=sketch_mgr,
        feature_mgr=feature_mgr,
        param_mgr=param_mgr,
        prop_mgr=prop_mgr,
        export_mgr=export_mgr,
    )

    mcp.run(transport="stdio")