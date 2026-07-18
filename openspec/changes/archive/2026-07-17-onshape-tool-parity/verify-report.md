```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:e0c803e0d80ef018c2e41e7e2827e12c450a266f55ed1512608821a1eae6e200
verdict: pass
blockers: 0
critical_findings: 0
requirements: 14/14
scenarios: 23/23
test_command: dotnet test tests/McpCad.Tests/ -f net8.0 --nologo
test_exit_code: 0
test_output_hash: sha256:f74404194835066b2f6d9f43b025c32514b86842e472681f04dfdbe0ac1ea15d
build_command: dotnet build src/McpCad.Server/McpCad.Server.csproj -f net8.0 --nologo
build_exit_code: 0
build_output_hash: sha256:dfe6e2bb7598441d700b0534888c1c8675295572aab61433f4da4cd610f918dd
```

# Verification Report — onshape-tool-parity

**Mode:** Strict TDD active · final independent verification
**Verdict:** **PASS**

## Completeness

| Metric | Result |
| --- | --- |
| Task checkboxes | 102/102 complete |
| Required task headings | 27/27 complete |
| Optional/non-goal items | Explicitly classified; no unimplemented item is claimed as supported |

## Executed Evidence

| Check | Command | Exit | Result |
| --- | --- | ---: | --- |
| Full suite | `dotnet test tests/McpCad.Tests/ -f net8.0 --nologo` | 0 | 105 passed, 7 skipped, 0 failed |
| Build | `dotnet build src/McpCad.Server/McpCad.Server.csproj -f net8.0 --nologo` | 0 | 0 warnings, 0 errors |

- `test_output_hash`: `sha256:f74404194835066b2f6d9f43b025c32514b86842e472681f04dfdbe0ac1ea15d`
- `build_output_hash`: `sha256:dfe6e2bb7598441d700b0534888c1c8675295572aab61433f4da4cd610f918dd`

## Compliance Summary

| Area | Result | Evidence |
| --- | --- | --- |
| P0 Part Studio loop | PASS | BTMSketch/extrude builders, WireMock and golden conformance tests; supplied live rectangle → extrude evidence produced the preserved cube. |
| P1 feature status | PASS | Supplied live evidence confirms revolve, fillet, and chamfer with `featureStatus: OK`; `FeatureManager` rejects `ERROR` and `UNKNOWN` feature statuses. |
| Hole classification | PASS | `onshape_hole` is absent from the registered catalog and collision test; the matrix classifies hole as `not_supported` with the live regeneration-error reason. |
| P2 assembly atomics | PASS | Assembly list/create/delete have WireMock and tool-delegation coverage; supplied live run completed cleanup to zero instances. |
| Parity matrix | PASS | Matrix contains P0–P2 mappings, endpoint/status columns, documented cm/address contract, and an honest unsupported-hole row. |
| Naming and legacy path | PASS | Canonical registrations are `onshape_*`; collision tests pass; legacy sketch/extrude methods are deprecated delegates. |
| Strict-TDD traceability | PASS | `apply-progress.md` contains RED/GREEN/TRIANGULATE/REFACTOR evidence; golden fixtures are consumed by conformance tests. |

## Design Coherence

Builders/templates remain HTTP-free, writes use the wrapped feature/sketch payloads with microversion-skew protection, the provider routes through managers, and P2 assembly wrappers use the existing provider seams. This matches the approved design.

## Review Gate

- **Result:** `allow`
- **Approved receipt:** `.git/gentle-ai/review-transactions/v2/review-33c2491df289b5b7/review-receipt.json`
- **Lineage:** `review-33c2491df289b5b7`
- **Receipt terminal state:** `approved`
- **Post-apply context:** generation `1`; authority revision `sha256:dcd677f4d4bcf85eda09b44111f4f97cc813ea66cd83cc15f0b496b81226d3ae`; candidate tree `9b12a56d4809e5c8b67e3cf47305071b88930429`; paths digest `sha256:58f9bb27139e9d1d5d80e0c883e47c8763cc1239ffdc6b2dd9d651606b1a05d4`.

## Issues

### CRITICAL

None.

### WARNING

