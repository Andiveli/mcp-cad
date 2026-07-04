# Archive Report: dynamic-provider-routing

## Status

**ARCHIVED** — verification **PASS WITH WARNINGS** (2026-07-03). All implementation tasks **T1.1–T6.6** checked. **No merge to `main`** per user; work lives on chain branch **`feature/dpr-solidworks`**.

## Artifacts read

| Artifact | Path / topic |
| -------- | ------------- |
| Proposal | `proposal.md` |
| Design | `design.md` |
| Tasks | `tasks.md` (no unchecked `- [ ]`) |
| Verify | `verify-report.md` |
| Apply progress | `apply-progress.md` |
| Sync | `sync-report.md` |

## Outcome summary

- **Phase 1:** `UserConfigStore`, installer TUI, server config load, `mcp_cad_select_provider` skill/tool.
- **Phase 2:** `IProviderRouter`, lazy multi-provider DI, unified MCP tools (`AtomicTools`, `SkillTools`, `WebCadTools`).
- **Validated in use:** Pi MCP registration, `connect`/`health`, provider selection scope; Onshape API 401 / cube modeling = **out of scope** (credentials + incomplete web modeling).

## Canonical sync

See `sync-report.md`. Six new domains copied to `openspec/specs/`. Two deltas **not** merged into deprecated Python canonical specs.

## Recorded debt (from verify)

- Corrupt `config.json` → no degraded fallback to `appsettings`.
- SolidWorks registry stub only.
- `ExportPollingTests` pre-existing failure.
- Legacy `openspec/specs/cad-provider-protocol` and `mcp-tool-registration` still Python — use change archive + `src/McpCad.Core` for C#.

## Git / delivery

- **Branch:** `feature/dpr-solidworks` (not merged to `main`).
- **Push:** `origin feature/dpr-solidworks` after archive commit.

## Archived path

`openspec/changes/archive/2026-07-03-dynamic-provider-routing/`

## next_recommended

- Optional: fix W1 + refresh `server-config-loader` spec; merge chain PRs when ready.
- Separate change for Onshape document/sketch/extrude end-to-end.

---

*Archived: 2026-07-03*
