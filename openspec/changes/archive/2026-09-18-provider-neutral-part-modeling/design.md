# Design: Onshape/Linux bounded sketch–extrusion context

## Scope reconciliation — authoritative current design

The completed design and implementation boundary is an internal **Onshape/Linux** sketch–extrusion context: portable typed geometry, frame reader/mapper/preflight, address-bound Onshape execution, and internal Onshape factory registration. It preserves existing public MCP tools, schemas, provider selection, and provider-specific behavior. It does not add a user-facing provider-neutral tool or certify live CAD.

The retained evidence for this boundary is: Unit 3 focused test suite **8 passed**; net8.0 regression **657 passed, 7 skipped, 0 failed**; and approved/acknowledged native review lineage `review-0419f6b666c4977c`. This is preserved review evidence, not a new review action in this reconciliation.

All remaining Inventor work is outside this change. Units 4 (native frame seam and fakes), 5 (context/transform conformance), and 6B (factory registration) are explicitly deferred to `inventor-windows-part-modeling`. That follow-up needs a later native planning launch, its own proposal authorization, and a configured Windows/Inventor boundary; it must not infer completion or validation from Linux evidence. Historical two-provider design text below is retained solely as provenance and is superseded where it conflicts with this section.

## Released Unit 2A reader/parser boundary

The measured [Onshape Unit 2 evidence](evidence/onshape-unit2-frame-summary.md) releases only an internal reader and parser at `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs` with tests at `tests/McpCad.Tests/Onshape/OnshapePartModelingFrameReaderTests.cs`. It uses the existing transport for a target-bound, read-only evaluator request; it adds no public API and performs no model write.

The parser must recognize only exact current and measured legacy value aliases needed for the frame payload, require strict finite values, and reject notices, microversion skew, malformed/unsupported values, and absent results. The observed v17 response used library version 3070 and `BTFeatureScriptEvalResponse-1859`; the fully-qualified legacy Array/String/Number aliases require fixture coverage. The former mixed fixture's missing legacy Number alias is a test-fixture pitfall, not a permissive-parser justification.

This release supersedes the older Unit 1-only/Unit 2 HOLD authority below only for Unit 2A. It does not integrate a reader into an adapter or release the full Unit 2 bridge. It also does not settle arbitrary transforms, negative extrusion, Inventor, registration, or any remaining architecture. Independent live evidence is not native review approval; inspection stopped before lineage on blocked selection.

## Historical evidence-driven decision (superseded by scope reconciliation above)

**Frame inspection plus a rigid coordinate transform is the minimal design; hardcoded native axes and datum-index corrections are unnecessary. Design is concrete but conditionally ready: Onshape REST sketch-frame equivalence remains a precise evidence gap.** A5 closes Inventor 2027 GetPosition and numeric extrusion semantics. O4/O5 close availability of pre-sketch FeatureScript evaluation, not the frame of `BTMSketch-151`. Both providers remain mandatory; unavailable frame inspection is a stopped pre-mutation failure, never an optional capability or fallback.

### Historical Unit 1 authorization

The explicit human choice now authorizes **Unit 1 only**: additive `McpCad.Core` shared contracts, pure validation, and their `McpCad.Tests` tests, bounded to at most 400 authored changed lines. This is a narrow exception to the broader hold: it has no provider API, native-frame transform, adapter, registration, public-tool, generic-executor, or arbitrary-stage-list work, so it is independent of the unresolved Onshape REST frame evidence. Units 2–6 remain HOLD until their stated evidence or dependencies close; both adapters remain mandatory future scope. This note records task/apply authority only—this design/research is still partial and does not claim whole-design or research completion, select a chain/remote base, authorize a size exception, or perform source implementation in this phase.

Current authority permits official research and units of **at most 400 authored changed lines**, superseding earlier no-research/no-split statements in this file and the unchanged proposal. AUTO/OpenSpec; chain topology deferred. This invocation updates only design/research/preproposal, starts no tasks and authorizes no implementation, live CAD, commits or delivery. Historical sections below retain provenance; this section governs conflicts.

### Historical Unit 6A authorization (superseded by scope reconciliation above)

The former registration Unit 6 is split into **Unit 6A** and **Unit 6B**. Unit 6A is explicitly released after the completed Onshape Unit 3: register only an internal `IPartModelingContextFactory` backed by the existing Onshape address-bound adapter through the existing server/bootstrap configuration path, and add Linux-verifiable dependency-injection compatibility tests. The factory creates a fresh context for each adapter-owned `CadElementAddress`; it does not introduce a public MCP tool, schema, provider argument, target schema, network call, credential requirement, or live CAD invocation. Existing Onshape tool registrations and existing provider interfaces remain unchanged.

