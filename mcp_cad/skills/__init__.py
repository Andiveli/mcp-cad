"""Skills package — composable CAD operations built on the CADProvider protocol.

Provides ``register_skills(mcp_instance, provider)`` which registers all
skill-based MCP tools.  Skills compose atomic provider operations into
higher-level workflows, organized by Inventor tabs and panels:

    Tab: Sketch → Panel: Draw
        skill_sketch   — create / activate sketch
        skill_line     — simple, midpoint, spline lines

Add new skills by creating a module in this package and registering its
tool(s) in ``register_skills()`` below.
"""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from mcp_cad.core.protocol import CADProvider
from mcp_cad.skills.sketch import skill_sketch as _skill_sketch
from mcp_cad.skills.line import skill_line as _skill_line
from mcp_cad.skills.circle import skill_circle as _skill_circle
from mcp_cad.skills.arc import skill_arc as _skill_arc
from mcp_cad.skills.rect import skill_rect as _skill_rect
from mcp_cad.skills.point import skill_point as _skill_point
from mcp_cad.skills.spline import skill_spline as _skill_spline
from mcp_cad.skills.ellipse import skill_ellipse as _skill_ellipse
from mcp_cad.skills.pattern import skill_pattern_circular as _skill_pattern_circular
from mcp_cad.skills.pattern import skill_pattern_rectangular as _skill_pattern_rectangular
from mcp_cad.skills.modify import skill_offset as _skill_offset
from mcp_cad.skills.modify import skill_move as _skill_move
from mcp_cad.skills.modify import skill_rotate as _skill_rotate
from mcp_cad.skills.modify import skill_delete_sketch as _skill_delete_sketch
from mcp_cad.skills.modify import skill_trim as _skill_trim
from mcp_cad.skills.modify import skill_scale as _skill_scale
from mcp_cad.skills.modify import skill_mirror as _skill_mirror
from mcp_cad.skills.constrain import skill_constraint as _skill_constraint
from mcp_cad.skills.dimension import skill_dimension as _skill_dimension
from mcp_cad.skills.revolve import skill_revolve as _skill_revolve


