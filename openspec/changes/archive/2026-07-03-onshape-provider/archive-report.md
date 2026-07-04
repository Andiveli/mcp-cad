# Archive Report: onshape-provider

## Status

**ARCHIVED** — verification **PASS with WARNINGs** (2026-07-02). All **41/41** tasks checked. Archived on **`feature/onshape-provider`** only; **no merge to `main`**, **not** on `feature/dpr-solidworks`.

## Artifacts read

- `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md`, `verify-report.md`, `judgment-day-report.md`, `sync-report.md`

## Outcome summary

- **`ICadWebProvider`**, Onshape HMAC client, managers, export polling, WireMock/conformance tests.
- **Next user priority:** make **Onshape connect** (valid API keys in `~/.config/mcp-cad/config.json`, MCP reconnect) — not blocked by this archive.
- **Future SDD:** parametric modeling (real sketch, doc create, cube E2E) — separate change.

## Canonical sync

See `sync-report.md` — `web-cad-provider`, `onshape-integration` copied to `openspec/specs/`.

## Recorded debt

- Build/test not run in original verify environment (`dotnet` PATH).
- `provider-routing` spec obsolete after DPR.
- `PollTranslationAsync_Transient500ThenDone_Succeeds` flaky/fail noted on DPR verify.
- Stubs: `sketch_replace`, limited `doc_new_*` on web provider.

## Archived path

`openspec/changes/archive/2026-07-03-onshape-provider/`

## next_recommended

1. Fix Onshape **401** (credentials + `secret_key_id` + reconnect MCP).
2. Open new SDD for **onshape modeling** when connection is green.

---

*Archived: 2026-07-03*