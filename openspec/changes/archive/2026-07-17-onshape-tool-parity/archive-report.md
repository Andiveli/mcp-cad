# Archive Report: onshape-tool-parity

**Archived:** 2026-07-17  
**Mode:** hybrid  
**Result:** archived

## Gates

- Native status: `nextRecommended: archive`; blockers: none; verification: `all_done`; archive: ready.
- Review gate: `allow`; approved terminal receipt: `review-33c2491df289b5b7`.
- Final receipt: `.git/gentle-ai/review-transactions/v2/review-33c2491df289b5b7/review-receipt.json`.
- Receipt terminal state: `approved`; final candidate tree: `9b12a56d4809e5c8b67e3cf47305071b88930429`.
- Paths digest: `sha256:58f9bb27139e9d1d5d80e0c883e47c8763cc1239ffdc6b2dd9d651606b1a05d4`.
- Verification: 14/14 requirements, 23/23 scenarios, 102/102 tasks; 105 passed, 7 skipped, 0 failed.
- No CRITICAL verification findings. Hole remains explicitly `not_supported`; live P0–P2 proof is complete for the supported catalog.

## Specs Synced

| Domain | Action | Details |
|---|---|---|
| `onshape-integration` | Updated | Added BT FeatureAdd and BTMSketch requirements; updated Basic+HMAC authentication wording. |
| `part-studio-modeling` | Created | Copied the complete capability specification. |
| `onshape-atomic-catalog` | Created | Copied the complete catalog specification. |
| `onshape-tool-parity-matrix` | Created | Copied the complete parity-matrix specification. |

## Archive Verification

- Main specs updated before archive move.
- Active change directory no longer contains `onshape-tool-parity`.
- Archive contains proposal, design, tasks, apply progress, verification report, specs, and this report.
- Persisted task artifact has no unchecked tasks; all 102 entries are checked, including explicitly classified optional/non-goal items.
- `.atl` and `.pi` were not touched. Credentials were not rotated. No commit or push was performed.

## Source of Truth

- `openspec/specs/onshape-integration/spec.md`
- `openspec/specs/part-studio-modeling/spec.md`
- `openspec/specs/onshape-atomic-catalog/spec.md`
- `openspec/specs/onshape-tool-parity-matrix/spec.md`

## Engram Traceability

The hybrid archive report is also persisted to Engram under `sdd/onshape-tool-parity/archive-report`. The report records the filesystem artifact paths and the exact native review receipt lineage above.