Unit 6B remains HOLD until Inventor Units 4–5 and their Windows boundary are released and complete. It alone will register the Inventor factory in `ProviderBootstrap.Inventor.cs`. The earlier combined-registration rollout rule remains authoritative for claiming two-provider availability; Unit 6A is deliberately only an additive Onshape registration seam and makes no Inventor, complete-provider-neutral, or cross-platform parity claim.

### Provider-local read seams and evidence boundary

- Add `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs`: internal reader using the existing client transport (parent source map identifies `OnshapeClient.SendJsonAsync`), not an ICadWebProvider extension. The attempted child path `Client/OnshapeClient.cs` was absent; no client implementation/path verification is claimed here. Inject reader into the address-bound adapter. FeatureList is not evaluation.
- O5 documents `POST /partstudios/d/{did}/{wvm}/{wvmid}/e/{eid}/featurescript` under current `/api/v17`; use workspace `w`, exact bound address and `rollbackBarIndex=-1`. This is a read/evaluation POST, not a modeling write. A provider-owned fixed lambda may only query the datum and calculate its frame; no caller script, newSketch, or model operation is accepted. Reuse existing authentication; O5's security declarations do not establish this client's authentication implementation.
- O4 demonstrates pre-sketch `qCreatedBy(makeId("Top"),EntityType.FACE)` evaluation through the endpoint. Resolve exactly one intended datum. Candidate frame computation is `alignCanonically(context, evPlane(context, ...))`, with origin projected from world zero as O2 specifies for newSketch. Read explicit x and normal; derive y by cross product, never choose x from a normal alone. O6 supplies canonicalization semantics, **not a documented bridge from REST BTMSketch-151 to newSketch**. Also establish that the evaluated datum is the one selected by builder TopplaneOp/JDC (and Front/Right equivalents). Until that bridge is evidenced, do not claim this candidate equals the actual REST sketch frame or enable baseline registration.
- O5 request is BTFeatureScriptEvalCall-2377: script, optional queries map and documented version fields. Preserve the Part Studio libraryVersion; guide value 2144 is an example, not a constant to copy. Exact acquisition of that version and bounded BTFSValue subtype `value` layouts require official extraction before implementing the reader. Parse only the needed discriminator variants and reject notices indicating evaluation failure, absent/undefined/TooBig result, unsupported shapes, nonfinite values, skew or malformed units. Do not invent a generic FeatureScript protocol, result JSON layout, or revision guarantee.
- Extend planned `IInventorPartModelingNative` internally with read-only work-plane frame inspection. Inspect existing candidate WorkPlanes, match geometry using GetPosition plus Plane normal rather than labels/index order, and retain the matched native object for Sketches.Add. Require a unique origin-centered coplanar candidate; ambiguity fails before mutation. A5 explicitly establishes inherited origin/orientation in target 2027. GetPosition provides x/y; validate their cross product against the native Plane normal. A2/A3 conversions are corroboration from 2024, not newly verified 2027 APIs and not required for the preflight transform. No correction of global PlaneMap.

### Transform contract and pre-mutation flow

Desired frames remain Top `(X,Y,Z)`, Front `(X,Z,-Y)`, Right `(Y,Z,X)`. In common length units let desired point `p = u*x + v*y`, native frame `(o,a,b,n)`. Compute `q = (dot(p-o,a), dot(p-o,b))`; transformation matrix is `M=[[a·u,a·v],[b·u,b·v]]`. Native direction sign is requested sign times `sign(n·desiredNormal)`; positive depth stays positive. Circles transform center only and preserve radius.

Before sketch mutation require finite origin/axes, unit mutually perpendicular axes, right-handed native frame, desired geometric plane match, origin near world zero, normal parallel/antiparallel to desired normal, `MᵀM≈I`, and `det(M)≈sign(n·desiredNormal)` with absolute determinant near one. Proposed explicit testable tolerances: 1e-9 dimensionless for axes/determinant and 1e-8 meters for origin/coplanarity (converted for COM centimeters); reject outside tolerance rather than repair/snap frames. Validate transformed and serialized geometry for collapse/overflow as well as raw inputs. These are design tolerances, not documented vendor accuracy guarantees.

