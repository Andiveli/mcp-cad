# Exploration: ICadWebProvider — Onshape Integration for mcp-cad

**Change name**: `onshape-provider`
**Mode**: hybrid (Engram + openspec filesystem)
**Project**: mcp-cad
**Date**: 2026-06-27

## Current State

### Repository facts (verified from the working tree)

The mcp-cad solution is a **.NET 8 / C#** project that exposes Autodesk Inventor as MCP tools over stdio. Layout:

- `src/McpCad.Core/` — provider interfaces + exception types + `ErrorResult` helper. Target `net8.0`, no dependencies. Pure abstraction layer.
- `src/McpCad.Inventor/` — only `McpCad.Inventor.csproj` is `net8.0-windows` (binds a hard path to the Inventor 2027 COM interop DLL). Implements `IMechanicalCadProvider` via the `InventorProvider` class, which composes 7 managers (Sketch, Feature, Document, Parameter, Property, Export, WorkFeature, Assembly) all fed by a single `InventorDriver`.
- `src/McpCad.Server/` — `net8.0-windows` exe. `Program.cs` does the DI wiring, `appsettings.json` carries the `Inventor:AutoConnect` flag.
- `src/McpCad.Tools/` — `net8.0`, references `ModelContextProtocol` 1.3.0. Holds `AtomicTools` (~50 MCP tools) and `SkillTools` (~25 composed skills). Both depend on `IMechanicalCadProvider` via primary-constructor injection.

### Interface contract (the thing we must design against)

Two interfaces in `McpCad.Core`:

1. **`ICadProvider`** — 13 methods across Connection (3) / Documents (6) / Export (4). All return `Dictionary<string, object?>`.
2. **`IMechanicalCadProvider : ICadProvider`** — adds Sketch (20), Features (20), Parameters (4), Properties (5), WorkFeatures (3), Assembly (15) — total ~67 methods. All sync, all returning `Dictionary<string, object?>`.

The result-dict envelope is **`{ "success": bool, ... }`**. Errors are **never thrown out of provider methods** — they are caught at the manager layer and converted via `ErrorResult.Create` or `ToolHelpers.Error` into the standard envelope `{ "success": false, "error": "..." }`. The tool layer (`AtomicTools.Catch` / `SkillTools.Catch`) re-catches only the **typed** exceptions (`InventorConnectionException`, `InventorComException`) and the generic fallback, so a leaky exception becomes `{ "success": false, "error": "Unexpected error: …" }`.

### DI shape (current)

```csharp
builder.Services.AddSingleton<InventorDriver>();
builder.Services.AddSingleton<IMechanicalCadProvider, InventorProvider>();
builder.Services.AddSingleton<ICadProvider>(sp => sp.GetRequiredService<IMechanicalCadProvider>());
```

Both tool classes are registered into MCP via `.WithTools<AtomicTools>().WithTools<SkillTools>()`. There is **no** provider abstraction over the wire — the server always boots one CAD backend, chosen at startup, and tools are statically bound to `IMechanicalCadProvider`.

### Critical observation: the existing `cad-provider-protocol` spec is Python

`openspec/specs/cad-provider-protocol/spec.md` describes `mcp_cad/core/protocol.py` with `CADProvider` ABC. This is a **Python** spec — it does NOT match the actual current C# interfaces. The spec is aspirational/legacy from the earlier Python project. The **real, current** code paths in `src/McpCad.Core/ICadProvider.cs` and `IMechanicalCadProvider.cs` are the source of truth for this exploration. Any new C# work must extend the C# interfaces, not the Python ABC.

### Onshape API surface (verified via Context7 / Onshape OpenAPI)

