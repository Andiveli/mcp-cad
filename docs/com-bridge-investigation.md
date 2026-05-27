# COM Bridge Investigation: Inventor 2025+ Advanced Features

> **Status**: Spike investigation — documentation only, no code changes.
> **Date**: 2026-05-27
> **Scope**: HoleFeatures, CircularPatternFeatures, ThreadFeatures

## Summary

mcp-cad uses **pywin32's late-bound `Dispatch`** to automate Autodesk Inventor via COM. This works well for most operations (documents, sketches, extrude, fillet, chamfer, revolve, parameters, properties, exports). However, three feature categories fail due to **pywin32's inability to marshal certain COM types** when calling Inventor 2025 methods that require specialized placement definitions, object collections, or enum parameters.

This document records what works, what's blocked, the root cause, and viable workarounds.

---

## What Works (MCP Tools Operational)

| Tool | Status | Notes |
|------|--------|-------|
| `connect` / `disconnect` / `health` | ✅ Full | COM lifecycle works |
| `doc_open` / `doc_new_part` / `doc_new_assembly` | ✅ Full | Template resolution via `GetTemplateFile` |
| `doc_save` / `doc_save_as` / `doc_close` | ✅ Full | |
| `sketch_create` / `sketch_line` / `sketch_circle` / `sketch_arc` / `sketch_rectangle` / `sketch_dimension` | ✅ Full | |
| `extrude` | ✅ Full | Uses `CreateExtrudeDefinition` + `SetDistanceExtent` with hardcoded enum values |
| `revolve` | ✅ Full | Uses `CreateRevolveDefinition` |
| `fillet` | ✅ Full | Uses `AddSimple` + `TransientObjects.CreateEdgeCollection()` |
| `chamfer` | ✅ Full | Uses `AddUsingDistance` / `AddUsingTwoDistances` + `EdgeCollection` |
| `param_list` / `param_get` / `param_set` / `param_set_expression` | ✅ Full | |
| `iproperty_get` / `iproperty_set` / `iproperty_summary` / `iproperty_custom_get` / `iproperty_custom_set` | ✅ Full | |
| `export_step` / `export_stl` / `export_pdf` / `export_dxf` | ✅ Full | |
| `crear_patron_taladros` (drilling skill) | ✅ Full | Composes `sketch_create` → `sketch_circle` → `extrude(cut)` |

**Total**: 30+ working operations via MCP.

---

## What's Blocked

### 1. HoleFeatures — `CreateSketchPlacementDefinition` / `CreatePointPlacementDefinition`

**What we tried**:
```python
hole_features = comp_def.Features.HoleFeatures
placement_def = hole_features.CreateSketchPlacementDefinition(sketch_point, planar_face)
# Also tried:
placement_def = hole_features.CreatePointPlacementDefinition(point, planar_face)
```

**Error**:
```
TypeError: Python instance can not be converted to a COM object
```
—or—
```
pywintypes.com_error: (-2147352571, 'Type mismatch', None, 2)
```

**Root cause**: The `CreateSketchPlacementDefinition` and `CreatePointPlacementDefinition` methods require Inventor COM objects as parameters (`SketchPoint`, `PlanarFace` references). pywin32's late-bound Dispatch cannot convert Python objects into the COM interface types that Inventor expects. When we pass a COM object retrieved earlier (e.g., a point from `TransientGeometry.CreatePoint`), the marshaling layer still fails because the method signature expects a specific COM interface (`SketchPoint`) rather than a generic IDispatch.

**Impact**: No native hole creation via MCP. Holes are one of the most common mechanical features.

### 2. CircularPatternFeatures — `ObjectCollection` Marshaling

**What we tried**:
```python
features = comp_def.Features
pattern_features = features.CircularPatternFeatures

# Create an ObjectCollection for the features to pattern
obj_col = app.TransientObjects.CreateObjectCollection()
obj_col.Add(extrude_feature)

pattern_def = pattern_features.CreateDefinition(
    obj_col,           # parent features to pattern
    axis_face,         # rotation axis
    count,             # number of occurrences
    angle,             # angle between occurrences
    True               # compute direction
)
```

**Error**:
```
TypeError: Python instance can not be converted to a COM object
```

**Root cause**: `CreateDefinition` requires an `ObjectCollection` of COM feature objects as its first parameter. While `TransientObjects.CreateObjectCollection()` successfully creates the collection, and we can `Add()` features to it, pywin32 cannot marshal this `ObjectCollection` into the method's first parameter position. The type conversion fails at the `IDispatch.Invoke` boundary because pywin32 doesn't recognize the ObjectCollection as matching the expected COM interface type.