A signed permutation is an optimization only when measured M supports it, not a required assumption. **Use all four ordered rectangle corners in every case**, avoiding a special-case fast path. Two-corner BuildRectangle would turn an arbitrary rotated rectangle into its bounding box; BuildPolyline also rejects diagonal edges. Direct child read verifies existing `BuildClosedProfile(pointsCm, plane)` supports ordered straight edges and converts centimeters to meters with N9 rounding. Supply transformed points in centimeters to that method exactly once; circle and extrusion builders still take meters. COM uses four SketchLines joined into the exact pinned sketch, not a two-corner native rectangle under arbitrary rotation. Reflection reverses winding without changing solid new-body intent.

Flow: validate request/target → read native datum/frame → validate frame and transform complete profile → recheck detectable target → create sketch → bind exact sketch/profile → extrude with mapped sign. Frame-reader transport/parser/cancellation/ambiguity errors are Sketch Failed/None, latch Stopped, Extrusion NotAttempted, **zero sketch/extrusion mutation and no automatic retry**, even though Onshape evaluation uses POST. A failure after actual sketch dispatch retains existing uncertainty rules. Do not create a disposable sketch to inspect evOwnerSketchPlane: it is post-mutation and cannot satisfy preflight. External edits between inspection and writes remain outside concurrency guarantees.

### Tests, rollout and bounded estimates

Keep previous conformance/ownership/failure tests; update WireMock ordering to target GET → evaluation POST → sketch POST → extrusion POST. Assert no write on every inspection failure and exactly one evaluation attempt. Add independent numeric frame fixtures covering axis permutation, flipped normal/reflection, arbitrary 30-degree in-plane rotation, asymmetric translated rectangles, circle centers and both directions on all planes. Round-trip native coordinates to expected world corners; test N9 collapse, units, wrong plane/offset origin, nonorthonormal/singular matrices, determinant mismatch, missing datum and ambiguity. OpenAPI-backed result fixtures must preserve discriminator/units/version evidence rather than fabricate layouts. Windows fake-native tests prove read calls precede Sketches.Add and exact plane/profile identity; platform execution is later verification, not a research blocker.

Revised estimates are authored additions plus deletions including tests/docs, not measurements or a task launch:

| Cohesive unit (behavior and tests together) | Estimate | Rollback boundary |
| --- | --- | --- |
| Typed two-step contract, validation and fake conformance | 180–260 | New Core modeling types/tests, before consumers |
| Onshape read-only frame reader and WireMock parser/preflight failures | 220–340 | New reader/tests, before adapter registration |
| Onshape transformed shape/extrusion context and WireMock conformance | 240–360 | New Onshape context/tests |
| Inventor pinned frame reader/native translation and Windows boundary tests | 220–340 | New native seam/tests |
| Inventor context ordering, transforms and Windows conformance | 220–340 | New Inventor context/tests |
| Both-provider registration and compatibility regression/docs | 80–140 | Factory registrations and integration tests |

Total **1,160–1,780**, each estimated unit <=400. Shared pure transform validation can live beside PartModelingValidation without exposing native frames publicly or adding an engine. Unit estimates replace older 700–1,050 forecast; tests stay with behavior, no code-golf. Actual overages require an honest revised boundary/decision, not an inferred exception. This is a bounded design forecast only: dependencies exist, chain topology remains unselected, and no tasks/delivery are launched. Roll out registration only after both baselines are verified and the Onshape bridge/version/parser evidence closes; reverting software never deletes CAD artifacts.

### Historical phase readback

Required proposal/spec/config and both injected skills read directly; relevant builder/interface/Inventor source read directly, supplemented by explicitly parent-provided client source map. No CodeGraph or executable shell tool exists here, so no root command/index/graph/build/test/diff claim. Sources A5/O4/O5/O6 are parent-validated official evidence, not child fetches. Research is substantively partial only for the REST frame/datum bridge and bounded version/result extraction—not for missing child fetch grants, Inventor indexed axes, delivery permission, or Windows execution. Next: parent official-document extraction for those precise Onshape facts, then reconcile readiness; no new product interview or permission round. `skill_resolution: paths-injected`; no separate phase skill path supplied/discovered.

## Historical decision and readiness (superseded by scope reconciliation above)

Add typed contracts in `McpCad.Core` and target-bound adapters for **both Onshape and Inventor**. Each context permits exactly one rectangle/circle sketch followed by exactly one new-body extrusion. Do not introduce a stage list, plan parser, shared executor, public MCP tool, or replacement for the existing Onshape orchestrator.

