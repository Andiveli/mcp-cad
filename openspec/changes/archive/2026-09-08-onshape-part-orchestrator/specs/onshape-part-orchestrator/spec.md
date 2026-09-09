# Onshape Part Orchestrator Specification

## Purpose

The `onshape_orchestrate_part` tool accepts a bounded, versioned plan for creating an Onshape Part Studio model from typed sketch, extrude, and checkpoint stages. It validates the complete plan before mutation, executes it in declaration order, and reports modeling and visual-capture outcomes independently. The existing `skill_onshape_god_part` remains unchanged.

## Requirements

### Requirement: Exact Versioned MCP and Plan Contract

The public MCP binding MUST be exactly two serialized string arguments:

```text
onshape_orchestrate_part(address_json: string, plan_json: string)
```

`address_json` MUST be the existing serialized `CadElementAddress` contract with `documentId`, `workspaceOrVersionId`, required non-empty `elementId`, and optional `microversionId`. `plan_json` MUST be a JSON string whose root value is an object containing exactly `schemaVersion` and `stages`:

```json
{
  "schemaVersion": 1,
  "stages": "array of 1 through 16 stage objects"
}
```

The tool MUST NOT require or advertise a combined request object with `address_json` nested as an object. The plan MUST use integer `schemaVersion: 1`; unsupported, missing, or non-integer schema versions MUST be rejected. The stage union is closed to `sketch`, `extrude`, and `checkpoint`.

Every plan JSON object MUST reject unknown members, missing required members, duplicate members, and members with the wrong JSON type. The validator MUST NOT coerce strings, booleans, nulls, or arrays into another type. Numeric values MUST be finite JSON numbers; `NaN`, positive or negative infinity, and numeric strings MUST be rejected. Required strings MUST be non-empty.

#### Scenario: Supported request is accepted

- GIVEN a valid serialized `address_json` string and a serialized `plan_json` string with `schemaVersion` equal to `1` and one valid sketch stage
- WHEN `onshape_orchestrate_part` validates the request
- THEN the request MUST pass preflight
- AND execution MAY begin only after preflight completes

#### Scenario: Unsupported version or unknown field is rejected

- GIVEN a plan whose `schemaVersion` is `2`, or whose plan or stage contains an unrecognized member
- WHEN `onshape_orchestrate_part` validates the request
- THEN it MUST return a descriptive preflight validation error
- AND it MUST NOT issue a modeling write

#### Scenario: Duplicate or mistyped JSON member is rejected

- GIVEN a request containing a duplicate JSON member at any plan object level, or a numeric `depth_cm` supplied as a string
- WHEN the request is parsed or validated
- THEN it MUST be rejected as malformed
- AND no provider modeling operation or capture MUST be attempted

### Requirement: Bounded Ordered Stage List

The plan MUST contain at least one and no more than 16 stages. Checkpoint stages count toward the 16-stage total, and no plan MAY contain more than 3 checkpoint stages. The array order MUST be the execution order; the tool MUST NOT reorder stages or interpret the plan as an arbitrary graph.

#### Scenario: Stage and checkpoint limits are enforced during preflight

- GIVEN a plan with 17 stages, or a plan with 4 checkpoint stages
- WHEN the plan is validated
- THEN it MUST be rejected before the first modeling mutation
- AND the provider MUST observe zero modeling writes

#### Scenario: Declaration order is preserved

- GIVEN a valid plan containing sketch, extrude, checkpoint, and later extrude stages in that order
- WHEN execution succeeds
- THEN the provider operations MUST be attempted in the declared order
- AND the returned stage results MUST use that same order

### Requirement: Plan-Local IDs and Typed Backward References

Every stage MUST have a unique non-empty plan-local `id`. IDs identify stages only and MUST NOT be treated as native Onshape feature IDs or topology selectors. A reference MUST name an earlier stage in the same plan; self-references and forward references MUST be rejected during preflight.

An `extrude` stage MUST reference an earlier stage whose `kind` is `sketch` through `sketchId`. A `checkpoint` stage MUST reference a preceding modeling stage through `afterStageId`; its target MUST be a `sketch` or `extrude` stage, and it MUST be the latest preceding modeling stage in declaration order. References to checkpoints, older modeling stages when a later modeling stage already precedes the checkpoint, unknown IDs, and references in the wrong typed field MUST be rejected.

#### Scenario: Duplicate, forward, or incorrectly typed references are rejected

