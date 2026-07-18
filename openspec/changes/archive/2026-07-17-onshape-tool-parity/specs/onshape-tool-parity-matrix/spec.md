# Onshape Tool Parity Matrix Specification

## Purpose

Traceability artifact linking Inventor `AtomicTools` to Onshape MCP atomic tools and their underlying Onshape REST endpoints. The matrix enables gap analysis, migration planning, and verification that new `onshape_*` tools cover the P0–P2 modeling slice without scope creep.

## Requirements

### Requirement: Matrix Structure

The parity matrix MUST be published as a structured file (Markdown table or JSON) under `docs/` or adjacent to the tools code, listing every entry with columns: Inventor Atomic, Onshape MCP Tool, Onshape REST Endpoint, Status.

#### Scenario: P0 entries complete

- GIVEN the change ships P0 atomics (`onshape_sketch_rectangle`, `onshape_extrude`, `onshape_feature_list`)
- WHEN the matrix is queried for these entries
- THEN each MUST have status `implemented`, a matching Onshape MCP tool name, and the corresponding `POST /api/v6/partstudios/...` endpoint

#### Scenario: Every Inventor tool accounted for

- GIVEN `AtomicTools.cs` contains ~83 atomic tools
- WHEN the matrix is read
- THEN every Inventor tool MUST appear in exactly one row
- AND the row's Status MUST be one of `implemented`, `declarative-composite`, or `not_supported`
- AND v1 tools outside P0–P2 MUST NOT be marked `implemented`

### Requirement: Status Classification Rules

| Status | Meaning |
|--------|---------|
| `implemented` | A canonical `onshape_*` MCP tool exists AND works against the live Onshape API for the P0–P2 slice |
| `declarative-composite` | The operation is covered by a broader Onshape operation (e.g., multiple Inventor sketch atomics collapse into one BTMSketch replace) |
| `not_supported` | No Onshape REST equivalent exists in v1 scope (must include column `Reason` or `Notes`) |

#### Scenario: Classification consistency

- GIVEN an active entry in the matrix
- WHEN the status is checked
- THEN it MUST follow the classification rules above
- AND `not_supported` entries MUST carry a human-readable reason (e.g., "COM-only concept", "iProperties not in Onshape REST")

### Requirement: Refresh Trigger

The matrix MUST be regenerated or validated when:
- A new `onshape_*` atomic tool is added
- An existing tool's implementation status changes
- A new Inventor atomic is added to `AtomicTools.cs`

#### Scenario: Tool addition triggers refresh

- GIVEN a new `onshape_revolve` tool is implemented
- WHEN the change is proposed
- THEN the matrix MUST be updated to reflect `onshape_revolve` as `implemented`
- AND any related Inventor revolve tools MUST be linked