- **Base URL**: `https://cad.onshape.com/api/v16` (note: `v16` is current; the spec uses URL placeholders `{baseUrl}` for enterprise tenants).
- **Auth**: two flows — **API key (HMAC-SHA256 signed)** or **OAuth2**. API key path: build a signature over `METHOD\nNONCE\nDATE\nCONTENT-TYPE\nPATH\nQUERY\n`, lowercased, then send `Authorization: On <accessKey>:HmacSHA256:<base64-hmac>`. `nonce` ≥ 16 chars, `authDate` is the UTC string. Either is fine for our scenario; API key is the simplest.
- **Document model**: documents have `Workspaces` (w), `Versions` (v), and `Microversions` (m). Every Part Studio / Assembly / FeatureStudio is an `element` inside a document, identified by `eid`. Geometry IDs are versioned against `microversionId` — you must send `rejectMicroversionSkew: true` on every write to avoid races.
- **Create document**: `POST /documents` body `{ "name": "..." }` → returns `id`. Documents are born empty — to model a part you then `POST /partstudios` to add a Part Studio element.
- **Sketches**: `BTMSketch` lives inside a Part Studio. Sketches are **declarative** — you build a `BTMSketch-151` payload with `entities` (BTMSketchCurve*, BTMSketchPoint) and `constraints` (BTMSketchConstraint) and POST to `/partstudios/d/{did}/w/{wid}/e/{eid}/sketches` to **replace** the active sketch. There is no incremental "add a line" — you rebuild the sketch payload.
- **Features**: feature creation is `POST /partstudios/d/{did}/w/{wid}/e/{eid}/features` with a `BTMFeature-134` array. Each feature has `featureType` (e.g. `extrude`, `fillet`, `chamfer`, `revolve`, `linearPattern`, `mirror`, `boolean`, `shell`) and a `parameters` array of `BTMParameter-1` (`parameterId` + `valueString`). For sketches embedded in features, references are by sketch `featureId` + entity id. The whole payload uses `FeatureScript` semantics under the hood.
- **Exports**: STL is **synchronous** (`GET /parts/d/{did}/w/{wid}/e/{eid}/partid/{partid}/stl` → 307 redirect). STEP is **asynchronous** (`POST /assemblies/.../export/step` returns a translation `id` you must poll via `GET /translations/{tid}` until `requestState` is `DONE`, then download). Units: `METER | CENTIMETER | MILLIMETER | INCH | FOOT | YARD`.
- **Rate limits**: API-key calls count against an **annual** quota per company (proportional to seats). 4xx/5xx don't count. 402 once exhausted.
- **Units**: Onshape documents default to **inches** for length, **radian** for angle unless you change the document's default. We must convert at the provider boundary.

### Critical gap: sync vs. async

Every method on `IMechanicalCadProvider` is **synchronous**. But Onshape exports are async, microversion tracking is async-poll, and even `BTMSketch` replacement is a fire-and-forget HTTP POST whose result needs polling for geometry IDs. If we keep the sync contract, our provider must internally `block` on `Task.Wait()` (or `GetAwaiter().GetResult()`) — acceptable, but blocks an MCP tool thread. Alternative: introduce a second interface `ICadWebProvider` with `Task<...>` methods and have the tools themselves be `async` — this is a much bigger ripple through `AtomicTools` / `SkillTools`. For the proposal, treat the sync-via-`.GetAwaiter().GetResult()` shape as the default; revisit async tools in a follow-up change.

## Affected Areas

- `src/McpCad.Core/ICadProvider.cs` — base interface. May need a new `IWebCadProvider : ICadProvider` sibling (not extension).
- `src/McpCad.Core/IMechanicalCadProvider.cs` — left **untouched**; Onshape does not implement this.
- `src/McpCad.Core/Exceptions/` — add `OnshapeApiException` and `OnshapeAuthException` next to the existing `InventorComException` / `InventorConnectionException`. The existing names are "Inventor" prefixed; the spec at `openspec/specs/cad-provider-protocol/spec.md` already calls for generic `CADError` / `CADConnectionError` — we should at least add a generic base `CadProviderException` so the tool layer's `catch (Exception)` can keep one shape.
- `src/McpCad.Core/Models/ErrorResult.cs` — already generic; reuse as-is.
- `src/McpCad.Server/Program.cs` — needs a provider-resolution switch (`Provider:Active = "inventor" | "onshape"`) and registration of `OnshapeProvider` + `OnshapeClient`. Optionally register both behind an `IEnumerable<IMechanicalCadProvider>` plus a default-selected one.
- `src/McpCad.Server/appsettings.json` — add `Onshape` section (base URL, access key, secret key, default document template id, default unit system).
- `src/McpCad.Tools/AtomicTools.cs` + `SkillTools.cs` — primary-constructor depends on `IMechanicalCadProvider`. **Two options** (see Approaches). The minimal change is to keep them on `IMechanicalCadProvider` and have the Onshape provider implement that interface **as well as** the new `ICadWebProvider` (the contract gets faked via sync wrappers around HTTP).
- **New project** `src/McpCad.Onshape/` — Onshape HTTP client (`HttpClient` + HMAC signer), `OnshapeProvider` implementing the chosen interface set, manager classes mirroring the Inventor layout, `OnshapeDriver` for connection lifecycle.
- `src/mcp-cad.sln` — add the new csproj.
- `openspec/specs/cad-provider-protocol/spec.md` — keep the Python spec for reference but **do not** modify it in this change. The C# reality diverges and the C# interfaces are the source of truth.