**Corrective design pass ONE: design remains blocked on native frame evidence and delivery approval; dependent tasks must not launch.** Repository evidence supports the existing centimeter convention and detectable target checks, but does not yet establish canonical world-space frame parity. The pinned Inventor native seam is planned implementation work, not a missing design prerequisite. Windows/interop testing is a later platform verification constraint, not a demand for complete implementation proof now. Neither adapter may be omitted.

Authority: [preproposal](preproposal.md), [proposal](proposal.md), [specification](specs/provider-neutral-part-modeling/spec.md), and `openspec/config.yaml`, read directly. Confirmed AUTO/OpenSpec/ask-on-risk, 400 authored changed lines. No chain, exception, implementation, commits, publication, external research, or live CAD is authorized here. Scope explicitly follows this repository's `src/` C# projects, not `packages/coding-agent`.

## Verified current seams

References below identify existing symbols, not proposed APIs. Targeted reads were used because this executor has neither CodeGraph nor shell execution; Git root/index checks, graph analysis, builds and tests were not run.

| Existing file / symbol | Verified behavior and implication |
| --- | --- |
| `src/McpCad.Core/ICadWebProvider.cs` | `SketchReplace(CadElementAddress, IReadOnlyDictionary<string, object?>? = null)` and `FeatureAdd(CadElementAddress, string, IReadOnlyList<Dictionary<string, object?>>? = null, string? = null)` return dictionaries. `ElementGet`, `CaptureView(address, view, width, height, edges)`, `MicroversionGet` exist. No new broad interface member is needed. |
| `src/McpCad.Core/IMechanicalCadProvider.cs` | `SketchCreate(string = "XY")`, `SketchRectangle(double,double,double,double)`, `SketchCircle(double,double,double,string? = null)`, `SketchProfiles()`, and `Extrude(string,double,string = "positive",double = 0,string = "new_body")` return dictionaries. There is no explicit sketch-object extrusion parameter. |
| `src/McpCad.Inventor/InventorProvider.cs` | Those methods delegate to private `SketchManager` and `FeatureManager` instances sharing `InventorDriver`; changing an adapter's private manager would not change the provider's active-sketch field. |
| `src/McpCad.Inventor/Managers/SketchManager.cs:20–26,80–109,338–351,394` | `PlaneMap` is **XY=1, XZ=2, YZ=3**; `SketchCreate` uses `WorkPlanes[planeIndex]`, `Sketches.Add(workPlane)`, stores a private active sketch, returns `sketch_name`. Circle uses native `CreatePoint2d`/`AddByCenterRadius`; rectangle is available. There is no explicit world-to-sketch frame mapping in this path. |
| `src/McpCad.Inventor/Managers/FeatureManager.cs:18–34,61–118,1231–1325` | New body=20485; positive=20993, negative=20994. Extrude casts to `Inventor.PartComponentDefinition`, calls `CreateExtrudeDefinition`, `SetDistanceExtent(distance,dirEnum)`, `Add`. `GetActiveSketch` chooses the **last sketch**, not the sketch returned by SketchManager. `ResolveProfile` merges intersections and may delete/recompute profiles. |
| `src/McpCad.Inventor/Helpers/ProfileResolver.cs` | `Resolve(dynamic, CancellationToken = default)` may run AddForSolid on Task.Run, then fall back to AddForSurface after timeout. This is not a strict solid-only, no-fallback execution boundary. Do not reuse it for the portable floor. |
| `src/McpCad.Onshape/Builders/OnshapeSketchBuilder.cs:14–27,575–636` | `BuildRectangle` accepts meters and normalizes corners. `BuildCircle` exists. `BuildSketch` maps Top→TopplaneOp/JDC, Front→FrontplaneOp/JCC, Right→RightplaneOp/JEC; null defaults to Top. It emits sketchPlane queries and `rejectMicroversionSkew=true`, not explicit world-space axes. Adapter always supplies the plane. |
| `src/McpCad.Onshape/Builders/OnshapeFeatureTemplates.cs:12–24` | `ExtrudeFromSketch(string,double,string = "positive",string = "new")` accepts meters, normalizes depth/direction, queries the supplied sketch ID, emits new-body intent. Adapter validates first so normalization cannot hide invalid input. |
| `src/McpCad.Onshape/Managers/PartStudioManager.cs:41–112` | `ElementGet` resolves element and updates driver metadata; `SketchReplace` POSTs feature JSON, checks feature failure, normalizes response and updates microversion. Reads and writes are synchronous wrappers around client calls. |
| `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanExecutor.cs` | `Execute(ValidatedOnshapePartPlan)` calls SketchReplace, binds native IDs, calls FeatureAdd with meter extrusion, stops modeling after failure; capture errors are independent. This class and its richer operations remain unchanged. |
| `src/McpCad.Server/ProviderBootstrap.cs`; `ProviderBootstrap.Inventor.cs` | `RegisterCadProvider` uses Provider:Active, default inventor; non-Windows rejects inventor. Onshape registers ICadWebProvider and NullMechanicalCadProvider; Windows Inventor registers IMechanicalCadProvider and ICadProvider aliases. Tool families remain separate. |
| `Directory.Build.props`; `tests/McpCad.Tests/McpCad.Tests.csproj` | Linux tests only net8.0; Windows also net8.0-windows. Inventor project/interop references are conditional on the Windows TFM, with Inventor 2027 v30 DLL path. Server uses WINDOWS while tests define MCP_CAD_WINDOWS. |