**Impact**: No circular/rectangular pattern features via MCP. Patterns are essential for bolt-hole circles, cooling fins, etc.

### 3. ThreadFeatures — `CreateThreadInfo` / `CreateStandardThreadInfo`

**What we tried**:
```python
thread_features = comp_def.Features.ThreadFeatures
thread_info = thread_features.CreateThreadInfo(
    thread_type,    # e.g., "ANSI Unified Screw Threads"
    size,           # e.g., "1/4"
    designation,    # e.g., "1/4-20 UNC"
    class_,        # e.g., "2B"
)

# Also tried CreateStandardThreadInfo with enum values
```

**Error**:
```
pywintypes.com_error: (-2147352571, 'Type mismatch', None, 2)
```

**Root cause**: `CreateThreadInfo` requires `ThreadTypeEnum` and string parameters that must match Inventor's internal thread table exactly. pywin32's late-bound Dispatch doesn't have access to the type library to resolve these enum values or validate the parameter types. The method may also need specific variant types (VT_BSTR, VT_I4) that late binding doesn't enforce correctly.

**Impact**: No thread features via MCP. Threads are cosmetic in Inventor (they don't affect geometry), but are important for manufacturing documentation.

---

## Root Cause Analysis

All three blocked features share the same underlying issue:

**pywin32's late-bound `Dispatch` cannot properly marshal certain COM types that Inventor 2025 methods require:**

1. **ObjectCollection to method parameters** — ObjectCollections created via `TransientObjects` can be populated, but pywin32 won't pass them as typed parameters to `CreateDefinition` methods.
2. **Specific COM interfaces** — Methods like `CreatePointPlacementDefinition` expect a `SketchPoint` or `PlanarFace` interface, not a generic IDispatch. Late binding only knows about IDispatch.
3. **Enum/variant type enforcement** — Methods like `CreateThreadInfo` need their string/enum parameters to arrive as specific variant types (VT_BSTR, VT_I4), but late binding passes everything as generic variants.

---

## Research Paths

### Path 1: Early Binding via `EnsureDispatch`

**Approach**: Use `win32com.client.gencache.EnsureDispatch("Inventor.Application")` to generate Python type wrappers from Inventor's COM type library. Early binding provides:
- Full type information for method parameters
- Proper COM interface marshaling
- Access to enumeration constants via `win32com.client.constants`

**Risk**: The `gen_py` cache generated by `EnsureDispatch` **corrupts the behavior of regular `Dispatch`**. After using `EnsureDispatch`, `Document.ComponentDefinition` access breaks because the generated wrappers change the object model. The cache must be cleared (`%LOCALAPPDATA%\Temp\gen_py`) to restore normal operation.

**Feasibility**: Medium. Works for one-off scripts but not sustainable for a long-running server that mixes early and late binding. Could potentially use a separate subprocess or isolated COM apartment.

**Priority**: Low. This was already attempted during the initial audit. The cache corruption issue makes it unsuitable for a mixed-binding server.

### Path 2: Raw IDispatch.Invoke with DISPPARAMS

**Approach**: Bypass pywin32's marshaling entirely by calling `IDispatch.Invoke` directly with manually constructed `DISPPARAMS` structures and `VARIANT` arguments. This gives complete control over parameter types.

```python
import pythoncom
from win32com.client import Dispatch

# Get the IDispatch pointer
inv = Dispatch("Inventor.Application")
dispatch = inv._oleobj_

# Manually construct DISPPARAMS with correct VARIANT types
# DISPID, lcid, flags, args...
```

**Risk**: Extremely verbose and fragile. Requires discovering DISPIDs for each method via `GetIDsOfNames`. Error handling is manual. No type safety.

**Feasibility**: Low-to-medium. Technically possible but requires significant COM interop code per method. Maintenance burden is high.

**Priority**: Low. Reserve for critical features only.

### Path 3: Workaround via Extrude-Cut (Proven Working)

**Approach**: Use the existing working sketch + circle + extrude-cut pipeline as a replacement for native HoleFeatures and CircularPatternFeatures.

For holes, this is straightforward:
1. Create a sketch on the target plane
2. Draw a circle (diameter = hole diameter)
3. Extrude-cut to depth

For circular patterns, this is a loop:
1. Calculate positions using trigonometry
2. For each position, create a sketch circle
3. Extrude-cut all profiles at once

**Already implemented**: `mcp_cad/skills/drilling.py` — `crear_patron_taladros()` composes `sketch_create` → `sketch_circle` (loop) → `extrude(cut)`.

**Risk**: None. Uses fully working protocol operations.

**Feasibility**: High. Hole geometry via extrude-cut is functionally equivalent to native `HoleFeatures` for simple through-holes and blind holes. Does not create hole annotations or tap data (which native HoleFeatures provides), but geometry is identical.

**Limitations**:
- No hole note/annotation in drawings (native HoleFeatures creates these)
- No thread display (native ThreadFeatures creates cosmetic threads)
- No automatic center mark in drawing views
- Circular patterns via loop are geometrically correct but don't create a Pattern feature (can't edit count/angle parametrically)

