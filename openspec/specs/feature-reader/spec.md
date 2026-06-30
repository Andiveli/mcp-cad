# feature-reader Specification

## Purpose

Walks each feature node in the feature tree in creation order and emits typed JSON descriptors. Analogous to SketchReader for 3D features.

## Requirements

### Requirement: Feature Tree Traversal

The system MUST walk the feature tree in creation order and emit one descriptor per feature.

#### Scenario: Creational order preserved

- GIVEN a part with Extrude1, Fillet1, Hole1
- WHEN FeatureReader walks the tree
- THEN descriptors are emitted in Extrude1 → Fillet1 → Hole1 order

### Requirement: Typed Descriptors

Each descriptor MUST include feature_type + type-dependent params.

| Feature Type | Required Params |
|--------------|-----------------|
| Extrude | profile_index, distance, direction, operation, taper |
| Revolve | profile_index, axis, angle, operation |
| Fillet | edges, radius, mode |
| Chamfer | edges, distance, mode |
| Hole | x, y, diameter, depth, type |
| Thread | face, specification, direction |
| Shell | faces, thickness, direction |
| Draft | faces, angle, pull_direction, fixed_entity |
| CircularPattern | profile, axis, count, angle, parent_feature_index |
| RectangularPattern | profile, x_count, x_spacing, y_count, y_spacing |
| MirrorFeature | profile, mirror_plane |
| Sweep | profile, path, operation, taper |
| Loft | profiles, operation |
| Coil | profile, axis, pitch, revolutions |
| Rib | profile, thickness, direction |
| Split | split_tool, remove_side |
| Combine | base_body, tool_bodies, operation |
| Thicken | faces, thickness, direction |
| Emboss | profile, depth, type |
| Derive | source_path |

#### Scenario: Extrude includes full param set

- GIVEN Extrude1 feature with distance=5, direction=positive
- WHEN FeatureReader inspects it
- THEN descriptor includes type:"extrude", profile_index, distance, direction, operation, taper

#### Scenario: CircularPattern captures parent linkage

- GIVEN a CircularPattern over Extrude1
- WHEN descriptor is emitted
- THEN it includes parent_feature_index, axis, count, angle

### Requirement: Unsupported Feature Handling

The system MUST emit structured warnings for unsupported feature types without aborting capture.

#### Scenario: Warning on unsupported type

- GIVEN a part containing an iFeature
- WHEN FeatureReader encounters an unsupported type
- THEN a warning is emitted with feature name and type
- AND traversal continues to the next feature

### Requirement: Replay Resolution Data

The system SHOULD capture profile_index + centroid signature. The system MAY capture parent_feature_index for pattern linkage.

#### Scenario: Centroid captured with profile

- GIVEN a feature using a sketch profile
- WHEN descriptor is emitted
- THEN profile_index is included
- AND centroid coordinates MAY be present for mismatch detection
