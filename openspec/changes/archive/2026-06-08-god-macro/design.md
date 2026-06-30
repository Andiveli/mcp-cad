# Design: God Macro — Full-Workflow Single-Call Macro

## Technical Approach

New `src/McpCad.Tools/MacroTools.cs` with a single `macro_god_part` MCP tool that composes sketch → feature → pattern → modify → verify in one call. Each phase is optional and independently try-caught. Structured sketch data (entities, constraints, dimensions) passes via JSON strings since MCP only accepts flat C# params. Follows the proven `macro_basic_part` envelope pattern (tree, bbox, parameters, viewport_images, warnings). `ask_before_modify` guard inspects `Health()` + `GetFeatureTree()` before modifying existing geometry.

## Architecture Decisions

| # | Decision | Options | Tradeoffs | Choice |
|---|----------|---------|-----------|--------|
| 1 | Param surface | Flat C# params vs grouped JSON | Flat: 30+ params, clear `[Description]`. Grouped JSON: flexible, fragile parsing. | **Flat params** with JSON for sketch data only |
| 2 | Phase order | Fixed vs custom | Fixed: simple, matches CAD workflow. Custom: complex, overkill for single call. | **Fixed**: sketch → feature → pattern → modify → verify |
| 3 | Partial failure | Fail-fast vs per-phase catch | Fail-fast: predictable, doc may be partial. Per-phase: resilient, envelope shows `phase_status`. | **Per-phase try-catch**, `phase_status` dict reports each phase |
| 4 | Existing-geometry guard | `Health()` only vs `GetFeatureTree()` | Health: fast, superficial. FeatureTree: precise, counts features. | **Both**: Health() for connection, FeatureTree for feature count ≥ 1 |
| 5 | Ask-before-modify behavior | Error vs confirmation envelope | Error: breaks agent flow. Confirmation envelope: agent can re-call with guard=false. | **Confirmation envelope**: `{ success: true, needs_confirmation: true, current_state: { ... } }` |
| 6 | Sketch JSON format | Flat array vs nested objects | Array: simple, each entity is `{ type, params }`. Nested: more structure, harder to parse. | **Flat array** of `{ type, [params] }` objects |
| 7 | Polygon support | Excluded vs built from lines | Excluded: users draw N lines manually. Built: macro generates line segments from center+radius+N. | **Built from lines**: new `polygon` type in sketch JSON |

## Data Flow

```
  macro_god_part(params)
       │
       ├─ 1. Parse sketch JSON ──→ errors → envelope
       ├─ 2. Sketch phase ───────→ entities + constraints + dimensions
       │      ├─ sketch_create(plane)
       │      ├─ for each entity: sketch_line/circle/rect/arc/spline/point
       │      ├─ for each constraint: sketch_constraint(mode, ...)
       │      └─ for each dimension: sketch_dimension(mode, ...)
       ├─ 3. Feature phase ──────→ extrude | revolve | sweep | loft | coil | rib
       │      └─ feature_type + operation + params → provider call
       ├─ 4. Pattern phase ──────→ circular | rectangular | mirror
       │      └─ delegate to CircularPattern / RectangularPattern / MirrorFeature
       ├─ 5. Modify phase ───────→ fillet | chamfer | shell | draft | thread | split
       │      └─ delegate to provider method
       ├─ 6. Verify phase ───────→ tree + bbox + params + viewport
       │      └─ best-effort, failures → warnings[]
       └─ 7. Build envelope ─────→ success + geometry_created + phase_status + verify data
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/McpCad.Tools/MacroTools.cs` | Create | `[McpServerToolType]` class with `macro_god_part` method (~800-1000 lines) |
| `src/McpCad.Tools/ToolHelpers.cs` | Modify | Add `Success()` method (mirrors `Ok()` pattern), add `Merge()` for phase dictionaries |
| `src/McpCad.Tools/SkillTools.cs` | None | `macro_basic_part` untouched, coexists as simpler alternative |

## Interfaces / Contracts

### JSON Sketch Schema

```jsonc
// sketch_entities: optional JSON array of entity definitions
[
  { "type": "line",   "x1": 0, "y1": 0, "x2": 10, "y2": 0, "tag": "base" },
  { "type": "circle", "cx": 5, "cy": 5, "radius": 3, "tag": "hole" },
  { "type": "rect",   "x1": -5, "y1": -5, "x2": 15, "y2": 5 },
  { "type": "arc",    "cx": 0, "cy": 0, "radius": 5, "start_angle": 0, "end_angle": 180 },
  { "type": "spline", "points": "0,0,5,10,10,0", "fit_method": "sweet" },
  { "type": "point",  "x": 10, "y": 10 },
  { "type": "polygon", "cx": 0, "cy": 0, "radius": 5, "sides": 6, "start_angle": 0 }
]

// constraints: optional JSON array
[
  { "mode": "coincident", "entity1": "1", "entity2": "2" },
  { "mode": "parallel",   "entity1": "3", "entity2": "5" },
  { "mode": "horizontal", "entity1": "4" }
]

// dimensions: optional JSON array
[
  { "mode": "linear",   "entity1": "1", "entity2": "2", "value": 10.0 },
  { "mode": "radius",   "entity1": "2", "value": 3.0 },
  { "mode": "angle",    "entity1": "3", "entity2": "4", "value": 45.0 }
]
```

### Envelope Contract

```jsonc
{
  "success": true,
  "geometry_created": true,
  "document_state": "new" | "existing",
  "phase_status": {
    "sketch": { "success": true, "entities": 4, "constraints": 2 },
    "feature": { "success": true, "feature_type": "extrude", "feature_name": "Extrusion 1" },
    "pattern": null, // skipped
    "modify":  { "success": true, "feature_type": "fillet" },
    "verify":  { "success": true }
  },
  "tree": { "feature_count": 2, "features": [...] },
  "bounding_box": { "min": [...], "max": [...], "size": [...] },
  "parameters": { ... },
  "viewport_images": [ ... ],
  "warnings": [],
  "next": "ready for additional operations"
}
```

### Ask-Before-Modify Envelope

```jsonc
// When ask_before_modify=true and existing features detected:
{
  "success": true,
  "needs_confirmation": true,
  "message": "Document has 3 existing features. Set ask_before_modify=false to proceed, or add to a new part.",
  "current_state": {
    "document_state": "existing",
    "feature_count": 3,
    "tree": { ... }
  }
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | JSON parsing | Parse valid + malformed sketch JSON, verify error messages |
| Unit | ask_before_modify guard | Mock Health() + GetFeatureTree() with 0/1/5 features |
| Integration | Phase composition | Create sketch JSON + feature → verify tree contains expected feature |
| Integration | Per-phase failure | Supply bad entity params → verify partial success envelope |
| E2E | Full pipeline | Sketch(rect+circle) → extrude → fillet → verify tree + bbox + viewport |

## Migration / Rollout

No migration required. `macro_god_part` is additive — `macro_basic_part` and all existing tools coexist unchanged. Delete `MacroTools.cs` for rollback.

## Open Questions

- [ ] Should `polygon` be in the sketch JSON or a separate `polygon_sides` param? JSON is cleaner, keeps param surface flat.
- [ ] Feature phase: should each feature type (extrude/revolve/sweep/loft/coil/rib) have its own param group, or one shared set with type dispatcher? Shared set with type dispatch — fewer params, clearer contract.