**Priority**: High. This is the recommended approach for immediate functionality.

---

## Workarounds

### Hole Feature: `create_hole_via_extrude_cut` Pattern

The following pattern creates a hole using only working protocol operations:

```python
# Hole workaround: sketch circle + extrude cut
# Equivalent to: HoleFeatures.CreateHole(diameter=3cm, depth=2cm)

provider.sketch_create("XY")                    # Create sketch on target plane
provider.sketch_circle(cx=5, cy=5, radius=1.5) # Draw circle at hole center
provider.extrude("1", distance=2.0, operation="cut")  # Cut through
```

**Existing implementation** — `mcp_cad/skills/drilling.py`:

```python
# The crear_patron_taladros() skill already implements this pattern for
# multiple holes in a linear pattern. A single-hole helper is a thin wrapper:

def create_hole_via_extrude_cut(
    provider: CADProvider,
    position: tuple[float, float],
    diameter: float,
    depth: float,
    plane: str = "XY",
) -> dict[str, Any]:
    """Create a single hole using sketch circle + extrude-cut.

    Args:
        provider: A CADProvider instance.
        position: (x, y) center of the hole in cm.
        diameter: Hole diameter in cm.
        depth: Cut depth in cm.
        plane: Work plane for the sketch (default "XY").

    Returns:
        Dict with success status, hole_diameter, and depth.

    Raises:
        ValueError: If diameter <= 0 or depth <= 0.
    """
    if diameter <= 0:
        raise ValueError(f"Hole diameter must be > 0, got {diameter}")
    if depth <= 0:
        raise ValueError(f"Hole depth must be > 0, got {depth}")

    radius = diameter / 2.0
    provider.sketch_create(plane)
    provider.sketch_circle(position[0], position[1], radius)
    provider.extrude("1", distance=depth, direction="positive", operation="cut")

    return {
        "success": True,
        "hole_diameter": diameter,
        "depth": depth,
    }
```

**Note**: This helper is documented here as a pattern. The `crear_patron_taladros` skill in `mcp_cad/skills/drilling.py` provides the same composition for the multi-hole case.

### Circular Pattern: Manual Position Calculation

For circular patterns (bolt-hole circles, cooling fins), compute positions with trigonometry and create features at each position:

```python
import math

def create_circular_pattern_via_loop(
    provider: CADProvider,
    center: tuple[float, float],
    radius: float,
    count: int,
    hole_diameter: float,
    depth: float,
    plane: str = "XY",
) -> dict[str, Any]:
    """Create a circular pattern of holes calculated manually.

    Each hole is positioned using sin/cos around a circle.
    All circles are drawn in one sketch, then cut in one extrude.
    """
    hole_radius = hole_diameter / 2.0
    provider.sketch_create(plane)

    for i in range(count):
        angle = 2 * math.pi * i / count
        x = center[0] + radius * math.cos(angle)
        y = center[1] + radius * math.sin(angle)
        provider.sketch_circle(x, y, hole_radius)

    provider.extrude("1", distance=depth, operation="cut")

    return {
        "success": True,
        "pattern_type": "circular",
        "count": count,
        "pattern_radius": radius,
        "hole_diameter": hole_diameter,
        "depth": depth,
    }
```

**Limitation**: This creates individual features, not a parametric `CircularPatternFeature`. The count and spacing are not editable as a pattern parameter.

### Thread Features: Cosmetic Only or Skip

Thread features in Inventor are **cosmetic** — they don't modify the solid geometry. Two options:

1. **Skip threads entirely** — the model geometry is complete without them.
2. **Model the thread groove** — use a helical sweep cut for visual threads (complex, requires revolve + sweep path).

For manufacturing documentation, the thread specification can be stored as a custom iProperty:

```python
provider.iproperty_custom_set("Thread_Spec", "1/4-20 UNC")
provider.iproperty_custom_set("Thread_Class", "2B")
provider.iproperty_custom_set("Thread_Type", "ANSI Unified")
```

---

## Spike Notes

### HoleFeatures — Spike Record

**Date tested**: 2026-05-26  
**Method**: Late-bound `Dispatch`  
**Approaches tried**:

1. **Direct `CreateSketchPlacementDefinition(sketch_point, planar_face)`**
   - Result: `TypeError: Python instance can not be converted to a COM object`
   - `sketch_point` was obtained from `TransientGeometry.CreatePoint()`, `planar_face` from `SurfaceBodies.Item(1).Faces.Item(1)`
   - Both are valid COM objects, but pywin32 won't marshal them to the expected interface types

