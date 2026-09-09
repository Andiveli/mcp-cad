# Exploration: Onshape Part Orchestrator

**Change:** `onshape-part-orchestrator`  
**Artifact store:** OpenSpec  
**Phase:** explore only

## Summary

The repository already has the low-level write seam and most payload builders needed for a bounded multi-stage workflow, but not the reference model needed for arbitrary part plans. The safest first slice is a **strict, versioned, finite plan schema** executed in declared order, with complete preflight validation, plan-local IDs resolved only to earlier successful stages, stop-on-first-modeling-failure behavior, and explicit partial-completion reporting. It should not be a script interpreter or claim full coverage of the motivating drawing.

## 1. Reusable symbols, flows, and constraints

### Existing layers and seams

- `ICadWebProvider` (`src/McpCad.Core/ICadWebProvider.cs`) is the provider boundary. Its reusable operations are `SketchReplace`, generic `FeatureAdd`, `FeatureList`, topology inspection, and `CaptureView`.
- `OnshapeProvider` delegates Part Studio work to `PartStudioManager`; writes are synchronous REST calls and successful responses update the driver's microversion.
- `OnshapeSketchBuilder` produces declarative `BTMSketch` envelopes. Existing builders cover rectangles, lines, circles, arcs, polylines, splines, ellipses, points, construction axes, legacy closed line profiles, and validated mixed line/arc closed profiles.
- `OnshapeFeatureTemplates` produces typed feature envelopes for extrude, revolve, sweep, fillet, chamfer, shell, patterns, mirror, offset plane, and edge work point. Tool-facing dimensions are cm and are converted once to meters at this boundary.
- `OnshapeAtomicTools` exposes these primitives independently. `OnshapeSkillTools` is the natural MCP/tool seam for composed workflows.
- `skill_sketch_and_extrude` demonstrates response-ID extraction and sketch-to-extrude linkage.
- `skill_onshape_god_part` demonstrates the closest reusable control flow: validate before the first mutation, append ordered feature phases, resolve returned feature IDs defensively, stop after the first dependent failure, preserve the latest successful feature ID, and capture ordered final `iso`/`top` views.
- `OnshapeGodPartCapture` already separates modeling success from capture success and continues to the top capture after an iso capture failure.
- Tests use provider recordings for orchestration behavior and WireMock/golden payload fixtures for REST conformance; live calls are not a CI requirement.

### Important current constraints

- `skill_onshape_god_part` is intentionally one base sketch/profile, one extrude, then a fixed optional feature sequence. Its single `latestFeatureId` is not a general dependency graph.
- Sketch plane selection in `OnshapeSketchBuilder.BuildSketch` accepts only canonical `Top`, `Front`, or `Right` queries. Existing offset-plane creation does **not** make sketches target that returned plane feature, and sketch-on-face is not implemented.
- Extrude can map `new`, `add/join`, `cut/remove`, and `intersect`, but its template has one sketch-region query with `filterInnerLoops: true` and no explicit boolean-scope selector. Repository evidence does not establish robust multi-body scope behavior.
- Feature/topology references are different domains. Returned feature IDs are suitable for feature dependencies, patterns, and sketch-region references; fillet/chamfer/shell require opaque edge/face topology IDs.
- Topology inspection returns geometric facts and opaque native IDs, but no semantic selector or stable plan-local naming layer. IDs may change as the feature tree changes.
- Onshape `combine` is listed only as declarative-composite; there is no Onshape tool/template implementation. Multi-body workflows must not assume combine support.
- Capture supports only `iso` and `top`. Every checkpoint adds cloud reads and human image-review work.
- Writes are incremental cloud mutations with `rejectMicroversionSkew: true`; there is no transaction or rollback. Retrying a partially completed plan can duplicate features.

## 2. Architectural seam: fixed executor, not arbitrary interpreter

### Recommended shape

Add a bounded orchestration component behind a single `OnshapeSkillTools` entry point, while continuing to call `ICadWebProvider` and existing builders/templates. Keep parsing/validation, reference resolution, execution, and result projection as distinct responsibilities so they can be unit-tested without live Onshape.

The input should be a versioned discriminated schema, for example:

- top-level schema version and bounded ordered `stages` array;
- each stage has a unique plan-local `id` and one supported `kind`;
- sketch stages contain one supported geometry form and a plane reference;
- extrusion stages reference a prior sketch stage by plan-local ID and choose a bounded operation (`new`, `add`, or `remove` for the first slice);
- optional checkpoints reference completed stage positions and choose only supported views;
- dependencies may reference only earlier stages, making execution deterministic and avoiding a general graph scheduler.

Preflight should validate the **entire** plan before any write: exact fields, schema version, stage/count limits, unique IDs, backward-only references, finite dimensions, supported operation/geometry/plane combinations, and checkpoint limits. At runtime, resolve plan-local IDs to returned native feature IDs. Return one record per declared stage with statuses such as `completed`, `failed`, and `skipped_dependency`, plus native feature ID/result where available. Stop on the first modeling failure in v1; checkpoint failures should be reported separately from modeling state.

### Why this is not an interpreter

The executor should have no expressions, variables beyond typed ID references, loops, conditionals, branching, user-provided FeatureScript, arbitrary feature type names, raw REST bodies, or dynamic dispatch by reflection. New stage kinds require code and tests. Declared order plus backward references is preferable to arbitrary DAG topological sorting in the first slice: it is easier to validate, replay mentally, report, and review.

Avoid routing the new plan through repeated MCP atomic-tool calls. The in-process skill should reuse the builders/provider directly, preserving typed validation and one coherent status envelope. Also avoid expanding `ICadWebProvider` merely for orchestration; its generic `SketchReplace`/`FeatureAdd` seam is already sufficient for supported payloads.

## 3. Candidate first slices and non-goals

### Recommended first slice

A useful but reviewable v1 is:

1. One Part Studio address and a strict `schemaVersion`.
2. A bounded ordered plan (human confirmation needed on the cap; recommendation: at most 16 modeling stages and 3 checkpoints).
3. Multiple named sketch stages on **canonical planes only**.
4. A deliberately small closed-profile catalog: rectangle, circle, legacy closed straight-line profile, and the existing validated mixed line/arc closed profile. One profile per sketch stage.
5. Extrude stages referencing a prior sketch ID with `new`, `add`, or `remove`; positive/negative direction and finite nonzero depth.
6. Plan-local dependency IDs resolved to native feature IDs; references only to prior stages.
7. Full preflight before mutation, stop on first modeling failure, explicit completed/failed/skipped stage results, and the last successful feature ID.
8. Final `iso` and `top` capture using the established behavior, with optionally bounded explicit stage checkpoints if the product chooses them for v1.

This can demonstrate a base plate plus simple additive or removal extrusions on canonical planes. Depending on Onshape's existing extrude behavior, simple circles may represent holes. It does **not** establish robust pockets with multiple loops, arbitrary face-mounted ribs, curved bodies, or hollow horizontal bosses.

### Alternative smaller slice

If live confidence in `add/remove` or review capacity is low, ship only multiple canonical-plane named sketches plus `new` extrudes and status reporting, then add scoped additive/removal behavior after payload/live verification. This is architecturally clean but has less product value.

### Explicit non-goals for v1

- Arbitrary interpreter, FeatureScript, raw payload escape hatch, loops, conditionals, or general DAG scheduling.
- Sketches on generated faces, arbitrary topology IDs as sketch planes, or sketches on generated offset planes until the sketch-plane query builder supports them.
- Semantic face/edge selectors, inferred topology, or promises of stable topology identity.
- Fillet, chamfer, shell, pattern, mirror, sweep, or revolve plan stages merely because atomic primitives exist; they can be later typed stage kinds.
- Multi-loop/profile-region selection, explicit boolean scope, combine, or reliable multi-body merging.
- Automatic reconstruction of the motivating image/drawing. The base plate, pockets/holes, ribs/curved body, and hollow horizontal bosses are validation motivation, not a v1 acceptance bundle.
- Rollback, deletion of already-created features, idempotent resume/retry, concurrent plan execution, or reconciliation with manual edits.
- Image understanding or geometry inferred from an image.

## 4. Product decisions requiring human confirmation before proposal