## Portable contract and bounded data flow

Proposed names below are new. Keep cross-assembly contract types public CLR types but internal to product usage (no MCP attributes or public JSON schema).

- `CanonicalPlane { Top, Front, Right }`, `LengthUnit { Millimeter, Centimeter, Meter }`, `ExtrusionDirection { Positive, Negative }`; reject undefined enum values. Units are mandatory, with no native/document default.
- Immutable `Point2`, closed `RectangleProfile` (opposite corners), `CircleProfile` (center/radius), `SketchDefinition(Plane, Unit, Profile)`, and `ExtrusionDefinition(Depth, Unit, Direction)`. No boolean-operation field: this boundary always means solid new body, zero taper, one-sided finite positive depth.
- `IPartModelingContext`: `Capabilities`, `CreateSketch(SketchDefinition, CancellationToken)`, `ExtrudeNewBody(SketchReference, ExtrusionDefinition, CancellationToken)`, and a typed `Result` snapshot. This context is one execution, not a reusable modeling session. A fresh context requires an explicit caller action; no automatic resume.
- `SketchReference`/`FeatureReference` are opaque identity objects. Keep native handles in the adapter's instance-owned map; lookup requires identity issued by that context, operation kind, and successful preceding sketch. Do not expose a constructor accepting native IDs or compare handles by user-visible strings. Foreign-provider, foreign-context, fabricated, consumed and unresolved references fail before native calls.
- Result contains exactly two ordered operation slots (Sketch, Extrusion), each with `NotAttempted`, `Succeeded`, `Failed`, or `Uncertain`, typed error code/detail and optional opaque reference. Add mutation disposition `None`, `Present`, `Unknown`: a known created empty sketch can survive a later profile error without being misreported as successful closed geometry. No arbitrary stage collection API.

State is `Ready → SketchSucceeded → Completed`; any modeling failure or uncertainty latches `Stopped`. Before each mutation check state, inputs, reference and detectable target validity. Calling extrusion on a stopped context returns its unchanged snapshot: no provider calls and the extrusion remains NotAttempted. Invalid extrusion after a successful sketch preserves that success and records extrusion Failed/None. Sketch failure leaves extrusion NotAttempted. Repeated completed operations never replay writes.

Validate all finite coordinates, unequal rectangle axes, positive finite radius/depth, defined enums and finite/nondegenerate converted values. Normalize opposite rectangle corners explicitly. Cancellation is checked before native calls and between Inventor subcalls; synchronous REST/COM calls cannot promise in-flight cancellation. Cancellation after a dispatched call does not prove non-mutation.

## Geometry mapping: established facts versus blockers

The portable desired frame convention is right-handed and origin-centered: Top `(u=+X,v=+Y,n=+Z)`; Front `(u=+X,v=+Z,n=-Y)`; Right `(u=+Y,v=+Z,n=+X)`. Positive extrusion follows n, negative follows -n. These are **proposed contract meanings**, not assertions that existing native sketch frames already match.

| Meaning | Onshape | Inventor |
| --- | --- | --- |
| Plane identity | Verified builder operation/deterministic IDs above | Existing labels/index map above is source truth, **not verified native datum geometry**. Do not assume Top→XY→index 1 proves XY geometry. |
| Coordinates | Builders consume meters; convert mm ×0.001, cm ×0.01, m ×1 once | `docs/providers.md` explicitly documents Inventor tool centimeters; provider/manager calls pass values unchanged into COM. Preserve that repository contract: mm ×0.1, cm ×1, m ×100. This is repository-supported boundary behavior, not independently verified Autodesk database-unit documentation. |
| Depth / direction | Pass positive meter depth and explicit positive/negative to ExtrudeFromSketch | SetDistanceExtent with explicit positive native distance and 20993/20994, after verified frame sign mapping. |
| New body | Explicit template `"new"`, emitted NEW (existing regression asserts this) | Explicit PartFeatureOperationEnum 20485 and solid profile; never inherit join/default behavior. |
| Orientation | Builder identifies canonical plane but contains no basis transform | Sketches.Add inherits native frame; current code does not resolve u/v/normal. |

