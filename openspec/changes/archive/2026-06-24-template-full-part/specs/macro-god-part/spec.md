# Delta for macro-god-part

## ADDED Requirements

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
