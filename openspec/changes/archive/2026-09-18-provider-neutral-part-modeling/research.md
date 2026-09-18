# Native plane research — evidence-driven frame inspection

## Current runtime evidence reconciliation

The sanitized [Unit 2 frame evidence](evidence/onshape-unit2-frame-summary.md) closes the previously stale runtime-frame and bounded evaluator-value evidence for the **released Unit 2A reader/parser slice**. It records three default datum frames, owner-frame agreement, asymmetric-prism checks, API v17/3070 response facts, and measured legacy aliases without retaining target identifiers or raw responses.

`proposal_ready: false` remains as historical research-gate state: it is not a claim that Unit 2A cannot proceed. The current release is intentionally narrower than the full portable proposal: no adapter integration, full Unit 2 completion, Inventor work, registration, public API, model write, or review approval is implied. The remaining architectural scope must be decided from later artifacts, not extrapolated from this experiment.

## Current assessment — supersedes historical gates below

**Substantively partial, with concrete frame-inspection design now supported.** Child fetch-tool absence is not a research blockade. Parent-validated A5 closes target-2027 GetPosition and extrusion units/direction; O4/O5 establish a real pre-sketch evaluation endpoint. Hardcoded default axes and Inventor index identity are no longer prerequisites: inspect and validate runtime geometry. Both providers remain required. See [current design](design.md#current-evidence-driven-decision-supersedes-historical-readiness-below).

### New parent-validated sources

The parent used its actual fetch_content/get_search_content tools. This executor reasoned from the supplied exact passages; it did not retrieve these URLs. Retrieval occurred in the current session; exact timestamp unavailable. Earlier retrieval records remain below as provenance.

| Ref | Official source / retrieval record | Established facts and limits |
| --- | --- | --- |
| A5 | https://help.autodesk.com/cloudhelp/2027/ENU/Inventor-API/files/WorkPlane_GetPosition.htm and https://help.autodesk.com/cloudhelp/2027/ENU/Inventor-API/files/ExtrudeDefinition_SetDistanceExtent.htm ; response `mtuyk7h019z9wl`, indexes 1/2 read with get_search_content | Same passages as A1/A4: sketches inherit work-plane origin/orientation; GetPosition returns Origin/XAxis/YAxis; numeric distance centimeters; kPositiveExtentDirection follows sketch normal. These two methods now have target-2027 evidence. A2/A3 are still 2024 sources. |
| O4 | https://onshape-public.github.io/docs/api-adv/fs/ ; raw `mtuymih36hctkd` index 1, readable `mtuymvh0b8dy9x` index 0 | Official Evaluating FeatureScript guide: POST `/api/v6/partstudios/d/{did}/w/{wid}/e/{eid}/featurescript?rollbackBarIndex=-1`, body `{libraryVersion:2144,script:lambda}`, lambda expressions only. V8 example `function(context is Context,queries){return transientQueriesToStrings(evaluateQuery(context,qCreatedBy(makeId("Top"),EntityType.FACE)));}` obtains a default datum before sketch creation. Other planeName placeholder supported. Does not establish its basis or REST sketch-frame equivalence. |
| O5 | https://cad.onshape.com/api/openapi ; raw `mtuynh99rug60r`; parent exact symbol/field searches via get_search_content | OpenAPI 3.0.1, info.version `1.220.87559-9d09f09aac42`, current server `https://cad.onshape.com/api/v17`. evalFeatureScript POST `/partstudios/d/{did}/{wvm}/{wvmid}/e/{eid}/featurescript`; wvm enum w/v/m; rollbackBarIndex default -1, optional elementMicroversionId/configuration, required path address fields. Security OAuth2Read including BasicAuth as reported by parent. |
| O6 | https://cad.onshape.com/FsDoc/library.html ; fresh response `mtuyk7h019z9wl` index 3, parent canonical/evPlane searches | alignCanonically(context,plane) returns the plane representing the coordinate system of a coplanar face; used in sketch-pattern plane transformation. newSketch follows canonical orientation and world-origin projection; newSketchOnPlane preserves explicit frame. evPlane reads planar face plane; evOwnerSketchPlane reads an actual existing sketch entity, therefore is not preflight. Two-vector plane constructor chooses arbitrary perpendicular x; normal alone cannot establish axes. |

O5 request BTFeatureScriptEvalCall-2377 exposes script string, queries map of string arrays, libraryVersion integer ('FeatureScript version used in Part Studio. Do not modify'), serializationVersion/sourceMicroversion/rejectMicroversionSkew. Response BTFeatureScriptEvalResponse-1859 exposes notices (BTNotice-227 array), console, result (BTFSValue-1888), libraryVersion/sourceMicroversion/serializationVersion/microversionSkew. Result uses required btType discriminator including Array-1499, Number-772, String-1422, Map-2062, WithUnits-1817, Other, Undefined and TooBig. Exact subtype value fields remain unextracted; no JSON result layout is guessed. The guide's 2144 and v6/v8 examples are not a current version-selection policy.

### Question disposition and precise next extraction

- Q1/Q2 Inventor: runtime GetPosition/Plane matching avoids guessed indices and axes; validate selected origin-centered datum before Sketches.Add. No need to document a universal WorkPlanes ordering.
- Q1/Q2 Onshape: query endpoint is established. Candidate `alignCanonically(evPlane(...))` plus world-origin projection is justified for documented newSketch semantics, **not yet established for serialized BTMSketch-151**. Need official bridge proving that REST sketchPlane selection uses this frame, and that evaluated Top/Front/Right datum matches builder operation/deterministic IDs. Plain evPlane is insufficient; post-sketch evOwnerSketchPlane cannot close a pre-mutation requirement.
- Q3/Q4: explicit rigid transform and normal-sign mapping are designed, not native-default claims. Arbitrary orthogonal in-plane rotation must work; signed permutations cannot be assumed. Existing BuildClosedProfile supports four transformed rectangle edges (centimeter input, meter output); BuildRectangle and axis-only BuildPolyline are unsuitable for arbitrary rotation. Retain Onshape extrusion-template direction linkage tests and require authoritative normal semantics in the REST bridge.
- Q5: closed for GetPosition/SetDistanceExtent by A5. 2027 corroboration of optional post-sketch conversions is not needed by the minimal algorithm.
- Bounded implementation evidence: extract only required BTFSValue subtype fields/units and supported acquisition of the current Part Studio libraryVersion. No new generic protocol, full API inventory, or live model experiment.

`proposal_ready: false` remains as the existing readiness field pending the substantive Onshape bridge, not an admission failure or missing permission. Research selection and <=400-line units are approved; chain topology deferred. No implementation/tasks/live CAD/delivery performed. All historical denials and claims of globally unvalidated parent evidence below are superseded by this assessment.

## Parent-validated evidence (current research state)

Overall status: **partial**. The child-local blocked execution below is retained as history, not a global research prohibition. The parent retrieved and read the following official passages using its own active tools. These establish API semantics, not the unresolved default datum-to-sketch frame mapping. Geometry tasks remain held; `proposal_ready` remains false.

Retrieval: during the current session; exact timestamp was not captured. Autodesk sources are the 2024 API documentation; applicability to the project's Inventor 2027 interop remains unverified. Onshape's live FeatureScript documentation is not version-pinned.

| Source | Official URL and supporting excerpt | Bounded claim |
| --- | --- | --- |
| A1 | https://help.autodesk.com/cloudhelp/2024/ENU/Inventor-API/files/WorkPlane_GetPosition.htm — “When sketches are created on a work plane they inherit the work plane's origin and orientation.” Returns Origin, XAxis and YAxis. | The API exposes the plane frame before sketch creation; this does not establish the identity of an indexed datum. |
| A2 | https://help.autodesk.com/cloudhelp/2024/ENU/Inventor-API/files/PlanarSketch_ModelToSketchSpace.htm — “takes a 3D coordinate in model space, projects it onto the sketch plane along the normal of the plane and returns a Point2d”. | An explicit model-to-sketch projection is available. |
| A3 | https://help.autodesk.com/cloudhelp/2024/ENU/Inventor-API/files/PlanarSketch_SketchToModelSpace.htm — “takes a 2D coordinate in sketch space, and returns a Point3d containing the coordinates of the point in model space.” | The inverse coordinate conversion is exposed. |
| A4 | https://help.autodesk.com/cloudhelp/2024/ENU/Inventor-API/files/ExtrudeDefinition_SetDistanceExtent.htm — “If a value is input, the units are centimeters.” “kPositiveExtentDirection defines the offset direction to be in the same direction as the normal of the sketch plane.” | Numeric depth is centimeters and positive follows sketch normal, in this documented API version. |
| O1 | https://cad.onshape.com/FsDoc/library.html#XY_PLANE — XY_PLANE is `plane(vector(0,0,0)*meter, vector(0,0,1), vector(1,0,0))`; YZ_PLANE uses normal `(1,0,0)` and x-axis `(0,1,0)`; XZ_PLANE uses normal `(0,1,0)` and x-axis `(0,0,1)`. | These are explicitly defined FeatureScript constants, NOT proof of the REST builder's default sketch frames. In particular XZ_PLANE differs from the proposed portable Front basis. |
| O2 | https://cad.onshape.com/FsDoc/library.html#newSketch-Context-Id-map — “The sketch coordinate system follows the canonical plane orientation and the sketch origin is the projection of the world origin onto the plane.” `newSketchOnPlane` says “The sketch coordinate system will match the coordinate system of the plane.” | The two creation APIs have documented frame semantics; REST equivalence remains unproven. |
| O3 | https://cad.onshape.com/FsDoc/library.html#worldToPlane-Plane-Vector — “Transforms a 3D worldPoint in world coordinates into a 2D point measured in a Plane's (x,y) coordinates.” | FeatureScript supports explicit coordinate transformation. |

### Retrieval audit

Parent `fetch_content` call 1 used URLs `https://cad.onshape.com/help/Content/planes.htm`, `https://help.autodesk.com/cloudhelp/2024/ENU/Inventor-API/files/WorkPlane.htm`, A2 and A3; response ID `mtuybxy50ttyqk`. Parent read content indexes 1, 2 and 3 with `get_search_content`. The Onshape help URL returned generic help, not usable plane-frame evidence.

Parent `fetch_content` call 2 used `https://cad.onshape.com/FsDoc/library.html`, A1 and A4; response ID `mtuycbkl0cq3j1`. Parent read indexes 1 and 2 in full and located index 0 passages with `get_search_content(findText=["XY_PLANE","XZ_PLANE","YZ_PLANE","worldToPlane","sketchPlane"])` and a subsequent search for `canonicalPlane`, `coordSystem`, `newSketch`. No `canonicalPlane` match was returned.

### Remaining evidence gap

We still need the default native datum identities and actual sketch bases used by the existing REST/Inventor paths, including target-version applicability. A design using native frame inspection and explicit coordinate conversion could avoid guessed hardcoded bases, but is an unvalidated design alternative, not completed research. No live CAD call or product-code change was made.

## Historical child execution (not current global admission)

```json
{
  "schema": "gentle-ai.sdd-research/v1",
  "revision": 2,
  "change": "provider-neutral-part-modeling",
  "outcome": "blocked",
  "artifact_store": "openspec",
  "research_request": {
    "selected": true,
    "authority": "Current explicit user selection after corrective design pass; supersedes earlier research-unselected state.",
    "classes": ["documentation"],
    "publishers_allowed": ["Onshape", "Autodesk"],
    "scope": "Canonical native plane identity, origins, sketch bases and normals, and Top/Front/Right mapping to Inventor XY/XZ/YZ, sufficient for portable 2D coordinates and extrusion-direction transforms.",
    "restrictions": ["Official documentation only", "No arbitrary open-web research", "No live CAD calls", "No credentials", "No product code or global configuration edits"]
  },
  "questions": [
    {"id": "Q1", "text": "What native datum identities and world-space origins correspond to Onshape Top, Front and Right, and Inventor XY, XZ and YZ, including verified Inventor WorkPlanes index identity?"},
    {"id": "Q2", "text": "For sketches created on each datum through the intended provider API, what world-space vectors define local positive x, local positive y and the oriented normal? Are these defaults stable or dependent on creation options or document conventions?"},
    {"id": "Q3", "text": "How does each native positive/negative extrusion option relate to the actual sketch normal, including any orientation reversal?"},
    {"id": "Q4", "text": "Which coordinate transforms and direction sign mappings connect those evidenced native frames to the proposed portable Top (+X,+Y,+Z), Front (+X,+Z,-Y), and Right (+Y,+Z,+X) frames? Is a signed permutation sufficient?"},
    {"id": "Q5", "text": "Does Autodesk 2024 documentation establish semantics applicable to the design's Inventor 2027 v30 interop target, or is target-version corroboration required?"}
  ],
  "admission": {
    "outcome": "denied",
    "observed_exact_grants": {"documentation": [], "open-web": []},
    "selected_class": "documentation",
    "required_tool": "fetch_content",
    "missing_capabilities": ["fetch_content"],
    "child_local_check": "The active tool definitions contain no fetch_content tool. Parent-reported availability is not child-local availability.",
    "reason": "Selected official-documentation lane cannot execute in this runtime. No shell, gateway, memory or repository-read substitute is admitted."
  },
  "candidate_urls_not_evidence": [
    "https://cad.onshape.com/help/Content/planes.htm",
    "https://help.autodesk.com/cloudhelp/2024/ENU/Inventor-API/files/WorkPlane.htm"
  ],
  "evidence_tool_calls": [],
  "sources": [],
  "validated_claims": [],
  "retrieval_time": null,
  "supporting_excerpts": [],
  "missing_evidence": ["Child-admitted validation of Q1–Q5 remains unavailable. Parent has supplied attributed retrieval records and passages; these are not child-validated claims."],
  "source_ambiguity": ["Parent reports retrieval; child has not independently validated publisher, content or version applicability.", "Default native sketch frame mapping remains unresolved in the supplied handoff; do not equate FeatureScript plane constants with REST default sketch frames."],
  "product_decisions": {
    "status": "confirmed",
    "note": "The portable frames are proposed contract choices from design.md, not native geometry claims. Product authority remains with the orchestrator."
  },
  "proposal_ready": false,
  "native_status": "unchanged",
  "skill_resolution": "paths-injected"
}
```

## Inputs and boundary

Read `design.md`, `preproposal.md`, and `explore.md` directly. The initial `exploration.md` lookup did not exist; the preproposal link resolved the input to `explore.md`. No previous `research.md` existed. Loaded the injected cognitive-doc-design skill; no separate executor skill path was supplied.

The current selection supersedes historical statements that research is unselected. User reports split delivery at no more than 400 changed lines, with chain topology deferred and no commit, PR or size exception authorized. No native status fields, design, implementation or CAD state were changed.

## Next step

Return to an evidence-enabled parent/executor for validation and persistence of the supplied partial evidence, retaining the already authorized scope. No new permission round is needed. This child's explicit no-grants contract requires a blocked outcome with no validated claims; it is not a global denial of parent research. Keep dependent geometry tasks held.

## Parent retrieval handoff received

The current user message supplies passages attributed to parent retrievals with response IDs `mtuybxy50ttyqk` and `mtuycbkl0cq3j1`. Parent reports using `fetch_content` for official Onshape help, FeatureScript library and Autodesk 2024 WorkPlane, sketch/model conversion, GetPosition and SetDistanceExtent pages, followed by `get_search_content` on the second response. These calls did not run in this child. Retrieval reportedly occurred this session; exact session retrieval time is unavailable. This handoff receipt is not a validated source or geometry claim, and the supplied passages are not promoted into the sources/claims arrays.

Remaining precise question for admitted validation: what origins, local x/y vectors and oriented extrusion normals does the actual existing REST/Inventor sketch-creation path use for each canonical datum, and how do those map to the proposed portable frames? Preserve separate checks for Inventor datum-index identity, 2024-to-2027 applicability and Onshape REST serialization. Do not infer default sketch frames from FeatureScript constants.

Official next-source targets, not evidence: Onshape documentation connecting default datum queries and REST sketch serialization to sketch coordinate systems; Autodesk target-version documentation for origin WorkPlanes identity and sketch/work-plane transforms. Runtime frame inspection versus hardcoded mapping remains a candidate for later evaluation, not an admitted conclusion or settled design. No live inspection is authorized.
