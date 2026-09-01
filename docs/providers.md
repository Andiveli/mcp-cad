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
| **Tools** | `OnshapeAtomicTools`, `OnshapeSkillTools` (`onshape_*`, `create_sketch`, `add_extrude`, `skill_sketch_and_extrude`, `onshape_export_step`, `onshape_export_stl`, …). |
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

`onshape_inspect_edges` and `onshape_inspect_faces` issue one authenticated read-only `bodydetails` GET per call. They accept the same address plus an optional exact, case-sensitive `body_id`, return opaque native topology IDs, and normalize observed meter and square-meter values to cm and cm2. An unknown body ID is an error; it never falls back to all bodies.

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
