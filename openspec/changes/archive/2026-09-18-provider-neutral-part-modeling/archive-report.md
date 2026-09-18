# Archive report: provider-neutral-part-modeling

- Archive status: **passed**
- Change: `provider-neutral-part-modeling`
- Archived path: `openspec/changes/archive/2026-09-18-provider-neutral-part-modeling/`
- Artifact store: OpenSpec

## Artifacts read

- `proposal.md`
- `specs/provider-neutral-part-modeling/spec.md`
- `design.md`
- `tasks.md`
- `apply-progress.md`
- `openspec/config.yaml`
- `sync-report.md`
- Verify report: **not present**. Native v2 status explicitly marks verification optional and archive ready; the final handoff supplied the available verification findings below.

## Task and verification truth

- Persisted `tasks.md` has no unchecked implementation task markers; all 14 rows are checked.
- Final reconciled scope is Onshape/Linux only: shared typed geometry, Onshape frame reader/mapper/preflight/context, and internal Onshape factory registration.
- Final Unit 3 focused result: **8 passed**.
- Final net8.0 regression: **657 passed, 7 skipped, 0 failed**.
- Unit 6A Onshape registration focused result: **2 passed** before the final regression.
- Native review lineage `review-0419f6b666c4977c` approved the corrected candidate; acknowledgement burned authority. No review operation was performed during archive.
- Units 4, 5, and 6B are reconciled as transfer—not implementation—to the future change `inventor-windows-part-modeling`.

## Canonical sync

- Domain synced: `provider-neutral-part-modeling`.
- Canonical path: `openspec/specs/provider-neutral-part-modeling/spec.md`.
- Sync was a non-destructive new-domain copy because no canonical file existed.
- Requirements synced: `Typed Portable Modeling Contract`; `Required Onshape Modeling Baseline`; `Explicit Geometry Interpretation and Pre-Mutation Validation`; `Capability-Gated Optional Evidence and Revision Metadata`; `Ordered Failure and Mutation Uncertainty Reporting`; `Existing Public Surface and Provider Selection Remain Unchanged`.
- ADDED/MODIFIED/REMOVED delta requirement names: none; the source was a full new domain spec rather than an operation-section delta.
- Same-domain active change warning: none.
- Destructive merge approval/blocker: none; no conflicting canonical spec was overwritten.

## Structured status and action context

- Native status schema: `gentle-ai.sdd-status` v2.
- Native state: `ready`; `nextRecommended: archive`; `applyState: all_done`; tasks `14/14`; no blockers or notes.
- Artifact store: `openspec`.
- Action context: `repo-local`.
- Workspace and allowed edit root: `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`.
- Archive paths and writes remained within the authoritative workspace/allowed root.
- Skill resolution: `paths-injected` (`gentle-ai`, `cognitive-doc-design`).

## Scope boundaries preserved

No live CAD, Windows, Inventor implementation, two-provider parity, commits, branches, PRs, staging, push, cleanup, publication, source changes, test execution, CAD/network calls, or credentials were performed during archive. The deferred exact future change `inventor-windows-part-modeling` was not created or approved.

Memory observation IDs: none (OpenSpec-only artifact store).
