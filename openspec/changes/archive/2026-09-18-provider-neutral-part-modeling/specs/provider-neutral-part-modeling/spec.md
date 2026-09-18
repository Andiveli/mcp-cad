# Onshape/Linux Part Modeling Specification

## Current delivery boundary

This specification defines the completed Onshape/Linux delivery only: typed portable geometry and an internal Onshape context for canonical-plane rectangle or circle sketches followed by new-body extrusion, while preserving all existing public CAD tool behavior. Automated evidence is offline/Linux-only: Unit 3 focused tests passed **8**, and the net8.0 regression passed **657** with **7 skipped** and **0 failed**. It does not certify live CAD.

Inventor-specific requirements formerly co-located in this specification are deferred to the explicitly named future change `inventor-windows-part-modeling`. They are not completed, discarded, or validated by this change. The historical text below is retained as provenance; this boundary supersedes any conflicting active-scope language.

## Purpose

Define an internal, typed portable part-modeling boundary that the Onshape adapter SHALL support for canonical-plane rectangle or circle sketches followed by new-body extrusion, while preserving all existing public CAD tool behavior.

## Requirements

### Requirement: Typed Portable Modeling Contract

The system MUST provide typed shared contracts for portable rectangle and circle sketch definitions, canonical plane selection, explicit units, extrusion depth and direction, capability declarations, operation outcomes/failures, and provider-scoped opaque references. The shared geometry contract MUST NOT require a cloud address, active COM document object, native feature ID, topology identity, revision value, or evidence payload.

#### Scenario: Portable request contains only portable geometry

- GIVEN a caller forms a portable modeling request
- WHEN the request defines a rectangle or circle sketch and a new-body extrusion
- THEN its geometry, plane, unit, depth, and direction MUST be represented by typed shared values
- AND any target and native feature reference MUST remain opaque and provider-scoped
- AND the request MUST NOT require a cloud-shaped document address or active COM object

#### Scenario: Cross-provider reference is rejected

- GIVEN a native reference returned during an Onshape execution
- WHEN it is supplied outside the execution that produced it
- THEN the request MUST be rejected before the corresponding mutation
- AND the reference MUST NOT be interpreted as a portable topology or feature identity

### Requirement: Required Onshape Modeling Baseline

The Onshape adapter MUST support the portable baseline: one closed rectangle or circle sketch on `Top`, `Front`, or `Right`, followed by a new-body extrusion that references that successful sketch. Capability declarations MUST NOT make this required baseline optional for the completed adapter. Inventor baseline work is deferred to `inventor-windows-part-modeling`.

#### Scenario: Onshape completes the portable baseline

- GIVEN an Onshape adapter with a valid adapter-owned target and a valid baseline request for each supported profile and canonical plane
- WHEN the adapter executes the request
- THEN it MUST create the requested closed sketch and its new-body extrusion
- AND the extrusion MUST bind to the native result of that request's sketch

### Requirement: Explicit Geometry Interpretation and Pre-Mutation Validation

The portable boundary MUST validate canonical plane, profile geometry, unit, extrusion depth, direction, sketch reference, and detectable invalid target before the corresponding modeling mutation. The Onshape adapter MUST map the requested canonical plane and orientation explicitly and MUST NOT silently substitute a native default plane, orientation, unit, depth, direction, or profile reference.

#### Scenario: Validated plane, unit, orientation, and reference are translated explicitly

- GIVEN a valid circle or rectangle request with an explicit unit, canonical plane, orientation-bearing direction, depth, and preceding sketch reference
- WHEN the Onshape adapter executes it
- THEN the native sketch and extrusion MUST reflect those requested meanings
- AND the extrusion MUST use the native result of the referenced successful sketch
- AND no provider default MAY replace an omitted or invalid required meaning

#### Scenario: Invalid or unresolved input prevents mutation

- GIVEN a request with an unsupported plane, invalid rectangle or circle, invalid or unsupported unit, invalid depth or direction, unresolved sketch reference, or detectable invalid target
- WHEN the Onshape adapter validates the request
- THEN it MUST report the validation failure before the corresponding mutation
- AND it MUST NOT perform that mutation

### Requirement: Capability-Gated Optional Evidence and Revision Metadata

Evidence/capture and revision metadata MUST be provider-specific optional capabilities. An adapter MUST explicitly report an unsupported optional capability and MUST NOT advertise it as available. A failure to capture optional evidence after successful geometry MUST be reported separately from the modeling result and MUST NOT change that geometry into a modeling failure. Revision metadata, when available, MUST remain opaque and MUST NOT imply cross-provider equivalence or a concurrency guarantee.

#### Scenario: Optional evidence failure follows successful geometry

- GIVEN an adapter that supports evidence capture and completes the baseline geometry
- WHEN its evidence capture fails
- THEN the geometry result MUST remain successful
- AND the evidence outcome MUST report its failure separately

#### Scenario: Optional capability is unavailable

- GIVEN an adapter that does not support evidence capture or revision metadata
- WHEN a consumer requests or inspects that optional capability
- THEN the adapter MUST explicitly report it as unsupported or unavailable
- AND it MUST NOT fabricate evidence, revision metadata, or concurrency semantics

### Requirement: Ordered Failure and Mutation Uncertainty Reporting

The system MUST stop subsequent modeling work after the first modeling failure. It MUST NOT automatically retry, resume, compensate, delete, or roll back mutations. Results MUST distinguish successful prior work, the failed or uncertain mutation, and later work that was not attempted; a failure after a provider call with uncertain mutation state MUST be reported as uncertain rather than as a confirmed non-mutation.

#### Scenario: Modeling failure leaves later work unattempted

- GIVEN an ordered portable request where an earlier sketch or extrusion succeeds and a later modeling operation fails
- WHEN execution reaches the failed operation
- THEN prior successful work MUST remain reported as successful
- AND all later modeling work MUST be reported as not attempted
- AND no retry, rollback, compensating delete, or further modeling call MUST occur

#### Scenario: Provider error leaves mutation state uncertain

- GIVEN a provider error after a modeling call where the adapter cannot determine whether the native mutation completed
- WHEN the adapter reports the result
- THEN it MUST report the affected mutation as uncertain
- AND it MUST stop later modeling work without retrying or rolling back

### Requirement: Existing Public Surface and Provider Selection Remain Unchanged

The change MUST be additive to existing public MCP tools, input schemas, output schemas, provider configuration selection, and provider-specific behavior. The server configuration MUST continue to select the provider without adding a public provider argument or universal target schema. The portable boundary MUST NOT narrow existing Onshape capabilities beyond the portable baseline.

#### Scenario: Existing public tool invocation remains unchanged

- GIVEN an existing public MCP tool invocation and configured provider selection that were valid before this change
- WHEN the portable modeling boundary is added
- THEN the tool name, input schema, output schema, selection mechanism, and behavior MUST remain unchanged
- AND the invocation MUST NOT require a provider argument or portable target value

#### Scenario: Provider-specific behavior remains available

- GIVEN a configured provider with behavior outside the portable baseline
- WHEN an existing provider-specific public tool uses that behavior
- THEN the behavior MUST remain available under its existing public contract
- AND the portable boundary MUST NOT claim that behavior is portable

## Explicit Bounds

This capability MUST NOT add a public provider-neutral MCP tool, SolidWorks support, a universal cloud-shaped target, automatic recovery, transactional guarantees, or an orchestrator, worker, verifier, scheduler, or autonomous planning loop.
