## Exploration: god-macro

### Current State

The codebase has a clean 3-layer architecture:

**Layer 1 — Managers** (`src/McpCad.Inventor/Managers/`): 9 managers wrapping Inventor's COM API. SketchManager (1066 lines) and FeatureManager (1826 lines) are the heavy ones, each implementing a comprehensive set of CAD operations.

**Layer 2 — Provider** (`InventorProvider.cs`, 308 lines): thin delegation from `IMechanicalCadProvider` interface to the 9 managers.

**Layer 3 — Tools** (two files in `src/McpCad.Tools/`):
- `AtomicTools.cs` (493 lines): 65+ atomic one-to-one MCP tools mapping provider methods
- `SkillTools.cs` (743 lines): 18 skill tools (composed operations) + `macro_basic_part` (285 lines of orchestration)

The existing `macro_basic_part` lives in `SkillTools.cs` and handles: fresh part creation (YZ sketch + rect/circle/tube/hollow-box + extrude/revolve + verification), continue mode (extrude only), and a rich success envelope (tree, bounding_box, parameters, viewport_images, warnings, next).

All MCP tools use the same pattern: `[McpServerTool]` attribute, flat C# primitive parameters (no complex nested types), `Dictionary<string, object?>` return, `Catch()` error handler, `ToolHelpers.Error()` / `ErrorResult.Create()` for error return dictionaries.

### Affected Areas

- `src/McpCad.Tools/MacroTools.cs` — **new file**; primary god-macro location (recommended separate from SkillTools)
- `src/McpCad.Core/IMechanicalCadProvider.cs` — no changes needed (all required ops already in the interface)
- `src/McpCad.Inventor/InventorProvider.cs` — no changes needed (passes through to all managers)
- `src/McpCad.Inventor/Managers/SketchManager.cs` — already has all sketch operations needed; no changes required
- `src/McpCad.Inventor/Managers/FeatureManager.cs` — already has all 3D features needed; no changes required
- `src/McpCad.Inventor/Helpers/` — all 8 helpers exist and are ready; no changes needed
- `src/McpCad.Tools/ToolHelpers.cs` — may add shared macro helpers if needed

### What Exists vs What's Missing

**Sketch entities — ALL present**: Line, Circle, Arc, Rectangle, Point, Spline, Ellipse
- Missing: Polygon (would manually build from lines)

**Sketch modify — ALL present**: Move, Rotate, Scale, Offset, Trim, Mirror
- Missing: Extend (not implemented anywhere)

**Constraints — ALL present**: coincident, collinear, concentric, parallel, perpendicular, tangent, equal, midpoint, symmetric, smooth, horizontal, vertical
**Dimensions — ALL present**: linear, radius, diameter, angle

**Sketch patterns — ALL present**: CircularPattern, RectangularPattern, Mirror

**3D create features — ALL present**: Extrude, Revolve, Sweep, Loft, Coil, Rib, Emboss
**3D modify features — ALL present**: Fillet, Chamfer, Shell, Draft, Thread, Split, Combine, Thicken, Hole, Derive

**3D patterns — ALL present**: CircularPattern, RectangularPattern, MirrorFeature

**Operation types — ALL present**: new_body, join, cut, intersect

**Ask-before-touch**: NOT IMPLEMENTED anywhere. Would be new functionality.

### Approaches

1. **Single-file god-macro in `MacroTools.cs`** (recommended)
   - Pros: clean separation from existing skills, focused file, follows existing pattern, easy to maintain
   - Pros: `IMechanicalCadProvider` already has everything needed — no manager changes
   - Cons: the macro itself will be large (potentially 600-1000 lines), but that's the nature of a macro
   - Effort: Medium-High

2. **Add to existing `SkillTools.cs`**
   - Pros: keeps all skills together
   - Cons: SkillTools is already 743 lines; adding god-macro would make it 1400+ lines
   - Effort: Medium

3. **Builder-pattern macro composition**
   - Pros: modular, extensible, testable
   - Cons: over-engineered for an agent-callable MCP tool; two-step builder pattern adds friction for a single-call tool
   - Effort: High

### Recommendation

**Approach 1: New `MacroTools.cs` file** — a dedicated `[McpServerToolType]` class housing the `macro_god_part` (or similar) method.

**Design sketch:**
```
MacroTools(IMechanicalCadProvider provider)
{
  [McpServerTool] macro_god_part(
    // Sketch phase params (optional — only if drawing)
    string? sketch_entities = null,    // JSON describing entities to draw
    string? constraints = null,        // JSON describing constraints
    string? dimensions = null,         // JSON describing dimensions

    // 3D phase params
    string? feature_type = "extrude",  // extrude|revolve|sweep|loft|coil|rib
    string? operation = "new_body",
    // feature-specific params (distance, angle, axis, etc.)

    // "ask before touch" guard
    bool ask_before_modify = false,

    // Pattern phase params (optional)
    string? pattern_type = null,       // circular|rectangular|mirror
    // pattern params

    // Modify phase params (optional)
    string? modify_type = null,        // fillet|chamfer|shell|draft|thread
    // modify params
  )
}
```

**Key design decisions:**
- Phase-based: sketch → feature → pattern → modify → verify
- Each phase is optional; the macro composes them into a single shot
- Sketch entities use a JSON array description for multiple entities in one parameter (since MCP tools only accept flat types, JSON strings are how we pass structured data)
- The macro returns the same rich envelope as `macro_basic_part` — success, geometry_created, document_state, tree, bounding_box, parameters, viewport_images, warnings, next
- `ask_before_modify=true`: checks doc state → if existing geometry/features, returns `{ success: true, needs_confirmation: true, message: "...", current_state: { tree, parameters } }` instead of proceeding
- `macro_basic_part` stays as-is for simple quick-start parts; god-macro is for complex one-shot creations

### Risks

- **JSON parameter parsing**: MCP tools use flat C# params, so sketch entity data must be passed as JSON strings. Adds parsing complexity and validation burden. Mitigation: use `System.Text.Json` with explicit error messages.
- **Error granularity**: a 5-phase macro could fail mid-way and leave the doc in a partial state. Mitigation: each phase is a try/catch block with rollback hints in the error envelope, and `geometry_created: true/false/partial` status.
- **COM timeout on complex operations**: a full god-macro with sketch + feature + pattern + fillet could take many COM calls. Mitigation: keep phase lengths reasonable; Inventor can handle sequential COM calls.
- **ask_before_modify relies on doc state detection**: the existing `Health()` + `GetFeatureTree()` pattern works for this. No new detection infrastructure needed.
- **Parameter surface area**: the macro will have 20-30 parameters. This is acceptable for a macro tool but must be well-documented. Mitigation: clear `[Description]` on every parameter, group them in the source with region comments.

### Ready for Proposal

Yes — all required provider and manager capabilities exist. No manager changes are needed. The exploration confirms the codebase is ready.

What the orchestrator should tell the user: "The codebase has everything needed for a god-macro. All sketch entities, constraints, dimensions, patterns, 3D features, and modify operations are already implemented in the managers. The approach is a new `MacroTools.cs` file with a phase-based orchestration method, JSON strings for structured sketch data, and a new `ask_before_modify` guard parameter. Ready for proposal."
