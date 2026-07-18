# Onshape atomic tool parity matrix

This matrix records the MCP-facing parity position against the Inventor atomic catalog. `implemented` tools are registered by `OnshapeAtomicTools`; `declarative-composite` work can be expressed through the listed Onshape primitives but has no dedicated atomic wrapper; `not_supported` has no current Onshape MCP equivalent.

## Address and units contract

Every element-scoped Onshape tool takes `address_json`, a serialized `CadElementAddress` with `documentId`, `workspaceOrVersionId`, and `elementId`; `microversionId` is optional. MCP modeling arguments use **centimeters**. `OnshapeUnitConversion` changes cm to meters at the MCP-to-Onshape builder/template boundary, so clients must always send cm. Onshape REST identifiers and feature payloads remain Onshape-native.

## Implemented tools

| Inventor Atomic | Onshape MCP | REST endpoint | Status | Notes |
| --- | --- | --- | --- | --- |
| `sketch_rectangle` | `onshape_sketch_rectangle` | Part Studio features POST | implemented | P0; rectangle BTMSketch payload. |
| `sketch_profiles` | `onshape_feature_list` | Part Studio features GET | implemented | P0 feature discovery seam. |
| `extrude` | `onshape_extrude` | Part Studio features POST | implemented | P0 BT feature envelope. |
| `revolve` | `onshape_revolve` | Part Studio features POST | implemented | P1 profile sketch plus single-edge axis sketch. |
| `fillet` | `onshape_fillet` | Part Studio features POST | implemented | P1 equal-radius fillet. |
| `chamfer` | `onshape_chamfer` | Part Studio features POST | implemented | P1 equal-distance chamfer. |
| `asm_list_components` | `onshape_asm_list_instances` | Assembly definition GET | implemented | P2 assembly instance listing from `rootAssembly.instances`. |
| `asm_place_component` | `onshape_asm_create_instance` | Assembly instances POST | implemented | P2 part document and element references. |
| Assembly instance delete (Onshape-specific) | `onshape_asm_delete_instance` | Assembly instance node-id DELETE | implemented | P2 instance deletion; method verified against current OpenAPI. |
| `export_step` | `onshape_export_step` | Translation/export API | implemented | Existing polling implementation. |
| `export_stl` | `onshape_export_stl` | Translation/export API | implemented | Existing polling implementation. |

## Declarative-composite tools

| Inventor Atomic | Onshape MCP | REST endpoint | Status | Notes |
| --- | --- | --- | --- | --- |
| `sketch_create` | `onshape_sketch_rectangle` | Part Studio features POST | declarative-composite | Create a declarative rectangle sketch. |
| `sketch_line` | — | Part Studio features POST | declarative-composite | Builder support is not registered as an atomic. |
| `sketch_circle` | — | Part Studio features POST | declarative-composite | Builder support is not registered as an atomic. |
| `sketch_arc` | — | Part Studio features POST | declarative-composite | Compose from sketch feature payloads. |
| `sketch_dimension` | — | Part Studio features POST | declarative-composite | Add through a declarative sketch payload. |
| `sketch_point` | — | Part Studio features POST | declarative-composite | Add through a declarative sketch payload. |
| `sketch_spline` | — | Part Studio features POST | declarative-composite | Add through a declarative sketch payload. |
| `sketch_ellipse` | — | Part Studio features POST | declarative-composite | Add through a declarative sketch payload. |
| `sketch_circular_pattern` | — | Part Studio features POST | declarative-composite | Use a pattern feature payload. |
| `sketch_rectangular_pattern` | — | Part Studio features POST | declarative-composite | Use a pattern feature payload. |
| `sketch_offset` | — | Part Studio features POST | declarative-composite | Use a sketch feature payload. |
| `sketch_move` | — | Part Studio features POST | declarative-composite | Use a sketch feature payload. |
| `sketch_rotate` | — | Part Studio features POST | declarative-composite | Use a sketch feature payload. |
| `sketch_delete` | — | Part Studio features DELETE | declarative-composite | Address the sketch feature directly. |
| `sketch_constraint` | — | Part Studio features POST | declarative-composite | Constraint payload support is not exposed atomically. |
| `sketch_trim` | — | Part Studio features POST | declarative-composite | Compose the resulting sketch geometry. |
| `sketch_scale` | — | Part Studio features POST | declarative-composite | Compose the resulting sketch geometry. |
| `sketch_mirror` | — | Part Studio features POST | declarative-composite | Compose the resulting sketch geometry. |
| `sweep` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `circular_pattern` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `rectangular_pattern` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `mirror_feature` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `loft` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `coil` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `rib` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `emboss` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `shell` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `thicken` | — | Part Studio features POST | declarative-composite | Onshape feature API can express it. |
| `combine` | — | Part Studio features POST | declarative-composite | Onshape boolean feature payload. |
| `split` | — | Part Studio features POST | declarative-composite | Onshape split feature payload. |
| `draft` | — | Part Studio features POST | declarative-composite | Onshape draft feature payload. |
| `work_plane` | — | Part Studio features POST | declarative-composite | Onshape construction feature payload. |
| `work_axis` | — | Part Studio features POST | declarative-composite | Onshape construction feature payload. |
| `work_point` | — | Part Studio features POST | declarative-composite | Onshape construction feature payload. |
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