def register_skills(mcp_instance: FastMCP, provider: CADProvider) -> None:
    """Register all skill-based MCP tools on the FastMCP instance.

    Skills are registered AFTER ``register_tools()`` in server.py.
    """

    # ------------------------------------------------------------------
    # Tab: Sketch — Panel: Draw
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def skill_sketch(
        plane: str = "XY",
    ) -> dict[str, Any]:
        """Tab: Sketch — Create or activate a sketch on a work plane.

        Call before any draw skill. Uses the existing active sketch if
        already on the same plane.

        Args:
            plane: "XY" (default), "XZ", or "YZ".

        Examples:
            skill_sketch("XY")
        """
        return _skill_sketch(provider, plane)

    @mcp_instance.tool()
    def skill_line(
        mode: str = "simple",
        end_x: float = 0.0,
        end_y: float = 0.0,
        start_x: float = 0.0,
        start_y: float = 0.0,
        mid_x: float = 0.0,
        mid_y: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a line.

        Modes:
            simple   — from (start_x, start_y) to (end_x, end_y)
            midpoint — centered at (mid_x, mid_y), ends at (end_x, end_y)

        Examples:
            # Simple line
            skill_line(start_x=0, start_y=0, end_x=10, end_y=5)

            # Midpoint line
            skill_line(mode="midpoint", mid_x=5, mid_y=5, end_x=10, end_y=5)
        """
        return _skill_line(
            provider,
            mode=mode,
            end_x=end_x,
            end_y=end_y,
            start_x=start_x,
            start_y=start_y,
            mid_x=mid_x,
            mid_y=mid_y,
        )

    @mcp_instance.tool()
    def skill_circle(
        mode: str = "center",
        cx: float = 0.0,
        cy: float = 0.0,
        radius: float = 1.0,
        x1: float = 0.0,
        y1: float = 0.0,
        x2: float = 0.0,
        y2: float = 0.0,
        x3: float = 0.0,
        y3: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a circle.

        Modes:
            center — center point + radius (cx, cy, radius)
            3point — three perimeter points (x1,y1, x2,y2, x3,y3)

        Examples:
            # Center-radius circle
            skill_circle(cx=5, cy=5, radius=3)

            # 3-point circle
            skill_circle(mode="3point", x1=0, y1=0, x2=10, y2=10, x3=20, y3=0)
        """
        return _skill_circle(
            provider,
            mode=mode,
            cx=cx,
            cy=cy,
            radius=radius,
            x1=x1,
            y1=y1,
            x2=x2,
            y2=y2,
            x3=x3,
            y3=y3,
        )

    @mcp_instance.tool()
    def skill_arc(
        mode: str = "center",
        cx: float = 0.0,
        cy: float = 0.0,
        sx: float = 1.0,
        sy: float = 0.0,
        ex: float = 0.0,
        ey: float = 1.0,
        ccw: bool = True,
        radius: float = 1.0,
        start_angle: float = 0.0,
        sweep_angle: float = 90.0,
        x1: float = 0.0,
        y1: float = 0.0,
        x_mid: float = 0.0,
        y_mid: float = 0.0,
        x_end: float = 0.0,
        y_end: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw an arc.

        Modes:
            center — center + start + end points (cx,cy, sx,sy, ex,ey)
            sweep  — radius + angles in degrees (cx,cy, radius, start_angle, sweep_angle)
            3point — three points (x1,y1, x_mid,y_mid, x_end,y_end)

        Examples:
            # Center arc from right to top around origin
            skill_arc(cx=0, cy=0, sx=10, sy=0, ex=0, ey=10)

            # Semicircle radius 5
            skill_arc(mode="sweep", cx=0, cy=0, radius=5, start_angle=0, sweep_angle=180)

            # 3-point arc
            skill_arc(mode="3point", x1=0, y1=0, x_mid=5, y_mid=5, x_end=10, y_end=0)
        """
        return _skill_arc(
            provider,
            mode=mode,
            cx=cx,
            cy=cy,
            sx=sx,
            sy=sy,
            ex=ex,
            ey=ey,
            ccw=ccw,
            radius=radius,
            start_angle=start_angle,
            sweep_angle=sweep_angle,
            x1=x1,
            y1=y1,
            x_mid=x_mid,
            y_mid=y_mid,
            x_end=x_end,
            y_end=y_end,
        )

    @mcp_instance.tool()
    def skill_rect(
        mode: str = "diagonal",
        x1: float = 0.0,
        y1: float = 0.0,
        x2: float = 10.0,
        y2: float = 10.0,
        cx: float = 0.0,
        cy: float = 0.0,
        corner_x: float = 5.0,
        corner_y: float = 5.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a rectangle.

        Modes:
            diagonal — two opposite corners (x1,y1, x2,y2)
            center   — center + one corner (cx,cy, corner_x, corner_y)

        Examples:
            # Rectangle 10x5 from origin
            skill_rect(x1=0, y1=0, x2=10, y2=5)

            # Centered 20x10 rectangle at (10,5)
            skill_rect(mode="center", cx=10, cy=5, corner_x=20, corner_y=10)
        """
        return _skill_rect(
            provider,
            mode=mode,
            x1=x1,
            y1=y1,
            x2=x2,
            y2=y2,
            cx=cx,
            cy=cy,
            corner_x=corner_x,
            corner_y=corner_y,
        )

    @mcp_instance.tool()
    def skill_point(
        x: float = 0.0,
        y: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a point.

        Examples:
            skill_point(x=10, y=5)
        """
        return _skill_point(provider, x, y)

    @mcp_instance.tool()
    def skill_spline(
        mode: str = "fit",
        points: str = "",
        fit_method: str = "sweet",
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw a spline.

        Modes:
            fit     — passes through all points (smooth interpolation)
            control — points define control polygon vertices

        Args:
            points: Comma-separated coords: "x1,y1,x2,y2,..." (min 3 points).
            fit_method: "sweet" (default), "smooth", or "autocad".

        Examples:
            skill_spline(points="0,0,3,5,7,3,10,0")
            skill_spline(points="0,0,5,10,10,0", fit_method="smooth")
        """
        coords = [float(v) for v in points.split(",")]
        pts = [(coords[i], coords[i + 1]) for i in range(0, len(coords), 2)]
        return _skill_spline(provider, mode=mode, points=pts, fit_method=fit_method)

    @mcp_instance.tool()
    def skill_ellipse(
        cx: float = 0.0,
        cy: float = 0.0,
        major_radius: float = 5.0,
        minor_radius: float = 3.0,
        angle: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Draw — Draw an ellipse.

        Args:
            cx, cy: Center point.
            major_radius, minor_radius: Radii in cm.
            angle: Major axis angle in degrees (0° = +X).

        Examples:
            # Horizontal ellipse 10x6
            skill_ellipse(cx=0, cy=0, major_radius=5, minor_radius=3)

            # Rotated 45°
            skill_ellipse(cx=10, cy=10, major_radius=8, minor_radius=4, angle=45)
        """
        return _skill_ellipse(provider, cx, cy, major_radius, minor_radius, angle)

    @mcp_instance.tool()
    def skill_pattern_circular(
        entities: str = "1",
        axis: str = "1",
        count: int = 6,
        angle: float = 360.0,
        fitted: bool = True,
        symmetric: bool = False,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Pattern — Circular pattern of sketch entities.

        Args:
            entities: Comma-separated entity indices, e.g. "1,2,3".
            axis: Sketch point index for rotation center.
            count: Number of instances including original.
            angle: Degrees between instances or total sweep.
            fitted: True → angle is total sweep, False → offset.
            symmetric: Distribute on both sides.

        Examples:
            # 6 copies equally around axis point 2
            skill_pattern_circular(entities="1", axis="2", count=6)

            # 45° offset between copies
            skill_pattern_circular(entities="1", axis="1", count=4, angle=45, fitted=False)
        """
        return _skill_pattern_circular(
            provider, entities, axis, count, angle, fitted, symmetric)

    @mcp_instance.tool()
    def skill_pattern_rectangular(
        entities: str = "1",
        x_axis: str = "1",
        x_count: int = 2,
        x_spacing: float = 5.0,
        y_axis: str = "",
        y_count: int = 1,
        y_spacing: float = 0.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Pattern — Rectangular pattern of sketch entities.

        Args:
            entities: Comma-separated entity indices.
            x_axis: Linear sketch entity for X direction.
            x_count, x_spacing: Instances and spacing in X (cm).
            y_axis, y_count, y_spacing: Y direction (optional, for 2D grid).

        Examples:
            # Linear: 5 copies at 10cm along X
            skill_pattern_rectangular(entities="1", x_axis="2", x_count=5, x_spacing=10)

            # 2D grid: 3x2 at 5cm
            skill_pattern_rectangular(entities="1", x_axis="2", x_count=3, x_spacing=5, y_axis="3", y_count=2, y_spacing=5)
        """
        return _skill_pattern_rectangular(
            provider, entities, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing)

    @mcp_instance.tool()
    def skill_offset(
        entities: str = "1",
        offset_x: float = 0.0,
        offset_y: float = 1.0,
        include_connected: bool = False,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Offset through a point.

        Examples:
            skill_offset(entities="2", offset_x=0, offset_y=5)
        """
        return _skill_offset(provider, entities, offset_x, offset_y, include_connected)

    @mcp_instance.tool()
    def skill_move(
        entities: str = "1",
        dx: float = 0.0,
        dy: float = 0.0,
        copy: bool = False,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Move sketch entities by a vector.

        Examples:
            skill_move(entities="1", dx=10, dy=0)
            skill_move(entities="1,2", dx=5, dy=5, copy=True)
        """
        return _skill_move(provider, entities, dx, dy, copy)

    @mcp_instance.tool()
    def skill_rotate(
        entities: str = "1",
        cx: float = 0.0,
        cy: float = 0.0,
        angle: float = 90.0,
        copy: bool = False,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Rotate sketch entities.

        Examples:
            skill_rotate(entities="1", cx=0, cy=0, angle=90)
            skill_rotate(entities="1,2,3", cx=5, cy=5, angle=45, copy=True)
        """
        return _skill_rotate(provider, entities, cx, cy, angle, copy)

    @mcp_instance.tool()
    def skill_delete_sketch() -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Delete the active sketch.

        Only works if not consumed by a feature.

        Examples:
            skill_delete_sketch()
        """
        return _skill_delete_sketch(provider)

    @mcp_instance.tool()
    def skill_trim(
        entity: str = "1",
        cutting_entity: str = "2",
        side: str = "end",
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Trim entity to intersection.

        Args:
            entity: Entity index to trim (1-based).
            cutting_entity: Entity to trim against.
            side: "start" or "end" — which endpoint to move.

        Examples:
            skill_trim(entity="1", cutting_entity="2")
            skill_trim(entity="3", cutting_entity="1", side="start")
        """
        return _skill_trim(provider, entity, cutting_entity, side)

    @mcp_instance.tool()
    def skill_scale(
        entities: str = "1",
        cx: float = 0.0,
        cy: float = 0.0,
        factor: float = 2.0,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Scale entities around a center.

        Args:
            entities: Comma-separated entity indices.
            cx, cy: Center point.
            factor: >1 = enlarge, <1 = shrink.

        Examples:
            skill_scale(entities="1,2", cx=0, cy=0, factor=2)
            skill_scale(entities="1", cx=5, cy=5, factor=0.5)
        """
        return _skill_scale(provider, entities, cx, cy, factor)

    @mcp_instance.tool()
    def skill_mirror(
        entities: str = "1",
        mirror_entity: str = "2",
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Modify — Mirror entities across a line.

        Args:
            entities: Comma-separated entity indices to mirror.
            mirror_entity: Index of line used as mirror axis.

        Examples:
            skill_mirror(entities="1", mirror_entity="2")
            skill_mirror(entities="1,3,5", mirror_entity="2")
        """
        return _skill_mirror(provider, entities, mirror_entity)

    @mcp_instance.tool()
    def skill_constraint(
        mode: str = "parallel",
        entity1: str = "1",
        entity2: str = "",
        sym_line: str = "",
        axis: str = "major",
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Constrain — Add geometric constraints.

        Modes: coincident, collinear, concentric, parallel, perpendicular,
        tangent, horizontal, vertical, equal, midpoint, symmetric, smooth.

        Examples:
            skill_constraint(mode="parallel", entity1="1", entity2="2")
            skill_constraint(mode="horizontal", entity1="1")
            skill_constraint(mode="symmetric", entity1="1", entity2="2", sym_line="3")
        """
        return _skill_constraint(provider, mode, entity1, entity2, sym_line, axis)

    @mcp_instance.tool()
    def skill_dimension(
        mode: str = "linear",
        entity1: str = "1",
        entity2: str = "",
        value: float | None = None,
        orientation: str = "aligned",
        position_x: float | None = None,
        position_y: float | None = None,
    ) -> dict[str, Any]:
        """Tab: Sketch → Panel: Constrain — Add a dimension constraint.

        Modes: linear, radius, diameter, angle.

        Examples:
            skill_dimension(mode="linear", entity1="1", entity2="2", value=25)
            skill_dimension(mode="radius", entity1="3", value=5)
        """
        return _skill_dimension(
            provider, mode, entity1, entity2, value, orientation, position_x, position_y)

    # ------------------------------------------------------------------
    # Tab: 3D
    # ------------------------------------------------------------------

    @mcp_instance.tool()
    def skill_revolve(
        plane: str = "XY",
        profile: str = "",
        profile_cx: float = 3.0,
        profile_cy: float = 0.0,
        profile_radius: float = 1.0,
        axis_x: float = 0.0,
        axis_y1: float = -1.0,
        axis_y2: float = 5.0,
        angle: float = 360.0,
        operation: str = "join",
    ) -> dict[str, Any]:
        """Tab: 3D — Revolve a profile around a vertical axis.

        Composes sketch_create + sketch_circle + sketch_line + revolve.
        Defaults produce a torus (circle at x=3 revolved around x=0).
        For a solid cylinder set profile_cx=0.

        Args:
            plane: Work plane ("XY", "XZ", "YZ").
            profile: Existing profile index (e.g. "1"). If empty, draws circle.
            profile_cx, profile_cy: Auto-drawn circle center (cm).
            profile_radius: Auto-drawn circle radius (cm).
            axis_x: X position of vertical axis line.
            axis_y1, axis_y2: Start/end Y of axis line.
            angle: Revolve angle in degrees (360 = full).
            operation: "join", "cut", or "intersect".

        Examples:
            # Torus (donut)
            skill_revolve(profile_cx=5, profile_radius=2)

            # Custom profile — draw shape first, then revolve
            skill_revolve(profile="1", axis_x=0)

            # Cut a groove
            skill_revolve(profile_cx=2, profile_radius=0.5, operation="cut")
        """
        return _skill_revolve(
            provider,
            plane=plane,
            profile=profile,
            profile_cx=profile_cx,
            profile_cy=profile_cy,
            profile_radius=profile_radius,
            axis_x=axis_x,
            axis_y1=axis_y1,
            axis_y2=axis_y2,
            angle=angle,
            operation=operation,
        )