2. **Direct `CreatePointPlacementDefinition(point, face)`**
   - Result: `pywintypes.com_error: (-2147352571, 'Type mismatch', None, 2)`
   - The method expects specific COM interfaces (`Point` and `Face`), not the generic IDispatch that late binding provides

**Workaround status**: ✅ Proven — `sketch_circle` + `extrude(cut)` produces identical geometry.

### CircularPatternFeatures — Spike Record

**Date tested**: 2026-05-26  
**Method**: Late-bound `Dispatch`  
**Approaches tried**:

1. **`CreateDefinition(ObjectCollection, axis_face, count, angle, compute_direction)`**
   - Result: `TypeError: Python instance can not be converted to a COM object`
   - The `ObjectCollection` was created via `TransientObjects.CreateObjectCollection()` and populated with a valid ExtrudeFeature object
   - pywin32 cannot marshal the `ObjectCollection` into the method's first parameter position

2. **Passing a single feature directly instead of ObjectCollection**
   - Result: Same `TypeError`
   - The method requires ObjectCollection specifically, but even single-element collections fail

**Workaround status**: ⚠️ Partial — manual loop with calculated positions produces identical geometry, but creates individual features, not a parametric pattern.

### ThreadFeatures — Spike Record

**Date tested**: 2026-05-26  
**Method**: Late-bound `Dispatch`  
**Approaches tried**:

1. **`CreateThreadInfo(thread_type, size, designation, class)`**
   - Result: `pywintypes.com_error: (-2147352571, 'Type mismatch', None, 2)`
   - Even with correct string values from Inventor's thread table (e.g., "ANSI Unified Screw Threads", "1/4", "1/4-20 UNC", "2B"), the method fails
   - The `thread_type` parameter likely requires a `ThreadTypeEnum` value, not a string

2. **`CreateStandardThreadInfo()`**
   - Not reachable — fails before method dispatch due to type issues

**Workaround status**: ⚠️ Minimal — thread spec can be stored as custom iProperties for documentation. Thread **geometry** is cosmetic and can be omitted or modeled manually via helical sweep.

---

## Priority and Next Steps

| Priority | Action | Effort | Impact |
|----------|--------|--------|--------|
| **P0 (done)** | Extrude-cut workaround for holes | ✅ Complete | Enables hole creation via skill |
| **P1** | Add `create_hole_via_extrude_cut()` and `create_circular_pattern_via_loop()` helpers to skills module | Low | Cleaner API for common patterns |
| **P2** | Investigate early binding in isolated subprocess for HoleFeatures | Medium | Could unblock native holes |
| **P3** | Investigate IDispatch.Invoke bypass for ObjectCollection marshaling | Medium-High | Could unblock circular patterns |
| **P4** | Thread features — currently cosmetic-only, document via iProperties | Low | Nice-to-have for drawing annotations |

### Recommended Path Forward

1. **Ship the extrude-cut workaround** — holes are the most requested feature and the workaround produces identical geometry.
2. **Add single-hole and circular-pattern skill helpers** to `mcp_cad/skills/` — thin wrappers over the existing sketch → circle → extrude-cut pipeline.
3. **Defer early binding investigation** — the `gen_py` cache corruption issue makes it risky for a server process. If needed in the future, run `EnsureDispatch` in a short-lived subprocess that generates the cache, then use `EnsureDispatch` only for the specific blocked methods while keeping the main server on `Dispatch`.
4. **Defer IDispatch.Invoke** — significant maintenance burden for marginal benefit. Revisit only if early binding proves insufficient.
5. **Thread features** are cosmetic — store specifications in iProperties and revisit only if drawing annotation is a hard requirement.

---

## Appendix: COM Enum Constants (Inventor 2025+)

These values were determined via `gencache.EnsureDispatch("Inventor.Application")` + `dir(constants)` during the initial audit. They are hardcoded in `mcp_cad/providers/inventor/feature.py` for late-bound Dispatch usage.

```python
# PartFeatureOperationEnum
kNewBodyOperation = 20485
kJoinOperation    = 20481
kCutOperation     = 20482
kIntersectOperation = 20483

# PartFeatureExtentDirectionEnum
kPositiveExtentDirection = 20993
kNegativeExtentDirection = 20994
kSymmetricExtentDirection = 20995
```

**Warning**: After using `EnsureDispatch`, clear `%LOCALAPPDATA%\Temp\gen_py` before running the server with regular `Dispatch`. The generated type library cache corrupts `Document.ComponentDefinition` access.