# Sync Report: onshape-provider

**Date:** 2026-07-03  
**Branch:** `feature/onshape-provider` (not `feature/dpr-solidworks`)

## Domains synced (new canonical)

| Domain | Action | Target |
| ------ | ------ | ------ |
| `web-cad-provider` | COPY (full) | `openspec/specs/web-cad-provider/spec.md` |
| `onshape-integration` | COPY (full) | `openspec/specs/onshape-integration/spec.md` |

## Domains not synced as canonical

| Domain | Reason |
| ------ | ------ |
| `provider-routing` | Describes **single-provider** `Provider:Active` + `RegisterCadProvider` (pre–dynamic-provider-routing). Superseded in runtime by `IProviderRouter` / `RegisterAllProviders`. Preserved only in archived change `specs/provider-routing/spec.md`. |

## Post-DPR drift (documented)

Runtime on `feature/dpr-solidworks` differs from this change’s Phase 4 tasks (unified tools, multi-provider host). **Connection and HMAC** behavior in `McpCad.Onshape` remains the basis for fixing **401** before a future **onshape modeling** SDD.

## Requirement operations

- No MODIFIED/REMOVED merges into existing canonical files (none overlapped).