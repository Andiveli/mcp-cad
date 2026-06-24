# macro-god-part Specification

## Purpose

Full-workflow single-call macro composing sketch → feature → pattern → modify → verify into one MCP call. Each phase is optional and independently try-caught.

## Requirements

### Requirement: Phase Composition

The system SHALL compose phases in fixed order: sketch → feature → pattern → modify → verify.

#### Scenario: Full pipeline executes all phases

- GIVEN sketch JSON with rect+circle entities, extrude feature params, fillet modify params
- WHEN macro_god_part is called
- THEN all five phases execute in order
- AND envelope.phase_status marks each phase success or null

#### Scenario: Zero-param call creates empty sketch

- GIVEN no params provided
- WHEN macro_god_part is called
- THEN only sketch phase runs on YZ plane
- AND geometry_created is false

### Requirement: Sketch Input via JSON

The system SHALL accept sketch_entities, constraints, and dimensions as optional JSON strings.

| Entity | Key Params |
|--------|------------|
| line | x1,y1,x2,y2,tag |
| circle | cx,cy,radius,tag |
| arc | cx,cy,radius,start_angle,end_angle |
| rect | x1,y1,x2,y2 |
| polygon | cx,cy,radius,sides |
| spline | points,fit_method |
| point | x,y |
| ellipse | cx,cy,major_radius,minor_radius |

#### Scenario: Valid JSON creates entities+constraints

- GIVEN sketch_entities with line+circle and constraints with coincident mode
- WHEN sketch phase runs
- THEN 2 entities and 1 constraint are created
- AND phase_status.sketch reflects counts

#### Scenario: Malformed JSON returns phase error

- GIVEN sketch_entities='{invalid}'
- WHEN JSON parsing fails
- THEN phase_status.sketch.success is false
- AND envelope.warnings contain parse error detail

### Requirement: 3D Features

The system SHALL support extrude, revolve, sweep, loft, coil, rib with operations new_body, cut, join, intersect.

#### Scenario: Extrude+cut removes material

- GIVEN a sketch with a closed profile
- WHEN feature_type="extrude" and operation="cut"
- THEN material is removed from existing body

#### Scenario: Coil creates helical body

- GIVEN circle profile + line axis
- WHEN feature_type="coil" with pitch=0.5 and revolutions=10
- THEN a helical body is created
- AND tree.feature_count >= 1

### Requirement: Pattern Support

The system SHALL support circular, rectangular, and mirror patterns for sketch and 3D features.

#### Scenario: Circular pattern on extruded feature

- GIVEN an extruded boss
- WHEN pattern_type="circular" with count=6 and axis="Y"
- THEN 6 instances are created

### Requirement: Modify Operations

The system SHALL support fillet, chamfer, shell, draft, thread, split, hole.

#### Scenario: Fillet on edges

- GIVEN an extruded body
- WHEN modify_type="fillet" with edges="1,2" and radius=0.5
- THEN specified edges are rounded
- AND modify phase succeeds

### Requirement: Ask-Before-Modify Guard

If ask_before_modify=true and document has >=1 feature, the system SHALL return a confirmation envelope.

#### Scenario: Guard returns confirmation on existing doc

- GIVEN document with 3 features and ask_before_modify=true
- WHEN macro_god_part is called
- THEN envelope.needs_confirmation is true
- AND current_state.feature_count equals 3
- AND no geometry is changed

#### Scenario: Guard bypassed when flag is false

- GIVEN existing document with ask_before_modify=false
- WHEN macro_god_part is called
- THEN phases execute normally
- AND geometry is modified

### Requirement: Partial Failure Reporting

Each phase SHALL be independently try-caught. Envelope SHALL report per-phase success/failure.

#### Scenario: Sketch succeeds, feature fails

- GIVEN valid sketch JSON but invalid feature params
- WHEN phases execute
- THEN phase_status.sketch.success is true
- AND phase_status.feature.success is false
- AND envelope.warnings contain feature error
- AND geometry_created is "partial"

### Requirement: Verification Envelope

The system SHALL return feature tree, bounding box, parameters, and viewport images.

#### Scenario: Verify data present after success

- GIVEN any successful macro_god_part call
- WHEN verify phase completes
- THEN envelope contains tree, bounding_box, parameters, viewport_images

### Requirement: Multi-Feature Replay via features[]

The system MUST accept a top-level features[] JSON array alongside the existing single-feature params. When present, the system MUST iterate features[] in order, dispatching each entry to the corresponding provider method. When absent or empty, the system MUST fall back to the current single-feature path.

Each features[] entry MUST include: feature_type + typed params + optional pattern_3d + optional modify_3d.

#### Scenario: features[] replays multiple features in order

- GIVEN features[] with entries for extrude, fillet, hole
- WHEN macro_god_part processes the array
- THEN features are created in Extrude → Fillet → Hole order
- AND phase_status reports per-entry success

#### Scenario: Empty features[] preserves backward compatibility

- GIVEN features=[] and feature_type="extrude"
- WHEN macro_god_part is called
- THEN the single-feature path executes
- AND result envelope is identical to pre-change output

#### Scenario: Entry with pattern produces patterned feature

- GIVEN a features[] entry with feature_type="extrude" and pattern_3d circular (count=6)
- WHEN the entry is processed
- THEN the extrude is created
- AND 6 instances are patterned around the axis

### Requirement: sketch_ref for Multi-Sketch Features

The system SHOULD accept sketch_ref in each features[] entry for multi-sketch feature linkage.

#### Scenario: Feature references sketch by index

- GIVEN a feature entry with sketch_ref=2
- WHEN macro_god_part processes the entry
- THEN the feature operates on sketch 2
- AND a warning is emitted if sketch_ref cannot be resolved