**Remaining design unknown:** native datum origins, basis vectors and normal signs for all three planes, including Inventor datum-index identity. Corrective repository searches covered source, tests/fixtures, docs and OpenSpec for unit terminology, WorkPlanes, world/sketch transforms, canonical IDs and plane normals. `docs/providers.md:11,39` establishes the existing cm boundary; no new native-unit convention is needed. This does not certify Autodesk database units independently. `tests/McpCad.Tests/Onshape/Fixtures/bodydetails_topology.json` has a face/circle normal `[0,0,1]`, but no canonical-plane ID or originating sketch frame; it cannot establish Top/Front/Right bases. Existing builder/validator tests exercise plane propagation and units, not world-space frame correspondence. The search found no evidence sufficient to close that correspondence; this is a bounded search result, not an assertion that no repository file could contain it.

If native frames differ, map coordinates and flip extrusion direction explicitly. A signed permutation/reflection suffices only if evidenced; otherwise use an explicit planar transform in the narrow native seam. Do not alter public SketchCreate or invent datum index corrections. **Human-actionable decision:** permit a narrowly scoped official Onshape/Autodesk documentation investigation for canonical datum identity, sketch basis/normal orientation and, as corroboration, numeric COM length units; alternatively supply authoritative repository fixtures/documentation for those facts. External research remains unselected and was not performed. No further product geometry interview is needed.

## Adapter-owned targets and native execution

### Onshape

`OnshapePartModelingAdapter.Bind(CadElementAddress)` returns a fresh context; addresses never enter shared geometry. Inject the existing `OnshapeDriver` alongside ICadWebProvider: `PartStudioManager.ResolveWvm` uses its mutable `DefaultWvm`, which defaults to `"w"`; `PartStudioModelingWireMockTests.ElementGet_UsesVersionAddressing_WhenDriverDefaultIsVersion` verifies `"v"`. Reject any mode other than `"w"` and nonempty-component validation failures before writes; recheck mode before each write without changing driver state. `ElementGet` detects an absent exact element (verified by `ElementGet_WhenExactElementIsMissing_ThrowsProviderException`); reject a returned non-PARTSTUDIO element type when available and fail preflight on unsuccessful lookup. `CadElementAddress` does not encode mode itself. These are **detectable invalid-target checks**, not proof of all access permissions, read-only conditions or race freedom. Do not add a universal writability guarantee or block design on one. A server rejection after dispatch is handled through the existing failure/uncertainty policy. Adapter factory registration passes the same driver used by its provider.

Flow: validate → ElementGet preflight → build rectangle/circle meter JSON → SketchReplace → normalize successful native sketch ID → issue opaque reference → validate extrusion/reference → ExtrudeFromSketch(native ID, meters, explicit direction, "new") → FeatureAdd. Preserve returned nonblank IDs exactly, including whitespace within an opaque ID. Missing ID after a write is Uncertain/Unknown, not a safe validation failure. Neither provider failures nor HTTP feature errors prove absence of a native feature; classify conservatively unless response evidence proves otherwise. No automatic client retry may be installed by this registration.

### Inventor

Do **not** compose only `SketchCreate` + drawing + `Extrude("1",...)`: extrusion selects the last sketch, numeric profile resolution mutates profile collections, and target identity is not checked. The smallest justified extension is a provider-local `IInventorPartModelingNative` seam, implemented by `InventorPartModelingNative`, bound to the existing driver. It is not a member of IMechanicalCadProvider or ICadProvider.

This seam pins the active **part** document/component and exact created sketch/profile objects in its context. It creates one canonical sketch and one closed shape, obtains one solid profile directly once, then creates an extrusion from that exact profile. Reuse proven manager call patterns for Sketches.Add, SketchLines/SketchCircles and ExtrudeFeatures, not their active-last-sketch or profile fallback helpers. Never call SketchProfiles (intersection merging/profile deletion is not a read-only validation), ProfileResolver, compensating Delete, or AddForSurface. Native handles remain inside Inventor assembly/context.