1. **First-slice value boundary:** approve the recommended canonical-plane sketch + `new/add/remove` extrusion slice, or choose the smaller `new`-only slice.
2. **Plane scope:** confirm generated face and offset-plane targeting are deferred. Including either materially expands payload/query research and topology risk.
3. **Geometry catalog:** confirm whether rectangle/circle/closed line/mixed line-arc profiles are all needed in v1; fewer forms reduce schema and test volume.
4. **Boolean semantics:** decide whether `add/remove` may rely on Onshape default scope for a single resulting body, or must wait for explicit target-body/scope support. The latter is safer for multi-body plans.
5. **Failure policy:** confirm stop-on-first-modeling-failure with no rollback. Continuing independent branches would require a real dependency scheduler and a more complex review model.
6. **Retry contract:** decide whether v1 is explicitly non-idempotent and requires a fresh/known Part Studio, or whether resumability is mandatory. Resumability should be a separate change.
7. **Checkpoint policy:** final iso/top only (recommended default) versus explicit intermediate checkpoints; if intermediate captures are selected, confirm stage/count limits and whether capture failure stops modeling (recommendation: it does not).
8. **Plan and output limits:** approve bounds such as 16 modeling stages, 3 checkpoints, and backward-only references.
9. **Tool naming/migration:** decide whether this is a new clearly named tool while preserving `skill_onshape_god_part` unchanged (recommended), and settle the public name without extending the existing skill's schema.
10. **Motivating drawing acceptance:** provide a dimensioned declarative plan or select a much smaller demonstrator. A raster/image alone is insufficient as executable truth.

## 5. Risks and mitigations

| Risk | Consequence | First-slice mitigation |
| --- | --- | --- |
| Partial cloud mutation; no rollback | Failed plans leave persistent features; retry can duplicate work | Validate all static facts first, stop on first modeling failure, return native IDs and explicit stage states, document non-idempotency |
| Feature and topology identity | Later edits can invalidate opaque face/edge references | Use plan-local IDs only for feature dependencies in v1; defer topology-dependent stages/selectors |
| Multi-sketch plane/face targeting | Existing sketch builder handles only canonical planes | Restrict v1 to Top/Front/Right; treat generated-plane/face sketches as a separate capability |
| Add/cut target ambiguity | Default boolean scope may affect the wrong body or fail in multi-body plans | Restrict/document single-body assumptions or defer add/remove pending explicit scope proof |
| Inner-loop/profile ambiguity | Extrude template selects a sketch region with inner-loop filtering and no region selector | One profile per sketch stage; no multi-loop pocket claims |
| Image-plan ambiguity | Visual appearance does not define dimensions, feature order, constraints, or intended booleans | Require explicit declarative geometry; use images only as review evidence |
| Checkpoint/review workload | Many captures add API calls and force humans to inspect many images | Final iso/top by default; cap opt-in checkpoints and report them in stage order |
| Schema growth into an interpreter | Hard-to-secure, hard-to-test dynamic behavior and unstable contracts | Closed stage union, exact-field validation, schema versioning, no raw payloads |
| Microversion/concurrent edits | References can become stale between writes | Preserve existing skew rejection and stop/report on conflict; no concurrent executor in v1 |
| Large implementation/review surface | Parser, executor, tool result, builders, tests, WireMock, and docs can become one oversized change | Keep v1 to sketches/extrudes/status/captures and split implementation into small reviewable commits/tasks during later phases |

## 6. External research selection

**Optional external research: unselected.** Repository evidence is sufficient to propose the bounded executor architecture and the recommended canonical-plane first slice. External Onshape/OpenAPI or live-sandbox research becomes necessary before design/apply only if the proposal includes generated-face sketch queries, generated offset-plane sketch targeting, explicit boolean scope/body selection, multi-region sketches, or resumable mutation behavior. Deferring those capabilities keeps research unnecessary for the proposal.

## Key Learnings

- The reusable asset is not the fixed `skill_onshape_god_part` schema; it is its orchestration discipline plus `OnshapeSketchBuilder`, `OnshapeFeatureTemplates`, `ICadWebProvider`, and capture/status helpers.
- Plan-local IDs can safely abstract returned **feature IDs**, but they do not solve topology selection.
- Canonical-plane multi-sketch execution is a natural first seam; face/feature-plane targeting is a separate missing primitive.
- Add/remove support exists syntactically in the extrude template, but robust scope semantics are not proven by repository evidence.
- A bounded ordered executor protects correctness and review workload better than an arbitrary interpreter.

## Recommendation

Proceed to proposal only after the human confirms the first-slice boundary, especially boolean scope, plane scope, checkpoint policy, failure/retry semantics, and plan limits. Preserve `skill_onshape_god_part` unchanged and introduce a new fixed-schema orchestrator rather than generalizing the existing macro.
