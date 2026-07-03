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
| **Tools** | `OnshapeAtomicTools`, `OnshapeSkillTools` (`onshape_*`, `create_sketch`, `add_extrude`, `onshape_export_step`, `onshape_export_stl`, …). |
| **Inventor in Onshape mode** | `IMechanicalCadProvider` resolves to `NullMechanicalCadProvider` (no COM; mechanical tools return `success: false`). |

## Routing rules

- Unknown `Provider:Active` → startup fails with `ConfigurationException` (no silent fallback).
- Only the active provider’s tool types are registered on the MCP server.
- Duplicate MCP tool names across tool classes → `ToolRegistrationException` at startup (validated before `Run`). Onshape exports use `onshape_export_step` / `onshape_export_stl` to avoid colliding with Inventor `export_step` / `export_stl` if both sets were ever registered together.

## Switching providers

1. Set `Provider:Active` to `inventor` or `onshape`.
2. Provide the matching credentials / Inventor session.
3. Restart the MCP server.

No database migration; rollback is configuration-only.