Before each subcall, verify the pinned active part document has not changed and the handle still belongs to it. Invalid/no document or assembly is rejected before sketch mutation. Document closure/change between calls stops execution; no auto-open/new/save. This detects changes, not a concurrency lock; concurrent external CAD edits remain outside guarantees. COM exceptions after dispatch are uncertain, and partial sketch construction records the known created artifact plus uncertainty. Execute on the established caller COM boundary, not Task.Run; threading/apartment expectations need a Windows mock-boundary test before claiming support.

`InventorDriver.InventorApp` auto-connects on access; context binding must not silently launch CAD or create a document. No portable implementation may advertise successful binding merely because Health returned a dictionary. Keep platform guards and native installation requirements.

## Optional capabilities

For the smallest implementation, both adapters explicitly declare portable evidence and revision metadata **Unsupported**. Existing Onshape CaptureView/MicroversionGet remain usable through existing public tools; their existence does not advertise portable support. Typed optional result distinguishes NotRequested, Unsupported, Succeeded and Failed; absence of payload never fabricates evidence or metadata. No image payload or revision is required in geometry.

If evidence is implemented later, it runs only after completed modeling, errors stay in the evidence slot, and modeling stays successful. A fake capability-supporting test context must prove that contract; actual adapters must pass unsupported/no-capture-call tests. Revision values would be provider-tagged opaque metadata, never a cross-provider comparison, expected revision, or concurrency guarantee. No optional implementation is required for this change.

## Exact proposed file boundaries

| File | Change responsibility |
| --- | --- |
| `src/McpCad.Core/Modeling/PartModelingContracts.cs` (new) | Geometry/enums, opaque references, two-slot results, optional capability status, IPartModelingContext; no target union or native objects. |
| `src/McpCad.Core/Modeling/PartModelingValidation.cs` (new) | Pure validation/unit normalization shared by both adapters; no generic executor/state scheduler. |
| `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` (new) | Address-bound context, fixed state transitions, builder calls, dictionary-to-typed normalization. No dependency on Tools. |
| `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` (new) | Fixed context state, portable translation, scoped reference ownership, partial/uncertain results. |
| `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` (new) | Narrow native interface plus COM implementation; pinned document/sketch/profile and explicit solid new-body calls. Native frame mapping remains gated above; pinned-handle implementation is ordinary planned work. |
| `src/McpCad.Server/ProviderBootstrap.cs`; `ProviderBootstrap.Inventor.cs` | Register only the configured concrete adapter factory using existing provider/driver services. Adapter contexts are created per invocation, never singleton mutable sessions. No targetless IPartModelingContext singleton. |
| `tests/McpCad.Tests/Modeling/PartModelingConformanceTests.cs` (new) | Shared behavior matrix/test fixture used with both adapters; validation, ownership, ordering, uncertainty, capabilities. |
| `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` (new) | Local REST preflight/write contract, IDs, payload units/plane queries, errors/no retries. |
| `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs` (new) | Windows-conditional native seam/fakes, exact profile binding, COM call arguments and partial failures. |
| `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs` (new) | Single configured factory, unchanged tool registration, Linux rejection and Windows selection. |

Leave existing provider interfaces, public tools, builder semantics, manager public methods and Onshape executor unchanged. CLR factory resolution is the internal integration point; no current public tool is rerouted through the smaller floor. This is intentional additive infrastructure, not an unadvertised new user workflow.

## Behavior-first verification

Strict TDD: failing behavior test → smallest implementation → triangulate → refactor; keep tests with their behavior. No tests were executed in this design phase.

1. Run the same 2 profiles × 3 planes × 3 units × 2 directions matrix for both adapters, including translated non-origin/asymmetric coordinates. Assert native geometry/frame meaning against independently established fixtures, not a test that repeats the adapter's map. Equivalent 20 mm/2 cm/0.02 m depth must agree; check negative direction and explicit new body with an existing body present in a fake target.
2. Invalid enums, NaN/infinity, zero/negative depth/radius, degenerate/overflowed geometry, absent target, detected non-workspace mode/wrong element type or assembly target cause zero corresponding mutation calls. Do not assert detection of undisclosed read-only permissions or races. Test post-conversion collapse as well as input validity.
3. Verify returned native sketch ID/object is exactly the extrusion source even if another sketch exists. Reject cross-provider/cross-context/fabricated/reused references; active-document change must stop before the next subcall.
4. Inject failure before and after every native boundary: sketch creation, profile drawing/resolution, extrusion. Assert successful prior slot, known partial sketch, uncertain dispatched write and NotAttempted later slot; zero retry/delete/rollback/fallback/surface/extra write. Exercise cancellation before and between calls without claiming interruption of dispatched COM.
5. Both production adapters report unsupported evidence/revision with no capture/revision calls. A capability-supporting fake proves capture failure cannot rewrite modeling success.
6. WireMock uses localhost only and asserts GET preflight then exactly sketch POST then extrusion POST, explicit plane IDs, meters, direction, NEW and sketch feature linkage. Cover missing IDs, feature error, transport error and malformed response.
7. Preserve `tests/McpCad.Tests/Tools/OnshapePartPlanExecutorTests.cs` and `tests/McpCad.Tests/Onshape/OnshapePartOrchestratorWireMockTests.cs`: declaration order, opaque IDs, conversion, capture continuation, no retry/rollback and public completed_with_capture_failures semantics. Run existing broader tool/builder regressions to protect polygon/mixed and add/remove behavior outside this floor.
8. Registration tests must construct services without credentials/network/COM activation. net8.0 tests prove Onshape selection, default/unknown selection compatibility and non-Windows Inventor rejection; net8.0-windows runs the same conformance matrix for real Inventor adapter with fake native seam plus isolated COM translation tests. A Linux-only green run is not two-provider verification.