- GIVEN a plan with duplicate stage IDs, an extrude referring to a later stage, or an extrude whose `sketchId` names an extrude stage
- WHEN the complete plan is preflighted
- THEN it MUST be rejected with a reference validation error
- AND no sketch, feature, or capture write MUST occur

#### Scenario: Earlier sketch ID resolves to a native feature ID

- GIVEN a valid extrude whose `sketchId` names an earlier successful sketch stage
- WHEN that sketch returns native feature ID `sketch-native-1`
- THEN the extrude operation MUST use `sketch-native-1` as its native sketch reference
- AND the caller MUST NOT need to provide `sketch-native-1` in the input plan

### Requirement: Sketch Stage Schema and Supported Profiles

A sketch stage MUST contain exactly `id`, `kind: "sketch"`, `plane`, and `profile`. `plane` MUST be exactly one of `Top`, `Front`, or `Right`. A sketch MUST contain exactly one closed profile, and the profile type MUST be exactly one of `rectangle`, `circle`, `polygon`, or `mixed`.

The profile forms MUST reuse the existing builder and atomic-tool centimeter contracts and MUST have these exact shapes:

```json
{
  "type": "rectangle",
  "x1_cm": "finite number",
  "y1_cm": "finite number",
  "x2_cm": "finite number",
  "y2_cm": "finite number"
}
```

Rectangle coordinates are the two existing corner coordinates passed to `BuildRectangle`; their order is not significant because the builder normalizes minima and maxima. The coordinates MUST define nonzero width and height.

```json
{
  "type": "circle",
  "cx_cm": "finite number",
  "cy_cm": "finite number",
  "radius_cm": "finite number greater than 0"
}
```

Circle fields MUST use the existing `cx_cm`, `cy_cm`, and `radius_cm` semantics of `onshape_sketch_circle`. The orchestrator MUST convert these centimeter values once at the existing builder/provider boundary.

```json
{
  "type": "polygon",
  "points": [
    {"x_cm": "finite number", "y_cm": "finite number"}
  ]
}
```

Polygon points MUST remain in ordered `x_cm`/`y_cm` form and MUST be passed through the existing closed straight-line profile validation semantics. The list MUST contain 3 through 256 vertices after normalization, with no repeated vertices, nonzero area, non-crossing edges, and no invalid adjacent backtracking. The polygon is closed by connecting its final normalized point to its first point. For compatibility, one repeated final point equal to the first point MAY be supplied; the existing normalization MUST remove that one closing duplicate before validation. Additional duplicates remain invalid.

```json
{
  "type": "mixed",
  "start": {"x_cm": "finite number", "y_cm": "finite number"},
  "segments": [
    {"type": "line", "end": {"x_cm": "finite number", "y_cm": "finite number"}},
    {"type": "arc", "through": {"x_cm": "finite number", "y_cm": "finite number"}, "end": {"x_cm": "finite number", "y_cm": "finite number"}}
  ]
}
```

Mixed segments MUST use derived starts: the first segment starts at `start`, and each later segment starts at the preceding segment's `end`. Line segments MUST contain only `type` and `end`; arc segments MUST contain only `type`, `through`, and `end`. Per-segment `start` or `mid` members MUST NOT be accepted. A mixed profile MUST contain at least one line and one arc, from 2 through 256 segments, and from 1 through 8 arcs inclusive. Its final segment endpoint MUST equal `start`, and it MUST satisfy the existing validated mixed line/arc topology rules, including finite, non-degenerate, contiguous geometry and non-negligible closed area. Splines, multiple loops, open profiles, and unsupported geometry members MUST be rejected.

#### Scenario: Supported canonical-plane profiles are accepted

- GIVEN valid rectangle corner coordinates, a circle with `cx_cm`/`cy_cm`/`radius_cm`, a straight polygon with ordered `x_cm`/`y_cm` points, and a derived-start mixed line/arc profile on `Top`, `Front`, or `Right`
- WHEN each sketch stage is preflighted
- THEN each stage MUST pass profile validation
- AND execution MUST create one native sketch feature for each successful stage

#### Scenario: Supported polygon closing compatibility is accepted

- GIVEN a valid polygon whose final point repeats its first point exactly once
- WHEN the polygon profile is preflighted and built
- THEN the repeated final point MUST be normalized using the existing closed-profile behavior
- AND the polygon MUST remain eligible for execution when the normalized profile is valid

#### Scenario: Unsupported plane or invalid profile is rejected before mutation

