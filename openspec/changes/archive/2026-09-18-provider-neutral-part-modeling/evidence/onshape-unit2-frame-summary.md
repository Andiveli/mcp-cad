# Onshape Unit 2 frame evidence

## Decision

Release **Unit 2A only**: the internal, read-only Onshape frame reader and its parser tests. This evidence does not release the address-bound adapter, the rest of Unit 2, Inventor work, registration, or the full provider-neutral feature.

## Durable evidence record

| Item | Verified result |
| --- | --- |
| Provenance | Sanitized experiment receipt SHA-256 `6524c94c355440d5619bcb743053b3812cddf1edbeb9a3de600339d5a777a4d9`; harness baseline commit `0d8c214` |
| Experiment completion | 6 writes: three new sketches and three positive new-body extrudes; 2 read-only evaluations |
| Geometry | One asymmetric triangle per Top, Front, and Right, sent in centimeters; each positive extrusion was 4 mm |
| Frame agreement | Preflight and owner frames matched exactly: origin and x/y/normal deltas were zero for all three planes |
| Prism check | Six vertices per extruded asymmetric prism matched within `1e-7 m` |
| Evaluator response | API v17 observed `libraryVersion` 3070, response wrapper `BTFeatureScriptEvalResponse-1859`, zero notices, and no microversion skew |

The protocol-constant datum face IDs are Top `JDC`, Front `JCC`, and Right `JEC`. They are non-private builder constants, so they may appear in parser fixtures.

## Measured frames

| Datum | X | Y | Normal |
| --- | --- | --- | --- |
| Top | `(1,0,0)` | `(0,1,0)` | `(0,0,1)` |
| Front | `(1,0,0)` | `(0,0,1)` | `(0,-1,0)` |
| Right | `(0,1,0)` | `(0,0,1)` | `(1,0,0)` |

All measured origins were `(0,0,0)`. This confirms only the tested default datum frames, asymmetric triangles, positive new-body extrusion, and the reported depth; it does not establish arbitrary axes/transforms, negative direction, or Inventor behavior.

## Parser boundary for Unit 2A

The evaluator schema probes observed the v17/3070 root wrapper above and fully-qualified legacy value aliases:

- `com.belmonttech.serialize.fsvalue.BTFSValueArray`
- `com.belmonttech.serialize.fsvalue.BTFSValueNumber`
- `com.belmonttech.serialize.fsvalue.BTFSValueString`

Unit 2A must accept only the exact current and measured legacy aliases required by its frame payload, validate strict finite numeric values, and reject notices, microversion skew, malformed shapes, unsupported tags, and absent/invalid results before any consumer can write. The original mixed fixture omitted the legacy number alias; realistic fixtures and tests must cover that alias explicitly.

## Non-claims

The resume script was target-pinned experiment harness code, not a public tool. No credentials, raw responses, document/workspace/element IDs, or feature IDs are retained here. Independent verification/live evidence is separate from review: no native review approval exists because inspection stopped before lineage on blocked selection.

## Links

- [Research reconciliation](../research.md#current-runtime-evidence-reconciliation)
- [Released task slice](../tasks.md#released-unit-2a-readerparser-slice)
- [Implementation boundary](../design.md#released-unit-2a-readerparser-boundary)
- [Apply reconciliation](../apply-progress.md#unit-2a-evidence-reconciliation)
