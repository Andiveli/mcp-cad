# CAD providers (mcp-cad)

The MCP server runs **one** CAD backend per process. Selection is via `Provider:Active` in configuration (default: `inventor`).

## Inventor (desktop)

| Topic | Behavior |
| --- | --- |
| **Config** | `Provider:Active`: `inventor` (default). `Inventor:AutoConnect`: connect on startup. |
| **Auth** | Local COM to a running Autodesk Inventor instance (Windows). |
| **Units** | Centimeters in tool APIs (matches existing Inventor tools). |
| **Latency** | Interactive; operations run in-process against the open document. |
| **Tools** | `AtomicTools`, `SkillTools` (`sketch_*`, `extrude`, `inventor_*`, …). |

## Onshape (cloud)

| Topic | Behavior |
| --- | --- |
| **Config** | `Provider:Active`: `onshape`. `Onshape:BaseUrl` (default `https://cad.onshape.com`). |
| **Secrets** | `Onshape:AccessKey`, `Onshape:SecretKeyId`, `Onshape:SecretKey` — use **User Secrets** (`UserSecretsId`: `mcp-cad-server-onshape`), `dotnet user-secrets`, or environment variables (`ONSHAPE_ACCESS_KEY`, `ONSHAPE_SECRET_KEY_ID`, `ONSHAPE_SECRET_KEY`). Secret values are **not** accepted via MCP tool arguments. Do not commit keys. |
| **Auth** | HMAC-signed API requests (`OnshapeClient`). |
| **Units** | Tool APIs use **cm**; Onshape REST uses meters; conversion happens inside `McpCad.Onshape`. |
| **Latency** | Network round-trips; STEP/STL export **polls** translations (up to ~120s, blocking at the tool layer). |
| **Tools** | `OnshapeAtomicTools`, `OnshapeSkillTools` (`onshape_*`, `create_sketch`, `add_extrude`, `skill_sketch_and_extrude`, `skill_onshape_god_part`, `onshape_export_step`, `onshape_export_stl`, …). |
| **Inventor in Onshape mode** | `IMechanicalCadProvider` resolves to `NullMechanicalCadProvider` (no COM; mechanical tools return `success: false`). |

### Modeling

Onshape modeling tools accept an `address_json` value that serializes a `CadElementAddress`:

```json
{
  "documentId": "a2e3cbf0909c00279ce5d80b",
  "workspaceOrVersionId": "05605c0cafe63902d2a627a0",
  "elementId": "dce1127c54f4f7f7a620dc26"
}
```

`documentId`, `workspaceOrVersionId`, and `elementId` are required. `workspaceOrVersionId` holds either the workspace or version identifier; `microversionId` is optional. The modeling MCP boundary uses centimeters, matching the Inventor tool convention. `OnshapeUnitConversion` converts dimensions to meters immediately before Onshape REST builders and feature templates are called; callers must not pre-convert values.

The P0 workflow is: `onshape_sketch_rectangle` → `onshape_feature_list` → `onshape_extrude` → `onshape_export_step` (or `onshape_export_stl`). The listed sandbox document, workspace, and Part Studio are the reference values shown above; live calls still require configured Onshape credentials.

`skill_sketch_and_extrude` combines rectangle-sketch creation and extrusion for one Part Studio address. It accepts rectangle coordinates and depth in cm, links the extrusion to the returned sketch feature ID, and returns `success`, `sketch`, and `extrude` phase results. If sketch creation fails or returns no feature ID, it skips the extrusion.

`skill_onshape_god_part` creates a rectangle and extrude, then optionally applies fillet, chamfer, shell, circular or rectangular pattern, and mirror features. Its fixed `options_json` schema accepts `plane`, `direction`, `booleanOp`, and optional feature objects. Fillet/chamfer require `edgeIds` plus `radiusCm`/`distanceCm`; shell requires `faceIds` and `thicknessCm`; patterns require explicit axis/direction feature IDs; mirror accepts only Top, Front, or Right. It validates the complete request before the first write, rejects degenerate rectangles, uses each immediate response feature ID, defaults pattern/mirror sources to the latest successful feature, and returns the god-macro phase envelope in `CallToolResult.StructuredContent`. It does not infer topology IDs or roll back completed writes.

`onshape_inspect_edges` and `onshape_inspect_faces` issue one authenticated read-only `bodydetails` GET per call. They accept the same address plus an optional exact, case-sensitive `body_id`, return opaque native topology IDs, and normalize observed meter and square-meter values to cm and cm2. An unknown body ID is an error; it never falls back to all bodies.

`onshape_capture_view` reads the v16 Part Studio shaded-views endpoint for an `iso` or `top` view. It defaults to 800×600, accepts dimensions from 1 through 4096, and normalizes supported view and edge casing. It traverses all shaded-view image cells and returns the first valid PNG without writing a local file. The MCP result contains an `ImageContentBlock` in `CallToolResult.Content`; `StructuredContent` retains `image_base64` and `mime_type: image/png`. `iso` is this project's canonical right-handed isometric orientation, not a claim about an Onshape named view.

See [the Onshape parity matrix](onshape-parity-matrix.md) for the full implemented and declarative-composite tool catalog, including P1 feature and P2 assembly tools.

## Routing rules

- Unknown `Provider:Active` → startup fails with `ConfigurationException` (no silent fallback).
- Only the active provider’s tool types are registered on the MCP server.
- Duplicate MCP tool names across tool classes → `ToolRegistrationException` at startup (validated before `Run`). Onshape exports use `onshape_export_step` / `onshape_export_stl` to avoid colliding with Inventor `export_step` / `export_stl` if both sets were ever registered together.

## Switching providers

1. Set `Provider:Active` to `inventor` or `onshape`.
2. Provide the matching credentials / Inventor session.
3. Restart the MCP server.

No database migration; rollback is configuration-only.