Commands when implementation is separately authorized: `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore`; on configured Windows additionally `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0-windows --no-restore`; `dotnet build src/mcp-cad.sln`. Interop installation/TFM constraints must be reported rather than bypassed. Mock tests prove translation and boundaries, not live CAD certification.

## Alternatives and risk decisions

| Alternative | Decision |
| --- | --- |
| Generic portable plan/stage engine | Rejected: unnecessary scheduling/parser surface. One bounded two-operation context satisfies ordering/results and explicit reference validation. |
| One opaque Execute(sketch,depth) call | Smaller but cannot directly exercise caller-supplied foreign/sketch reference rejection. Two methods on a one-shot context are the minimum useful reference seam. |
| Extend broad mechanical interface / reuse latest sketch | Rejected: breaks implementers or silently selects wrong sketch. Provider-local pinned native seam is narrow and testable. |
| Fix Inventor global PlaneMap in this change | Rejected: source does not prove native ordering; may change public behavior. Resolve/transform in additive path after evidence. |
| Move existing executor/builders to Core | Rejected: cloud JSON/address coupling and unnecessary compatibility risk. Keep only portable values/validation in Core. |
| Implement optional capture/revision now | Deferred: unsupported is honest and smaller; existing Onshape behavior remains available. |
| Ship Onshape first as completed scope | Rejected: both adapters mandatory; capability gaps block implementation. |

## Review forecast, rollout and rollback

Estimated authored additions plus deletions (not measured): contracts/validation with shared tests **180–260**; Onshape behavior with WireMock **180–270**; Inventor native/context behavior with Windows tests **280–420**; selection/regression integration **60–100**. Total **700–1,050**, excluding this planning artifact. Native frame resolution could increase it. This exceeds 400 without code-golf or weakened tests.

Independently reviewable suggested work units are: (1) typed two-step semantics and validation with fake conformance; (2) Onshape concrete binding with local REST tests; (3) Inventor pinned solid-profile binding with Windows tests; (4) configured adapter availability and compatibility tests. Each has behavior/tests together and can be removed independently before registration; their dependencies do not authorize separate delivery. These are review suggestions, **not a selected split/chain or permission to commit**. Ask-on-risk requires a human delivery decision before implementation; no size exception is inferred.

Rollout is additive factory registration only after both required-floor adapters and platform tests pass and evidence blockers close. No public migration/feature narrowing. Software rollback removes factory registration and new adapter/contracts files with their tests, restoring existing service paths; it never deletes CAD artifacts. Failures may leave geometry requiring explicit operator assessment. No retries, resume, save, compensation or rollback of native mutations.

## Phase readback

Only this design artifact is authored. Required input artifacts/config and the two injected skills were read directly. `skill_resolution: paths-injected` for cognitive-doc-design and work-unit-commits; no separate phase-skill path was injected and no registry discovery was performed. Native status supplied by parent recommends design with no launch blockers; this phase does not recalculate status or start tasks. Corrective pass ONE separates: (1) unresolved native frame/datum facts requiring authoritative evidence or permission for scoped official-doc research; (2) planned narrow pinned-native implementation, not a prerequisite proof; (3) Windows/interop execution as a later verification constraint. Existing centimeter semantics and detectable Onshape target checks are now grounded in additional repository evidence. Remaining human decisions are grouped research/evidence permission and delivery strategy for the unchanged 700–1,050-line estimate against 400. No split/exception is inferred; tasks remain held.
