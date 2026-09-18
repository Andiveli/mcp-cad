# Pre-proposal gate

## Current evidence-driven gate (revision 3)

This section supersedes all historical admission/readiness and no-research/no-split statements below and in the unchanged proposal. Product scope remains confirmed: both Onshape and Inventor; additive contracts/adapters only. AUTO/OpenSpec. Official Onshape/Autodesk research and <=400 authored changed-line units are authorized; chain topology deferred. No exception, implementation, live CAD, tasks, commits or delivery authorized by this update.

```json
{
  "schema": "gentle-ai.sdd-preproposal/v1",
  "revision": 3,
  "change": "provider-neutral-part-modeling",
  "research_request": {"selected": true, "classes": ["documentation"], "publishers_allowed": ["Onshape", "Autodesk"]},
  "admission": {"outcome": "parent-validated-evidence-accepted", "scope": "Reason from supplied official parent retrievals; no child fetch claimed"},
  "research_reference": "research.md#current-assessment--supersedes-historical-gates-below",
  "evidence_references": ["A1", "A2", "A3", "A4", "A5", "O1", "O2", "O3", "O4", "O5", "O6"],
  "product_decisions": {"status": "confirmed", "both_providers_required": true},
  "research_status": "partial-substantive",
  "proposal_ready": false,
  "remaining_evidence": ["REST BTMSketch-151 datum selection and actual frame equivalence to canonicalized evPlane plus projected origin, including extrusion normal", "Required BTFSValue subtype layouts/units and current Part Studio libraryVersion acquisition"],
  "delivery": {"unit_budget_lines": 400, "units_authorized": true, "chain_topology": "deferred", "size_exception": false},
  "native_status": "unchanged"
}
```

A5 now establishes target-2027 work-plane inheritance and numeric extrusion semantics; O4/O5 establish pre-sketch evaluation. Runtime frame inspection and full coordinate transforms avoid hardcoded native axes/index corrections. O6 does not alone establish REST sketch equivalence. Readiness is held on that precise Onshape evidence, not child tool absence, old delivery permission, or Windows verification. Parent may complete bounded official extraction under existing permission; no repeated product interview or no-grants research retry is requested. Current design contains provider-local seams, four-corner rectangle handling, pre-mutation failures and <=400 estimates. Only design/research/preproposal edited; no native status recalculation or next-phase launch.

## Historical parent research assessment

Official-documentation research is selected and **partial**, not globally denied. The parent fetched and validated the passages recorded in [research.md](research.md#parent-validated-evidence-current-research-state). Native default frame mapping and target-version applicability remain unresolved, so dependent geometry planning is held. The child-local admission records below remain historical. Product scope stays confirmed; user authorized review units of at most 400 changed lines, with chain topology deferred and no automatic commits, PRs, or size exception.


## Current research gate (supersedes historical research selection below)

```json
{
  "schema": "gentle-ai.sdd-preproposal/v1",
  "revision": 2,
  "change": "provider-neutral-part-modeling",
  "exploration_reference": "explore.md",
  "research_request": {
    "selected": true,
    "classes": ["documentation"],
    "scope": "Official Onshape and Autodesk documentation for canonical native plane identity/origin/basis/normal, Top/Front/Right and Inventor XY/XZ/YZ mapping, portable 2D coordinate transforms and extrusion-direction signs.",
    "questions_reference": "research.md#native-plane-research--blocked",
    "restrictions": ["Official documentation only", "No arbitrary open-web research", "No live CAD calls or credentials", "No product code or global configuration edits"]
  },
  "admission": {
    "outcome": "denied",
    "observed_exact_grants": {"documentation": [], "open-web": []},
    "missing_capabilities": ["fetch_content"],
    "scope": "Child-local admission only; not a global denial of parent research",
    "parent_handoff": "Retrieval records and passages supplied; validation and partial-evidence persistence require an evidence-enabled executor under this runtime contract"
  },
  "research_reference": "research.md",
  "evidence_references": [],
  "product_decisions": {"status": "confirmed", "authority": "Existing explicit product selections remain unchanged; native frame evidence remains unresolved."},
  "proposal_ready": false,
  "native_status": "unchanged"
}
```

Current user selection explicitly authorizes scoped research and split delivery at no more than 400 changed lines; chain topology remains deferred, with no commits, PRs or size exception authorized. The child lacks `fetch_content`; parent-reported availability does not confer child capability. This mandatory executor-contract gate update does not modify native planning status or authorize a new proposal. Historical phase records below remain for provenance.

Status: confirmed by explicit user selections. Execution: SDD AUTO. Artifact store: openspec.

Evidence: [exploration](explore.md). Parent-reported installed native `sdd-status`: nextRecommended=propose, blockedReasons=[], repo-local workspace `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`. This supersedes the exploration's earlier unavailable-status note. The native planning-artifact report preceded this proposal; exploration and this gate were available on disk.

## Confirmed product decisions

- Public surface: shared contracts and adapters ONLY; no new public MCP tool.
- Required portable baseline: canonical-plane rectangle/circle sketches and new-body extrusion for BOTH implemented providers, Onshape and Inventor.
- Evidence/capture and revision metadata: optional, capability-gated and provider-specific; no cloud/desktop equivalence or concurrency promises.
- Compatibility: preserve all existing public tools, schemas and behavior.
- Selection and targets: current server configuration selects the provider; targets remain adapter-owned.
- Failure policy: stop on modeling failure; no automatic retry or rollback.
- Research: explicitly unselected. Repository evidence only; no external research is requested or required.
- Exclusions: SolidWorks implementation and the future orchestrator/worker/verifier loop.

## Confirmation record

The user explicitly supplied the decisions and prohibited another interview. The product question round is resolved by those selections, not by inferred agreement. No pending product confirmation blocks proposal writing.

Both adapters are required in this change. The exploration's suggestion to defer Inventor or preselect split delivery is superseded by this confirmation.

## Delivery boundary

Delivery strategy: ask-on-risk. Review budget: 400 changed lines. No split, chain strategy, or size exception is authorized. If a later forecast cannot accommodate the confirmed scope, pause for a delivery decision; do not silently remove Inventor or weaken acceptance.

This phase writes only this gate and [proposal.md](proposal.md). No source edits, implementation, commits, or next-phase execution are authorized by this executor's assignment.
