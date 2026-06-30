# Proposal: God Macro — Full-Workflow Single-Call Macro

## Intent

Replace multi-step atomic/skill tool chains with a single `macro_god_part` call. Users currently need 5–15 sequential MCP calls for sketch + 3D + patterns + modify. This macro composes all phases into one call, reducing token usage and latency while keeping `macro_basic_part` for quick starts.

## Scope

### In Scope
- New `MacroTools.cs` file with `[McpServerToolType]` class
- Phase-based composition: sketch → feature → pattern → modify → verify
- All 6 sketch entity types via JSON (line, circle, arc, rect, spline, point)
- All 12 geometric constraints + dimensions via JSON
- Sketch modify: trim, offset, move, rotate, scale, mirror (Extend deferred)
- All 6 3D create features: extrude, revolve, sweep, loft, coil, rib
- All 7 3D modify features: fillet, chamfer, shell, draft, thread, split
- 3D patterns: circular, rectangular, mirror
- Sketch patterns: circular, rectangular, mirror
- `ask_before_modify` guard (doc check → confirmation prompt)
- Polygon built from lines (no atomic polygon tool exists)
- Rich envelope consistent with `macro_basic_part`
- Coexists with `macro_basic_part` (no conflict, no removal)

### Out of Scope
- Extend operation (not implemented anywhere in managers)
- Assembly-level operations
- Non-Inventor providers
- Material assignment (separate iProperty tool)
- Builder-pattern API (over-engineered for single-call MCP)

## Capabilities

### New Capabilities
- `macro-god-part`: Full-workflow single-call macro composing sketch entities, constraints, dimensions, 3D features, patterns, and modify operations via phase-based orchestration with JSON-structured sketch input.

### Modified Capabilities
- `mcp-tool-registration`: Register `macro_god_part` as a new MCP tool in the registry.

## Approach

New `src/McpCad.Tools/MacroTools.cs` with phase-based composition. Each phase optional; macro composes into one shot. Sketch data via JSON strings (flat C# params limitation). `ask_before_modify` inspects doc via `Health()` + `GetFeatureTree()` — if existing geometry detected, returns `needs_confirmation: true` instead of proceeding. Rich envelope: tree, bbox, params, viewport images, warnings.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/McpCad.Tools/MacroTools.cs` | New | God-macro file (~600-1000 lines) |
| `src/McpCad.Tools/ToolHelpers.cs` | Modified | May add shared macro helpers |
| `IMechanicalCadProvider.cs` | None | All ops exist |
| `InventorProvider.cs` / Managers | None | All pass-through ready |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| JSON parsing errors | Medium | `System.Text.Json` with explicit validation messages |
| Mid-phase partial state | Medium | Per-phase try/catch with `geometry_created: true/false/partial` |
| 25-30 param surface | High | Clear `[Description]` on every param, grouped with regions |

## Rollback Plan

Delete `MacroTools.cs`, unregister tool from registry. `macro_basic_part` remains unaffected.

## Dependencies

- `System.Text.Json` (already in .NET)
- All existing provider methods (no new manager changes)

## Success Criteria

- [ ] `macro_god_part` tool registered and callable via MCP
- [ ] Phase composition produces correct geometry for 3+ complex scenarios
- [ ] `ask_before_modify` correctly detects existing geometry and returns confirmation
- [ ] JSON sketch input creates correct entities, constraints, and dimensions
- [ ] Envelope contains all verification data (tree, bbox, params, images)
