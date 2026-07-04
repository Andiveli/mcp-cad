# 4R Review Summary — onshape-provider

**Date:** 2026-07-02  
**Branch:** `feature/onshape-provider`  
**Verdict:** **NOT merge-ready** — address CRITICAL findings before `sdd-archive` / PR.

## CRITICAL (merge-blocking)

| # | Lens | Issue |
| --- | ------ | -------- |
| R1 | Risk | `onshape_authenticate` does not update `OnshapeClient` signing keys; MCP can pass `secret_key` in tool args |
| R1 | Risk | Path segments (`documentId`, etc.) not encoded — path smuggling risk |
| R2 | Readability | `AssemblyManager` constructed but never exposed on `OnshapeProvider` / `ICadWebProvider` |
| R2 | Readability | Auth contract vs startup HMAC / `SecretKeyId` misaligned |
| R3 | Reliability | `create_sketch_Catches_CadProviderException` test does not throw `CadProviderException` |
| R3 | Reliability | `export_step` / `export_stl` same names Inventor+Onshape — collision not tested if both registered |
| R4 | Resilience | Export poll: transient HTTP failure aborts entire poll (no retry within 120s) |
| R4 | Resilience | 429 typed but no backoff/retry; Polly referenced, unused |

## Recommended fix order (before archive)

1. **Secrets:** Remove `secret_key` from MCP tools; wire User Secrets / env only; sync driver authenticate → client credentials (or drop runtime authenticate).
2. **Paths:** `Uri.EscapeDataString` (or equivalent) on all path segments in `OnshapeApiPaths`.
3. **Tests:** Fix mock to throw `CadProviderException`; add cross-provider tool collision test or rename Onshape export tools.
4. **Provider:** Wire or remove `AssemblyManager`; document stubs (`create_sketch` / `add_extrude`).
5. **Resilience (v1 minimum):** Retry transient GET on translation poll; optional 429 wait from `Retry-After` or remove Polly until used.

## Next SDD step

- **Do not** `sdd-archive` until CRITICALs resolved or explicitly accepted with risk sign-off.
- Re-run **4R** or targeted `review-risk` after fixes.
- Optional: **judgment-day** after fixes.

Full agent outputs: parallel subagent runs `review-risk`, `review-readability`, `review-reliability`, `review-resilience` (session 2026-07-02).

---

## Round 2 — after `sdd-apply` remediation (2026-07-02)

**Verdict:** **Much improved** — prior **8 CRITICAL themes** largely fixed; **1 new CRITICAL** + WARNINGs remain.

| Lens | Round 2 |
| ------ | ---------- |
| **R1 Risk** | Prior CRITICALs **resolved** (no MCP secrets, path encode, User Secrets, doc_delete confirm). WARNING: `ICadWebProvider.Authenticate` still on contract for in-process use. |
| **R2 Readability** | Assembly on provider **fixed**. WARNING: no MCP tools for assembly; auth params vs HMAC story still confusing. |
| **R3 Reliability** | **CRITICAL:** `ToolRegistrationCollisionTests` still expects `export_stl` collision — stale after `onshape_export_*` rename. WARNING: narrow mock error coverage. |
| **R4 Resilience** | Poll 5xx retry + Polly removed **OK**. WARNING: 429 during poll loop still aborts export; Retry-After date not handled. |

**Before archive:** fix stale collision test (or assert no collision + positive `onshape_export_*` names); run `dotnet test` locally; optional small 429-in-poll fix.