## Approaches

### Approach A — `IWebCadProvider : ICadProvider` (sibling, NOT extending `IMechanicalCadProvider`)

Define a **sibling** interface `ICadWebProvider` in `McpCad.Core` that extends only the common `ICadProvider`, plus web-specific operations: `DocumentList`, `DocumentCreate`, `DocumentOpenById`, `DocumentSetActiveElement`, `UnitSystemGet/Set`, `ExportStepAsync` (the polling wrapper), and the same `SketchCreate/...` shape but with **element-id parameters** instead of "active sketch" implicit state. Onshape implements it; it does not implement `IMechanicalCadProvider`.

Tools are split: existing `AtomicTools` / `SkillTools` keep their `IMechanicalCadProvider` dependency for Inventor. New `OnshapeAtomicTools` / `OnshapeSkillTools` depend on `ICadWebProvider`. The server registers **one** toolset per the active provider; the other set is not registered. This requires a tiny provider-aware factory in `Program.cs`.

- **Pros**:
  - Cleanly honors the user's "ICadWebProvider is NOT IMechanicalCadProvider" decision. Web CAD really is a different domain (document/element identifiers, unit conversion, microversion, async exports).
  - No leaky abstractions: Inventor-only concepts (COM `InventorDriver`, implicit active sketch, cm units) stay out of the web interface.
  - Future-proofs Fusion 360 and 3DExperience — they will also implement `ICadWebProvider`, not `IMechanicalCadProvider`.
  - Tools can be provider-specific and honest about their semantics — e.g. `OnshapeSketchCreate(planeElementId)` is a real call signature, not a lie wrapped in `string plane = "XY"`.
- **Cons**:
  - **Two tool classes per provider** = duplication of the 50+ atomic tool surface. Mitigation: extract a `ToolNaming` helper for the prefix (`onshape_` / `inventor_`) and a shared envelope; keep method bodies independent.
  - Server must choose provider at startup, not per-call. Acceptable — different CAD backends are very different worlds.
  - If the user later wants both providers running side by side, we'll need an "active provider" selector tool. Defer.
- **Effort**: **High** (4–6 weeks). New project, ~50 tool methods × provider, HMAC signer, microversion handling, async export polling, unit conversion, plus a fixture/mock server for tests.

### Approach B — `IWebCadProvider` extends `IMechanicalCadProvider`, Onshape implements both

Same interface shape as Inventor (line, circle, "active sketch" string, cm units), but mapped to Onshape internally: the Onshape provider maintains an in-memory "active sketch buffer" and flushes it as a `BTMSketch` payload on the first feature call. Implicit unit conversion inches ↔ cm at the boundary.

- **Pros**:
  - **Zero tool changes.** `AtomicTools` and `SkillTools` are reused as-is.
  - One tool class, one MCP server, one set of names.
  - Fastest path to "Onshape works".