- GIVEN a sketch on a generated face or offset plane, an open polygon, a zero-radius circle, or a mixed profile with 9 arcs
- WHEN the plan is preflighted
- THEN the plan MUST be rejected
- AND no cloud modeling write MUST occur

### Requirement: Extrude Stage Schema and Single-Body Semantics

An extrude stage MUST contain exactly `id`, `kind: "extrude"`, `sketchId`, `operation`, `depth_cm`, and `direction`. `operation` MUST be exactly one of `new`, `add`, or `remove`. `depth_cm` MUST be a finite, nonzero centimeter value. `direction` MUST be exactly one of `positive` or `negative`, matching the existing `onshape_extrude` argument contract. The stage MUST reference an earlier sketch stage, and the referenced sketch MUST contain one supported closed profile.

`add` and `remove` MUST be interpreted under the explicit first-slice assumption that the operation produces or addresses a single resulting solid body. The contract MUST NOT claim explicit multi-body target selection, combine semantics, or reliable behavior for multi-body boolean scope. Plans requiring those capabilities are outside this schema and MUST be rejected or deferred rather than guessed. The orchestrator MUST pass the resolved native sketch feature ID, `depth_cm`, `direction`, and operation to the existing typed extrude boundary, which converts centimeters to meters once.

#### Scenario: Base, add, and remove extrudes succeed

- GIVEN a valid plan with a base sketch and extrude `operation: "new"`, followed by earlier-sketch extrudes with `operation: "add"` and `operation: "remove"`
- WHEN the provider accepts each modeling operation under the single-body-result assumption
- THEN the three extrudes MUST execute in declaration order
- AND each successful extrude result MUST expose its returned native feature ID
- AND the result MUST identify the latest successful modeling feature ID

#### Scenario: Invalid extrude value or dependency is rejected

- GIVEN an extrude with `depth_cm` `0`, `NaN`, infinity, a numeric string, an unsupported operation, or a reference to a checkpoint
- WHEN the complete plan is preflighted
- THEN the plan MUST be rejected
- AND no modeling mutation MUST occur

### Requirement: Checkpoint Capture Stages

A checkpoint stage MUST contain exactly `id`, `kind: "checkpoint"`, `afterStageId`, and `view`. `view` MUST be exactly `iso` or `top`. The `afterStageId` MUST identify the latest preceding modeling stage in declaration order, not an arbitrary earlier modeling stage. A checkpoint MUST execute at its declaration position, after all preceding declared stages have been processed; it MUST NOT be delayed, reordered, or attached to a later stage. Multiple checkpoints MAY refer to the same latest preceding modeling stage when no newer modeling stage intervenes, but each still executes at its own declaration position.

A successful checkpoint MUST report its view and structured capture metadata in `captures.results` and MUST produce one native MCP image content block. Successful checkpoint image blocks MUST appear in declaration order in the returned `CallToolResult.Content`; failed checkpoints MUST produce no image block. The structured result remains in `CallToolResult.StructuredContent`, and capture metadata MUST remain in `captures.results` rather than being represented only by image content.

A capture failure MUST be represented as a capture failure for that checkpoint, MUST NOT be represented as a modeling-stage failure, and MUST NOT prevent subsequent modeling stages from being attempted.

#### Scenario: Checkpoint executes at its declaration position

- GIVEN a valid plan with a sketch, an `iso` checkpoint targeting that sketch, and a later extrude
- WHEN execution succeeds
- THEN the sketch MUST be processed before the checkpoint capture
- AND the checkpoint capture MUST be attempted before the later extrude
- AND the checkpoint MUST NOT be delayed until the end of the plan

#### Scenario: Checkpoint failure is nonfatal

- GIVEN a valid plan with a checkpoint between two modeling stages
- WHEN the checkpoint capture fails but the provider remains usable
- THEN the checkpoint result MUST report `captureStatus: "failed"`
- AND the surrounding modeling stages MUST retain independent modeling statuses
- AND the later modeling stage MUST still be attempted

#### Scenario: Unsupported checkpoint view or target is rejected

- GIVEN a checkpoint with view `front`, with `afterStageId` naming a future stage, with `afterStageId` naming a checkpoint, or with `afterStageId` naming an older modeling stage while a later modeling stage already precedes it
- WHEN preflight runs
- THEN the plan MUST be rejected
- AND no capture or modeling operation MUST be attempted

### Requirement: Complete Preflight Before Mutation

