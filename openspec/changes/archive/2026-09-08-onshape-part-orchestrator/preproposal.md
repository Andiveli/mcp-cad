# Pre-proposal decision state: Onshape Part Orchestrator

**Status:** product decisions confirmed
**Execution mode:** auto
**Research lane:** unselected
**Evidence:** `explore.md`

## Confirmed

- Preserve `skill_onshape_god_part` unchanged.
- Introduce a new fixed-schema orchestrator rather than an arbitrary interpreter.
- Use ordered typed stages with plan-local IDs and backward-only references.
- Images are verification evidence, not executable dimensional truth.
- Splines, raw FeatureScript/payloads, loops, conditionals, arbitrary DAG execution, semantic topology selectors, combine, and resumability are not implicit v1 capabilities.

## Confirmed first-slice decisions

- Extrusion operations: `new`, `add`, and `remove`, under an explicit single-body-result assumption.
- Sketch targets: canonical `Top`, `Front`, and `Right` planes only.
- Visual verification: up to 3 explicit intermediate checkpoints; capture failures are reported separately and do not stop modeling.
- Plan bound: maximum 16 total stages with backward-only references.
- Safety defaults: stop on first modeling failure, no rollback, and explicitly non-idempotent execution against a fresh or known Part Studio.
- Public tool: new `onshape_orchestrate_part`; `skill_onshape_god_part` remains unchanged.

## Pre-proposal readiness

External research remains unselected because repository evidence is sufficient for this bounded slice. Generated planes/faces, explicit multi-body boolean scope, multi-loop regions, combine, resumability, and image-to-geometry inference remain deferred.
