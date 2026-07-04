# Sync Report: dynamic-provider-routing

**Date:** 2026-07-03  
**Mode:** Archive-time sync (parent-approved; no prior `sdd-sync`)

## Domains synced (new canonical)

| Domain | Action | Target |
| ------ | ------ | ------ |
| `user-config-store` | COPY (full) | `openspec/specs/user-config-store/spec.md` |
| `installer-wizard` | COPY (full) | `openspec/specs/installer-wizard/spec.md` |
| `server-config-loader` | COPY (full) | `openspec/specs/server-config-loader/spec.md` |
| `select-provider-skill` | COPY (full) | `openspec/specs/select-provider-skill/spec.md` |
| `provider-router` | COPY (full) | `openspec/specs/provider-router/spec.md` |
| `lazy-provider-registry` | COPY (full) | `openspec/specs/lazy-provider-registry/spec.md` |

## Domains not merged into legacy canonical

| Domain | Reason |
| ------ | ------ |
| `cad-provider-protocol` | Existing `openspec/specs/cad-provider-protocol/spec.md` is **Python-era** (`DEPRECATED.md`). Delta not merged destructively; C# truth remains in archived change `specs/cad-provider-protocol/spec.md`. |
| `mcp-tool-registration` | Same — legacy Python canonical; delta preserved in archive only. |

## Requirement operations

- **ADDED/MODIFIED/REMOVED** in `cad-provider-protocol` and `mcp-tool-registration`: recorded in archive-report; not applied to obsolete canonical files.

## Warnings

- Active same-domain changes: none detected under `openspec/changes/*/specs/` for synced domains.