The orchestrator MUST complete validation of the entire request before any cloud mutation or capture attempt. Preflight MUST include address validity, parsing of the two serialized string arguments, exact plan object shape, schema version, stage and checkpoint limits, unique IDs, reference direction and types, supported planes, profile validity using the existing builder/tool shapes, finite centimeter values, supported extrude operations and direction, and checkpoint view and target validity.

A plan that passes preflight MUST not be rejected later for a statically knowable schema, bound, reference, or geometry error. Runtime provider errors remain possible after preflight and MUST be reported according to the execution requirements.

#### Scenario: Late malformed stage prevents an early write

- GIVEN a plan whose first five stages are valid and whose sixth stage has an unknown field or invalid profile
- WHEN the complete plan is submitted
- THEN preflight MUST fail before stage one is written
- AND the provider write count MUST remain zero

### Requirement: Sequential Failure Handling and Non-Idempotency

After successful preflight, modeling stages MUST execute sequentially in declaration order. If a sketch or extrude modeling operation fails, the orchestrator MUST stop attempting all later modeling stages. Every later modeling stage MUST be reported as skipped or unattempted, and MUST NOT be sent to the provider. A runtime modeling failure MUST NOT trigger rollback, compensating deletion, or automatic retry; successful earlier writes MUST remain in the Part Studio.

Checkpoint capture failure MUST NOT stop modeling, as specified separately. If modeling has already failed, later stages, including checkpoints, MUST be reported as not attempted rather than invoked.

The operation MUST be documented and reported as non-idempotent. Callers MUST use a fresh or otherwise known Part Studio and MUST NOT blindly retry a plan after partial completion. Existing microversion-skew rejection and cancellation/timeout error propagation MUST remain authoritative.

#### Scenario: Cloud modeling failure leaves a partial model

- GIVEN a valid plan with a successful base sketch and extrude, followed by an extrude that the provider rejects, and then another modeling stage
- WHEN execution reaches the rejected extrude
- THEN the failed stage MUST be reported with its modeling error
- AND the later modeling stage MUST be reported skipped or unattempted
- AND no deletion or rollback request MUST be issued
- AND the successful earlier native feature IDs MUST remain in the result

#### Scenario: Partial execution is not a safe retry

- GIVEN a plan that has already created one or more native features before a modeling failure
- WHEN a caller submits the same plan again to the same Part Studio
- THEN the orchestrator contract MUST make clear that the retry MAY duplicate features
- AND the caller MUST be responsible for selecting or restoring a fresh/known Part Studio

### Requirement: Ordered Result Envelope

For a request that passes preflight, the tool MUST return one MCP `CallToolResult`. Its `StructuredContent` MUST contain this exact JSON envelope:

```json
{
  "schemaVersion": 1,
  "status": "completed | completed_with_capture_failures | partial_modeling_failure",
  "modeling": {
    "status": "completed | failed",
    "lastSuccessfulFeatureId": "string or null",
    "failedStageId": "string or null",
    "error": {"code": "string", "message": "string"} or null
  },
  "stages": "ordered array of stage result objects",
  "captures": {
    "status": "none | completed | partial_failure",
    "results": "ordered array of checkpoint capture result objects"
  }
}
```

The result `schemaVersion` MUST be `1`. `stages` MUST contain one result for every declared stage in declaration order. Each stage result MUST contain exactly `sequence`, `id`, `kind`, `stageStatus`, `modelingStatus`, `captureStatus`, `nativeFeatureId`, and `error`, with the following values:

- `sequence` is the zero-based declaration index.
- `stageStatus` is `completed`, `failed`, or `skipped`.
- `modelingStatus` is `completed`, `failed`, `not_applicable`, or `not_attempted`.
- `captureStatus` is `completed`, `failed`, `not_applicable`, or `not_attempted`.
- `nativeFeatureId` is the returned native Onshape feature ID for a successful sketch or extrude, and `null` for checkpoints, failed stages, and skipped stages.
- `error` is a `{ "code": string, "message": string }` object only for a failed modeling stage, and is `null` for all other stage results.

Each `captures.results` entry MUST contain exactly `stageId`, `view`, `status`, `evidence`, and `error`. `status` MUST be `completed` or `failed`; `evidence` MUST contain the established structured capture metadata on success and MUST be `null` on failure; `error` MUST be `null` on success and a `{ "code": string, "message": string }` object on failure. Capture results MUST be ordered by checkpoint declaration order. `modeling.status` MUST describe only sketch and extrude outcomes; capture failures MUST NOT change it. `lastSuccessfulFeatureId` MUST be the native ID of the latest successful modeling stage, or `null` when no modeling stage succeeded.