The atomic-catalog scenario describes `address_json` as an object with an `entities` array, while the approved design and actual MCP surface use a serialized `address_json` string plus explicit cm coordinate parameters. The runtime and documentation agree; reconcile this wording in a follow-up spec change rather than changing the verified interface.

### SUGGESTION

Add an env-gated automated live-smoke test when sandbox stability permits; current live evidence is preserved operational evidence, not a CI test.

## Final Verdict

**PASS** — all 102 task checkboxes are complete, the full suite and build are green, supplied live P0/P1/P2 evidence is consistent with the matrix, and the approved review gate allows archive.

```json
{
  "schema": "gentle-ai.verify-result/v1",
  "evidence_revision": "sha256:e0c803e0d80ef018c2e41e7e2827e12c450a266f55ed1512608821a1eae6e200",
  "change": "onshape-tool-parity",
  "status": "pass",
  "verdict": "PASS",
  "blockers": 0,
  "critical_findings": 0,
  "requirements": "14/14",
  "scenarios": "23/23",
  "checks": [
    {"criterion": "Tasks complete", "result": "pass", "evidence": "102/102 checkboxes and 27/27 required headings complete."},
    {"criterion": "Strict TDD evidence", "result": "pass", "evidence": "apply-progress records task-level RED/GREEN/TRIANGULATE/REFACTOR evidence and golden conformance coverage."},
    {"criterion": "Full test suite", "result": "pass", "evidence": "105 passed, 7 skipped, 0 failed; exit 0."},
    {"criterion": "Build", "result": "pass", "evidence": "net8.0 server build exited 0 with 0 warnings and 0 errors."},
    {"criterion": "P0-P2 live parity and matrix", "result": "pass", "evidence": "P0 sketch/extrude, P1 revolve/fillet/chamfer, and P2 assembly lifecycle are live-proven; hole is honestly not_supported."},
    {"criterion": "Review gate", "result": "pass", "evidence": "review-33c2491df289b5b7 is approved and the bound post-apply gate result is allow."}
  ],
  "test_command": "dotnet test tests/McpCad.Tests/ -f net8.0 --nologo",
  "test_exit_code": 0,
  "test_output_hash": "sha256:f74404194835066b2f6d9f43b025c32514b86842e472681f04dfdbe0ac1ea15d",
  "build_command": "dotnet build src/McpCad.Server/McpCad.Server.csproj -f net8.0 --nologo",
  "build_exit_code": 0,
  "build_output_hash": "sha256:dfe6e2bb7598441d700b0534888c1c8675295572aab61433f4da4cd610f918dd",
  "reviewGate": {
    "result": "allow",
    "receipt": ".git/gentle-ai/review-transactions/v2/review-33c2491df289b5b7/review-receipt.json",
    "lineage": "review-33c2491df289b5b7",
    "receipt_terminal_state": "approved",
    "context": {
      "gate": "post-apply",
      "lineage_id": "review-33c2491df289b5b7",
      "generation": 1,
      "store_revision": "sha256:dcd677f4d4bcf85eda09b44111f4f97cc813ea66cd83cc15f0b496b81226d3ae",
      "genesis_revision": "sha256:dcd677f4d4bcf85eda09b44111f4f97cc813ea66cd83cc15f0b496b81226d3ae",
      "chain_identity": "sha256:dcd677f4d4bcf85eda09b44111f4f97cc813ea66cd83cc15f0b496b81226d3ae",
      "bundle_digest": "sha256:dcd677f4d4bcf85eda09b44111f4f97cc813ea66cd83cc15f0b496b81226d3ae",
      "base_tree": "731ff94cbc733d6124b91a7cb0edc83fb23aeaa4",
      "candidate_tree": "9b12a56d4809e5c8b67e3cf47305071b88930429",
      "paths_digest": "sha256:58f9bb27139e9d1d5d80e0c883e47c8763cc1239ffdc6b2dd9d651606b1a05d4",
      "fix_delta_hash": "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "policy_hash": "sha256:34fb63d7f29f8613cd4431382b1057398a4816f8a4c20fc34677fffc80a184f6",
      "ledger_hash": "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "evidence_hash": "sha256:e0c803e0d80ef018c2e41e7e2827e12c450a266f55ed1512608821a1eae6e200",
      "base_relationship_valid": true
    }
  },
  "next": "ready-for-archive"
}
```