- **Cons**:
  - **Violates the user's stated design decision** — they explicitly said `ICadWebProvider` should NOT extend `IMechanicalCadProvider`. Doing it anyway builds technical debt the user told us not to build.
  - Fakes the implicit "active sketch" state. Onshape has **no** active-sketch concept; sketches are elements identified by id. The buffer is a leaky abstraction that will bite on multi-sketch workflows (assemblies referencing two sketches).
  - Hides unit conversion inside the provider — tools stay inches-naive, errors get harder to diagnose when a number comes out 2.54× off.
  - Forces Onshape into the same sync envelope. Real Onshape sketches (replacing the whole payload on every edit) and async exports make the "active sketch" lie especially expensive.
  - Locks out Fusion 360 / 3DExperience later — they'll also be web CAD, and they will refuse to implement a fake `IMechanicalCadProvider`.
- **Effort**: **Medium** (2–3 weeks). Reuses tools, so the time is in the provider + signer + sync wrappers.

### Approach C (hybrid) — sibling interface, but tools get a strategy

A middle ground: `ICadWebProvider` is a sibling. Tools are split by provider, but the **method bodies** are 90% shared. Extract a `CadToolCatalog` static class per provider that maps the same logical operation (sketch_line, extrude, etc.) to the right provider method, and let two thin tool classes dispatch through it.

- **Pros**:
  - Honors the design decision (sibling interface, no `IMechanicalCadProvider` extension).
  - Some deduplication of the 50+ tool surface.
  - Each provider still owns its semantics.
- **Cons**:
  - Premature abstraction. With one web provider and one COM provider, the shared `CadToolCatalog` will inevitably favor one shape. Adds a layer that's hard to undo later.
  - Defers the real decision: when Fusion arrives, will the catalog re-fork? Probably yes.
  - More moving parts than A and a weaker payoff than A.
- **Effort**: **High+** (5–7 weeks). New project + tool class per provider + a shared catalog that doesn't quite work.

## Recommendation

**Pick Approach A** (`ICadWebProvider : ICadProvider` sibling, separate Onshape tool classes, provider chosen at server startup).

Why:

1. **The user explicitly told us to.** They were clear that "the future is web CAD providers" and that conflating web and COM under `IMechanicalCadProvider` will block Fusion 360 / 3DExperience later. Honoring a stated architectural decision is the cheapest thing we can do.
2. **The interface mismatch is real, not cosmetic.** Onshape's `did/wvm/wvmid/eid` address scheme and microversion semantics cannot honestly be expressed as `SketchCreate(string plane = "XY")`. We'd be smuggling a fake "active sketch" buffer through the seam, and the buffer will be wrong as soon as a user opens two sketches in a session.
3. **Duplication of the 50+ tool surface is bounded.** It looks scary on paper, but the Onshape implementations share a common `OnshapeDriver` (HMAC + HttpClient) and a small set of helpers (geometry → FeatureScript value string, unit conversion). Most of the duplication is one-line delegations.
4. **Approach B's "win" is short-lived.** Saving 2–3 weeks now costs us 3–4 weeks of refactor when the second web provider arrives, plus the indignity of telling the user "we did the thing you told us not to do".
5. **Approach C is the worst of both worlds** — the abstraction layer is paid for now and the duplication is paid for later.

Concretely, the proposal will deliver:

- `McpCad.Core/ICadWebProvider.cs` — sibling interface, ~25 methods (12 from `ICadProvider` + 13 web-specific: list/create/open docs, element addressing, units, microversion, async export wrapper).
- `McpCad.Core/Exceptions/OnshapeApiException.cs` + `OnshapeAuthException.cs` + a new `CadProviderException` base class so the tool layer's `catch` can stay clean.
- New `McpCad.Onshape/` project: `OnshapeClient` (HttpClient + HMAC signer), `OnshapeDriver`, `OnshapeProvider : ICadWebProvider`, 5 managers (Document, Sketch, Feature, Parameter, Export).
- New `McpCad.Tools/Onshape/OnshapeAtomicTools.cs` + `OnshapeSkillTools.cs`.
- `Program.cs` switch: `Provider:Active = "inventor" | "onshape"`. If `onshape`, register `OnshapeProvider` + `OnshapeAtomicTools` + `OnshapeSkillTools` and **skip** the Inventor ones.
- `appsettings.json` `Onshape` block (base URL, access key, secret key, default document name, default unit).
- Tests against a `WireMock`-style fake HTTP server (HMAC-signed requests, deterministic responses), so we don't burn the annual API quota in CI.