For each successful checkpoint, `CallToolResult.Content` MUST contain one native `ImageContentBlock` for its PNG image, in checkpoint declaration order. `Content` MUST contain no image block for a failed or unattempted checkpoint. The image blocks are transport evidence; the corresponding view, status, provider metadata, and error state MUST remain in `StructuredContent.captures.results`. The structured envelope MUST remain available even when capture failures occur. A preflight rejection MUST return a descriptive validation error with modeling not started and MUST NOT be represented as a successful execution envelope.

#### Scenario: Successful result separates modeling and capture outcomes

- GIVEN a valid plan with successful base, add, and remove modeling stages and one successful `iso` checkpoint
- WHEN execution completes
- THEN the result status MUST be `completed`
- AND `modeling.status` MUST be `completed`
- AND `stages` and `captures.results` MUST preserve declaration order
- AND every successful modeling stage MUST expose its native feature ID
- AND the checkpoint MUST have `modelingStatus: "not_applicable"` and `captureStatus: "completed"`
- AND `CallToolResult.Content` MUST contain the checkpoint's native image block
- AND the checkpoint metadata MUST remain in `StructuredContent.captures.results`

#### Scenario: Capture failure does not become modeling failure

- GIVEN a plan whose modeling stages all succeed and whose `top` checkpoint capture fails
- WHEN the result is returned
- THEN `status` MUST be `completed_with_capture_failures`
- AND `modeling.status` MUST remain `completed`
- AND the checkpoint capture result MUST contain the capture error
- AND all successful modeling native feature IDs MUST be present
- AND `CallToolResult.Content` MUST contain no image block for the failed checkpoint

#### Scenario: Modeling failure reports later work as unattempted

- GIVEN a plan with a modeling failure at sequence 2 and additional modeling stages after it
- WHEN execution returns
- THEN `status` MUST be `partial_modeling_failure`
- AND `modeling.status` MUST be `failed`
- AND `failedStageId` and `error` MUST identify the first modeling failure
- AND later modeling stage results MUST be `skipped` with `modelingStatus: "not_attempted"`
- AND `lastSuccessfulFeatureId` MUST identify the latest successful modeling stage before the failure

### Requirement: Additive Compatibility With Existing God Macro

The new orchestrator MUST be additive. It MUST register the public tool name `onshape_orchestrate_part` with the two-string MCP signature without renaming, removing, widening, or changing the request, result, or execution behavior of `skill_onshape_god_part`. Existing callers of `skill_onshape_god_part` MUST continue to use its existing fixed workflow and schema unchanged.

The orchestrator MUST remain behind the established provider boundary and MUST preserve existing authentication, unit conversion, synchronous write, microversion-skew rejection, cancellation, timeout, and error-propagation behavior. It MUST NOT require raw REST payloads or repeated internal MCP calls as part of its public contract.

#### Scenario: God macro remains unchanged

- GIVEN an existing caller using `skill_onshape_god_part` with its current valid request
- WHEN the new tool is registered
- THEN the existing tool name and schema MUST remain available unchanged
- AND its fixed workflow and result behavior MUST remain unchanged
- AND the new orchestrator MUST be independently callable as `onshape_orchestrate_part`

## Explicit Bounds and Non-Goals

The first version is limited to one addressed Onshape Part Studio, one ordered plan, at most 16 total stages, at most 3 checkpoints, canonical `Top`/`Front`/`Right` sketch planes, one closed profile per sketch, and `new`/`add`/`remove` extrudes under the single-body-result assumption. It does not provide:

- arbitrary scripts, FeatureScript, raw REST payloads, reflection-based dispatch, variables, loops, conditionals, branching, or a general DAG scheduler;
- sketches on generated faces, arbitrary topology selectors, generated or offset-plane sketch targeting, or stable plan-local face/edge naming;
- multiple loops or selectable regions in one sketch, explicit multi-body boolean scope, combine, or multi-body-result workflows;
- fillet, chamfer, shell, pattern, mirror, sweep, revolve, or other unlisted feature stages;
- image-to-geometry inference or reconstruction of a drawing from visual evidence;
- rollback, compensating deletion, automatic retry, resumability, reconciliation, concurrent plan execution, or idempotent replay.

Images and checkpoint captures are verification evidence only; they MUST NOT be interpreted as executable dimensions or geometry.
