# Part Studio Modeling Specification

## Purpose

Part Studio modeling operations for Onshape: BTMSketch replace, feature add with BT feature envelope, microversion-safe write sequencing, and feature discovery. This capability enables the P0 E2E loop: rectangle sketch → extrude → STEP export.

## Requirements

### Requirement: BTMSketch Replace Payload

`SketchReplace` MUST POST a valid BTMSketch JSON body to the Onshape Part Studio sketch endpoint. The body MUST wrap sketch entities (lines, circles, rectangles) in the BTMSketch schema — not a flat `featureType`/`parameters` structure. The response microversion MUST update the driver cache.

#### Scenario: Rectangle sketch on empty Part Studio

- GIVEN a `CadElementAddress` targeting a Part Studio element with an empty sketch
- WHEN `SketchReplace` is called with a rectangle BTMSketch body (four line segments forming a closed rectangle)
- THEN the POST body MUST conform to Onshape BTMSketch JSON shape
- AND the response MUST return success with an updated microversion
- AND the driver `LastMicroversion` MUST reflect the response `microversionSkew`

#### Scenario: Line sketch

- GIVEN a `CadElementAddress` targeting a Part Studio element
- WHEN `SketchReplace` is called with a line BTMSketch body (single line segment)
- THEN the POST body MUST conform to Onshape BTMSketch JSON shape
- AND the response MUST return success

### Requirement: BT Feature Envelope for FeatureAdd

`FeatureAdd` MUST POST a wrapped BT `feature` envelope body — NOT the flat `{ featureType, parameters }` structure. The envelope MUST contain a `feature` object with `featureType`, `parameters`, and optional `suppressed`/other BT fields. `rejectMicroversionSkew=true` MUST be included in the envelope.

#### Scenario: Extrude from sketch

- GIVEN a Part Studio with a completed sketch
- WHEN `FeatureAdd` is called with `"extrude"` featureType and parameters (depth, direction, boolean operation)
- THEN the POST body MUST be `{ "feature": { "featureType": "extrude", "parameters": [...] }, "rejectMicroversionSkew": true }`
- AND the response MUST return a feature ID and updated microversion
- AND the driver `LastMicroversion` MUST reflect the response

#### Scenario: Flat payload rejection (regression guard)

- GIVEN the current `FeatureManager.FeatureAdd` implementation serializes `{ featureType, parameters }` flat
- WHEN such a body is sent to the Onshape API
- THEN Onshape MUST reject it (400-level error)
- AND the implementation MUST instead send the wrapped BT envelope
- AND WireMock tests MUST verify the correct envelope shape

### Requirement: Feature Discovery

The system MUST list features for a Part Studio element via `FeatureManager.FeatureList`. The response MUST include feature IDs, types, and parameters for every feature in the element's feature tree.

#### Scenario: List features after extrude

- GIVEN a Part Studio with a sketch and an extrusion
- WHEN `FeatureList` is called
- THEN the response MUST contain a `features` array with entries for both sketch and extrude
- AND each entry MUST carry `featureId`, `featureType`, and `parameters`

#### Scenario: Empty Part Studio

- GIVEN a Part Studio with no features
- WHEN `FeatureList` is called
- THEN the response MUST contain an empty `features` array

### Requirement: Modeling Microversion Discipline

Every modeling write (`SketchReplace`, `FeatureAdd`) MUST include `rejectMicroversionSkew=true` in the POST body or query. On success, the response microversion MUST update the driver's `LastMicroversion` for subsequent reads and writes.

#### Scenario: Sketch then extrude sequence

- GIVEN a Part Studio with microversion `mv1`
- WHEN `SketchReplace` succeeds returning microversion `mv2`
- THEN `FeatureAdd` on the same element MUST use `mv2` for skew protection
- AND the `FeatureAdd` POST MUST include `"rejectMicroversionSkew": true`