## Risks

- **Annual API quota** is the single biggest operational risk. Onshape counts every signed 2xx/3xx against the company-wide annual limit. An LLM agent in a loop that does sketch → extrude → save → re-open → sketch can burn 100+ calls per design iteration. We must (1) batch feature additions (`POST /features` accepts an array), (2) cache `featureId` results, and (3) expose a "dry-run" mode for the unit tests. Otherwise the user's first week on Onshape via mcp-cad will hit 402.
- **Microversion skew** — every write must pass `rejectMicroversionSkew: true`, and reads must be pinned to a known `microversionId`. Without this, a concurrent edit from the Onshape web UI will silently break the agent's geometry-id references. The provider must encapsulate this; tools must not see it.
- **Unit conversion is lossy at the boundary.** Inventor speaks cm; Onshape defaults to inches. A `0.1` cm sketch dimension becomes `0.03937…` inches. Floating-point drift in FeatureScript value strings can change feature parameters' hash. Convert at one place (provider boundary), use a fixed `CultureInfo.InvariantCulture`, and use `G9` format (Onshape's documented default) to keep the round-trip stable.
- **Sync wrapper over async Onshape** is a real cost. Long operations (large assembly export to STEP) will block an MCP tool thread for 10–60 s. With a single-threaded stdio MCP server, the user perceives this as a frozen server. Mitigation: keep the sync wrapper but route through a dedicated long-running host service, and document the latency. **Consider** an `async` follow-up that lifts the tool methods to `Task<>`.
- **API key in `appsettings.json`** is a credential leak risk. Mitigations: support `DOTNET_USER_SECRETS` or env-var fallback (`ONSHAPE_ACCESS_KEY` / `ONSHAPE_SECRET_KEY`), and never log the secret. Document this in the change's design.md.
- **C# dependency surface** — we will pull in `Microsoft.Extensions.Http` (for `IHttpClientFactory`) and possibly `Polly` (for retry on 5xx and rate-limit backoff). This is a change to the lean dependency profile of `McpCad.Core`, which currently has zero NuGet deps. Keep the Onshape-only deps inside `McpCad.Onshape` so the Core project stays pure.
- **Two provider stories in one solution** can confuse future contributors ("which interface do I implement?"). Add a top-level `docs/providers.md` (or extend `README.md`) that draws the line clearly: COM = `IMechanicalCadProvider`, web = `ICadWebProvider`. This is part of the design artifact.
- **Existing `cad-provider-protocol` spec is Python** and out of sync with reality. Don't touch it in this change — it would scope-creep a C# change into a Python migration. Note the divergence in the design.

## Open Questions for the User

Before proposal:

1. **Auth mode** — do you have an Onshape API key already, or should we wire OAuth2 too? API key is simpler and is what the exploration assumes.
2. **Default unit system** — keep the existing tools' cm/m/rad semantics and convert at the Onshape boundary (recommended), or expose Onshape-native units through the tools? The latter requires new tool parameters.
3. **Single-provider or multi-provider server** — at startup, the user picks one backend (simplest), or can both be alive and a tool switches between them? Recommendation: single-provider for v1.
4. **Sync vs. async tools** — accept the `.GetAwaiter().GetResult()` sync wrapper for v1 and revisit in a follow-up, or pay the async ripple now? Recommendation: sync for v1.

## Ready for Proposal

**Yes.** The exploration is complete enough to draft a focused `sdd-propose` run. The proposal should:
- Pick Approach A and explain why.
- State the 4 open questions above and recommend a default for each.
- Forecast the workload: 4–6 weeks, 400-line budget is **exceeded** → chained PRs (PR-1: Core interface + exceptions; PR-2: Onshape client + driver + HMAC; PR-3: OnshapeProvider + managers; PR-4: Tools + DI switch + appsettings + tests).
- Flag the annual API quota as the primary operational risk.

**Artifacts**: Engram `sdd/onshape-provider/explore` | `openspec/changes/onshape-provider/exploration.md`
**Next**: sdd-propose
**Skill Resolution**: paths-injected (skill-resolver, sdd-phase-common)
