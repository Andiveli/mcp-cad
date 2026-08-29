# Onshape atomic tool parity matrix

This matrix records the MCP-facing parity position against the Inventor atomic catalog. `implemented` tools are registered by `OnshapeAtomicTools`; `declarative-composite` work can be expressed through the listed Onshape primitives but has no dedicated atomic wrapper; `not_supported` has no current Onshape MCP equivalent.

## Address and units contract

Every element-scoped Onshape tool takes `address_json`, a serialized `CadElementAddress` with `documentId`, `workspaceOrVersionId`, and `elementId`; `microversionId` is optional. MCP modeling arguments use **centimeters**. `OnshapeUnitConversion` changes cm to meters at the MCP-to-Onshape builder/template boundary, so clients must always send cm. Onshape REST identifiers and feature payloads remain Onshape-native.

## Implemented tools

| Inventor Atomic | Onshape MCP | REST endpoint | Status | Notes |
| --- | --- | --- | --- | --- |
| `sketch_rectangle` | `onshape_sketch_rectangle` | Part Studio features POST | implemented | P0; rectangle BTMSketch payload. |
| `sketch_point` | `onshape_sketch_point` | Part Studio features POST | implemented | Dedicated BTMSketch point entity; live-verified with `featureStatus: OK`. |
| `sketch_profiles` | `onshape_feature_list` | Part Studio features GET | implemented | P0 feature discovery seam. |
| `extrude` | `onshape_extrude` | Part Studio features POST | implemented | P0 BT feature envelope. |
| `revolve` | `onshape_revolve` | Part Studio features POST | implemented | P1 profile sketch plus single-edge axis sketch. |
| `sweep` | `onshape_sweep` | Part Studio features POST | implemented | P1 SOLID profile-plus-path sweep; live-verified with `featureStatus: OK`. |
| `fillet` | `onshape_fillet` | Part Studio features POST | implemented | P1 equal-radius fillet. |
| `chamfer` | `onshape_chamfer` | Part Studio features POST | implemented | P1 equal-distance chamfer. |
| `shell` | `onshape_shell` | Part Studio features POST | implemented | P1 selected-face shell; live-proven flat `btType` envelope with `featureStatus: OK`. |
| `asm_list_components` | `onshape_asm_list_instances` | Assembly definition GET | implemented | P2 assembly instance listing from `rootAssembly.instances`. |
| `asm_place_component` | `onshape_asm_create_instance` | Assembly instances POST | implemented | P2 part document and element references. |
| Assembly instance delete (Onshape-specific) | `onshape_asm_delete_instance` | Assembly instance node-id DELETE | implemented | P2 instance deletion; method verified against current OpenAPI. |
| `export_step` | `onshape_export_step` | Translation/export API | implemented | Existing polling implementation. |
| `export_stl` | `onshape_export_stl` | Translation/export API | implemented | Existing polling implementation. |

## Declarative-composite tools

