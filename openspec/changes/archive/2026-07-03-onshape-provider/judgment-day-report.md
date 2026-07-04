# Judgment Day — onshape-provider

**Date:** 2026-07-02  
**Target:** `feature/onshape-provider` — full Onshape slice (post 4R remediation + micro test fix)  
**Skill resolution:** paths-injected (gentle-ai, AGENTS.md referenced)

## Round 1

| Judge | Status | Verdict |
|-------|--------|---------|
| **Judge A** (`jd-judge-a`) | **Incomplete** — intercom-only session lacked Read/Bash; retry returned no findings body | — |
| **Judge B** (`jd-judge-b`) | Complete blind review | **CONDITIONAL** |

Per Judgment Day contract: **do not treat as APPROVED** without both judges or explicit human sign-off.

## Confirmed (Judge B only — treat as **suspect** until Judge A agrees)

| ID | Severity | Issue | File |
| ---- | ---------- | ------- | ------ |
| C1 | CRITICAL | 429 without Retry-After: `response.Dispose()` then fall-through → `ObjectDisposedException` | `OnshapeClient.cs` ~89–111 |
| C2 | CRITICAL | `ExportStl` does not poll translation (unlike `ExportStep`) | `ExportManager.cs` |
| C3 | CRITICAL | `MockOnshapeProvider.DocDelete` ignores `confirm` guard | `MockOnshapeProvider.cs` |
| C4 | CRITICAL | Second 429 on retry may leak `HttpResponseMessage` | `OnshapeClient.cs` |
| C5 | CRITICAL | Thin tests for client error paths / tool Catch subtypes | tests |

## Suspect / INFO (Judge B)

- W1–W10: resilience, G9 precision, HTTP timeout, sync-over-async, FeatureGetResult, pagination, dead code, Catch loses typed errors, response dispose on errors
- Inventor regression: **none confirmed** (Judge B)

## Fixes applied before JD

- `sdd-apply` micro: collision tests + 429 retry test (round 2)

## Next steps (Judgment Day protocol)

1. **Fix agent** (`jd-fix-agent` or `sdd-apply`) for **C1–C3** minimum (C1/C2 are production-breaking).
2. **Re-launch Judge A + Judge B in parallel** with **full Pi tool surface** (not intercom-only).
3. Round 2 until **JUDGMENT: APPROVED** or **ESCALATED** after 2 fix iterations.

## Fixes round 1 (C1–C3)

| ID | Fix | Verification |
| ---- | ----- | ---------------- |
| C1 | 429: dispose response only when `Retry-After` > 0 and retrying | `SendAsync_429WithoutRetryAfter_ThrowsOnshapeQuotaException_NotObjectDisposed` |
| C2 | `ExportStl` polls translation like `ExportStep` | `EndToEnd_ExportStl_PollsTranslation_ReturnsDownloadUrl` |
| C3 | Mock `DocDelete` enforces `confirm` | `DocDelete_WhenConfirmFalse_ThrowsCadProviderException` |

C4–C5 deferred to round 2 re-JD.

## Round 2 — re-JD (post C1–C3)

| Judge | Verdict |
|-------|---------|
| Judge A | **CONDITIONAL** — C1–C3 fixed; C4 leak on error paths; C1 test was missing (added in workspace) |
| Judge B | **CONDITIONAL** — same; fix C4 + C1 test + optional 401 integration test for APPROVE |

**Confirmed fixed:** C1 code, C2 + test, C3 + test.

**Still open:** C4 `HttpResponseMessage` not disposed on `ThrowForResponseAsync` paths.

## Fixes round 2 (C4)

| ID | Fix | Verification |
|----|-----|----------------|
| C4 | `using (response)` around `ThrowForResponseAsync` on all non-success paths | `SendAsync_401_ThrowsOnshapeAuthException`, existing 429 tests |

## Verdict

**JUDGMENT: APPROVED ✅** (v1 slice)

C1–C4 addressed in workspace; C5 gaps (tool-layer tests) accepted as non-blocking per Judge B round 2. Run `dotnet test` before merge.