| Inventor Atomic | Onshape MCP | REST endpoint | Status | Notes |
| --- | --- | --- | --- | --- |
| `sketch_create` | `onshape_sketch_rectangle` | Part Studio features POST | declarative-composite | Create a declarative rectangle sketch. |
| `sketch_line` | `onshape_sketch_line` | Part Studio features POST | implemented | Dedicated BTMSketch line with optional visible LENGTH dimension; live-verified with solver status `OK`. |
| `sketch_circle` | `onshape_sketch_circle` | Part Studio features POST | implemented | Dedicated BTMSketch circle with optional visible DIAMETER dimension; live-verified with solver status `OK`. |
| `sketch_arc` | `onshape_sketch_arc` | Part Studio features POST | implemented | Dedicated BTMSketch arc payload with endpoint topology. |
| `sketch_polyline` | `onshape_sketch_polyline` | Part Studio features POST | implemented | Axis-aligned multi-line BTMSketch with explicit coincident, horizontal, and vertical constraints; live-verified with `featureStatus: OK`. |
| `sketch_dimension` | — | Part Studio features POST | declarative-composite | Add through a declarative sketch payload. |
| `sketch_spline` | `onshape_sketch_spline` | Part Studio features POST | implemented | Non-periodic interpolated BTMSketch spline with endpoint handles; live-verified with `featureStatus: OK`. |
| `sketch_rectangular_pattern` (one-row foundation) | `onshape_sketch_linear_pattern_circle` | Part Studio features POST | implemented | One-row linear circle pattern (`patternc2=1`) live-verified with `featureStatus: OK`; full 2D parity remains separate. |
| `sketch_ellipse` | `onshape_sketch_ellipse` | Part Studio features POST | implemented | Rotated ellipse with optional visible major/minor diameter dimensions; live-verified with solver status `OK`. |
| `sketch_circular_pattern` | `onshape_sketch_circular_pattern_line` | Part Studio features POST | implemented | Closed circular line pattern with construction pivot circle; live-verified with `featureStatus: OK`. |
| `sketch_rectangular_pattern` (full 2D) | — | Part Studio features POST | declarative-composite | Full two-dimensional rectangular-pattern parity remains unwrapped. |
| `sketch_offset` | — | Part Studio features POST | declarative-composite | Use a sketch feature payload. |
| `sketch_move` | — | Part Studio features POST | declarative-composite | Use a sketch feature payload. |
| `sketch_rotate` | — | Part Studio features POST | declarative-composite | Use a sketch feature payload. |
| `sketch_delete` | — | Part Studio features DELETE | declarative-composite | Address the sketch feature directly. |
| `sketch_constraint` | — | Part Studio features POST | declarative-composite | Constraint payload support is not exposed atomically. |
| `sketch_trim` | — | Part Studio features POST | declarative-composite | Compose the resulting sketch geometry. |
| `sketch_scale` | — | Part Studio features POST | declarative-composite | Compose the resulting sketch geometry. |
| `sketch_mirror` | — | Part Studio features POST | declarative-composite | Compose the resulting sketch geometry. |
| `circular_pattern` | `onshape_circular_pattern` | Part Studio features POST | implemented | Feature-level `circularPattern` for a source feature and axis sketch edge; live-proven with `featureStatus: OK` in disposable document `243b7f4c501d53eaccaea926`. |
| `rectangular_pattern` | `onshape_rectangular_pattern` | Part Studio features POST | implemented | Feature-level `linearPattern` with one direction by default and an optional second sketch-edge direction; live-proven two-direction pattern returned `featureStatus: OK`. |
| `mirror_feature` | `onshape_mirror_feature` | Part Studio features POST | implemented | Feature-level `mirror` for a source feature across a canonical Top, Front, or Right plane; live-proven with `featureStatus: OK`. |
| `loft` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `coil` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `rib` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `emboss` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `thicken` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `combine` | — | Part Studio features POST | declarative-composite | Onshape boolean feature payload. |
| `split` | — | Part Studio features POST | declarative-composite | Onshape split feature payload. |
| `draft` | — | Part Studio features POST | declarative-composite | Onshape draft feature payload. |
| `work_plane` | `onshape_work_plane` | Part Studio features POST | implemented | Canonical Top, Front, or Right offset plane only; nonnegative cm offset, live-proven `cPlane` OFFSET payload with `featureStatus: OK`. Advanced plane modes remain out of scope. |
| `work_axis` | `onshape_work_axis` | Part Studio features POST | implemented | Sketch-backed construction axis only: one centered local `x` (default) or `y` line on canonical Top, Front, or Right, with `disableImprinting`; use the resulting sketch edge for revolve or pattern axes. This is not native Onshape WorkAxis parity. Live-verified Front/+X with `featureStatus: OK` in `FI9fyDtF3wxmK6E_1`. |
| `work_point` | `onshape_work_point` | Part Studio features POST | implemented | Native `cPoint` edge point only: supply a topology-dependent Onshape edge ID and normalized parameter in [0,1] (default 0.5). Arbitrary XYZ points are not supported. Live-verified edge `JPV` at 0.5 returned `featureStatus: OK`. |
| `asm_list_constraints` | — | Assembly definition GET | declarative-composite | Available from assembly definition data. |
| `asm_ground_component` | — | Assembly instances POST | declarative-composite | Requires an assembly instance payload. |
| `asm_replace_component` | — | Assembly instances POST | declarative-composite | Replace through an instance update payload. |
| `asm_constraint_mate` | — | Assembly features POST | declarative-composite | Constraint feature payload not wrapped. |
| `asm_constraint_flush` | — | Assembly features POST | declarative-composite | Constraint feature payload not wrapped. |
| `asm_constraint_angle` | — | Assembly features POST | declarative-composite | Constraint feature payload not wrapped. |
| `asm_constraint_insert` | — | Assembly features POST | declarative-composite | Constraint feature payload not wrapped. |

## Not supported

| Inventor Atomic | Onshape MCP | REST endpoint | Status | Notes |
| --- | --- | --- | --- | --- |
| `derive` | — | — | not_supported | File-path based Inventor derive has no cloud equivalent. |
| `thread` | — | — | not_supported | No dedicated atomic template. |
| `hole` | — | Part Studio features POST | not_supported | Native V3 location queries resolved live, but every tested payload regenerated with `featureStatus: ERROR`; the unproven MCP tool was removed. |
| `inspect_edges` | — | — | not_supported | No inspection atomic. |
| `param_list` | — | — | not_supported | Post-creation parameter catalog is out of scope. |
| `param_get` | — | — | not_supported | Post-creation parameter access is out of scope. |
| `param_set` | — | — | not_supported | Post-creation parameter editing is out of scope. |
| `param_set_expression` | — | — | not_supported | Expression editing is out of scope. |
| `iproperty_get` | — | — | not_supported | Inventor document-property model differs. |
| `iproperty_set` | — | — | not_supported | Inventor document-property model differs. |
| `iproperty_summary` | — | — | not_supported | Inventor document-property model differs. |
| `iproperty_custom_get` | — | — | not_supported | Inventor document-property model differs. |
| `iproperty_custom_set` | — | — | not_supported | Inventor document-property model differs. |
| `export_pdf` | — | — | not_supported | Drawing export is not part of the Onshape atomic catalog. |
| `export_dxf` | — | — | not_supported | Drawing export is not part of the Onshape atomic catalog. |
