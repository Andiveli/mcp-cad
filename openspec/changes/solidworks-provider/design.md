# Design: SolidWorks Provider (Provider-Agnostic Generalization)

**Change**: solidworks-provider  
**Phase**: sdd-design (executor)  
**Based on**: proposal.md (locked provider-agnostic + rename), specs/ (generalization, pluggable-server, solidworks-basic-loop, consolidated), engram #272 (detailed SW plan + sw-01.. challenges), #275 (decision), #273 (Strict TDD state), sdd-init #72 (multi-CAD from day 1), prior explore (tagging/selection diffs, COM, contract analysis), current codebase patterns.  
**Strict TDD**: Enforced throughout.  
**Delivery**: Chained/incremental per proposal (respect ~400 LOC/review budget).

## 1. Executive Summary & Architecture Overview
The design realizes a **provider-agnostic MCP surface** (single set of 80+ tools) with **config-driven backend selection** ("Cad:Provider"). Public rename is limited to the 3 connection tools; everything else was already neutral. Backward compatibility is absolute via strong `[Obsolete]` aliases + delegation. A single server binary hosts exactly one active `IMechanicalCadProvider` (Inventor default). McpCad.SolidWorks provides a skeleton + minimal managers for the basic modeling loop (connect → new part → sketch + entities + profiles → extrude → inspect via tree/image/bbox).

**Core unchanged**:
- `IMechanicalCadProvider : ICadProvider` (Core) — stable contract.
- Result envelopes (`{success, ...}` or `{success:false, error}`).
- `ToolHelpers.Catch` + generalized `CadComException` / `CadConnectionException` (with `[Obsolete]` Inventor* aliases already in Core).
- Tool layer delegation pattern.
- 9-manager structure in reference impl.

**Key diagrams** (mermaid + ASCII):

```mermaid
flowchart TD
  Client["MCP Client (cad_connect or inventor_connect alias)"] --> AtomicTools
  AtomicTools -- delegates via ToolHelpers.Catch --> IMechanicalCadProvider
  subgraph "Server (single active)"
    Config["Cad:Provider (appsettings / env)"]
    Config --> Program
    Program -->|Inventor (default)| InventorDriver
    Program -->|SolidWorks| SolidWorksDriver
    InventorDriver --> InventorProvider --> 9xManagers
    SolidWorksDriver --> SolidWorksProvider --> 4xMinimalManagers + stubs
  end
  InventorProvider & SolidWorksProvider -.->|implements| IMechanicalCadProvider
```

ASCII wiring (Program.cs startup):
```
if (provider == "SolidWorks") {
  services.AddSingleton<SolidWorksDriver>();
  services.AddSingleton<IMechanicalCadProvider, SolidWorksProvider>();
} else {
  ... Inventor ...
}
services.AddSingleton<ICadProvider>(sp => sp.GetRequiredService<IMechanicalCadProvider>());
... WithTools<AtomicTools>() ...  // neutral + aliases both visible
// auto-connect (provider-aware)
```

Provider delegation (identical for both):
```
SolidWorksProvider.Connect() => _driver.Connect()
SolidWorksProvider.DocNewPart(...) => _document.DocNewPart(...)
... (1:1 for all 80+)
```

## 2. Generalization of Public Surface (from generalization/spec.md)
### Exact Renames + Aliases in AtomicTools.cs
**Primary methods** (keep `[McpServerTool]` + `[Description]` here; these become the registered names):
- `inventor_connect()` → `cad_connect()`
- `inventor_disconnect()` → `cad_disconnect()`
- `inventor_health()` → `cad_health()`

**Descriptions** (CAD-neutral, mention examples in parens ok per spec):
- "Connect to the running CAD application (Inventor, SolidWorks, etc.)."
- "Disconnect from the CAD application without closing it."
- "Check CAD connection health and active document state."

**Deprecated aliases** (add after the primaries, or grouped):
```csharp
[McpServerTool, Description("Connect to the running CAD application (Inventor, SolidWorks, etc.). [DEPRECATED: use cad_connect + Cad:Provider]")]
[Obsolete("Use cad_connect (and set 'Cad:Provider' in config) instead. Aliases remain for backward compatibility during transition.")]
public Dictionary<string, object?> inventor_connect() => cad_connect();

... identical for disconnect + health (thin delegation, no logic dup) ...
```
- Aliases also carry `[McpServerTool]` so MCP framework sees both distinct methods (no collision).
- Behavior identical (including error envelopes via ToolHelpers.Catch).
- No impact on call sites inside AtomicTools (none exist for these 3).

**References to update in AtomicTools**:
- Only the 3 method defs + any header comments ("Connection (3)").
- Internal class XML/docs already lean neutral.

**Impact on other Tools files**:
- SkillTools.cs, MacroTools.cs, TemplateTools.cs: **Zero changes**. They inject `IMechanicalCadProvider` and call neutral methods (or macros compose doc_*/sketch_* etc.). No hard-coded inventor_connect in production paths (only in archived proposals/skills examples).
- ToolHelpers.cs: unchanged (already catches Cad*).

**Test strategy (Strict TDD — tests first)**:
- `AtomicToolsTests.cs`: 
  - Keep existing `InventorConnect_Delegates...` (they will continue to pass via alias).
  - Add parallel `CadConnect_DelegatesToProvider()`, `CadDisconnect_...`, `CadHealth_...` (assert WasCalled on provider).
  - Add alias verification tests: call `inventor_health()` after rename, assert success + CallLog hit; use reflection to assert `[Obsolete]` attribute present on alias methods + Description.
- `ToolRegistrationTests.cs`:
  - Extend `AtomicTools_HasExpectedMethodCount` (or add specific): assert both `cad_*` and `inventor_*` method names exist on type (via reflection + `GetMethods`).
  - Add test: DI + resolve AtomicTools, enumerate MCP tools (or attributes), confirm neutral names are primary in description/registration.
  - Update any DI_Registration_InjectsCorrectProvider calls to use neutral where new.
- `ErrorHandlingTests.cs`: Update a subset of `inventor_connect` calls to `cad_connect` (or keep both); theory tests already use types (CadConnectionException etc.) — tolerant of alias names in messages.
- New/expanded provider-agnostic contract tests (in McpCad.Tests): theory or base class exercising connection tools against any IMechanicalCadProvider (mocked). "Both name sets produce identical envelopes."
- Edge: Alias on SolidWorks provider still works (surface is provider-agnostic).
- Before any AtomicTools.cs edit: the new alias/rename tests must be written and green against current (pre-rename) code where possible, or added in same commit with production under TDD discipline.

**Docs/Skills**:
- README.md + docs/tools-reference.md: Connection table shows `cad_connect` etc. as primary. Add prominent "Migration from inventor_*" callout box: "Old names continue to work via deprecated aliases. Update new skills/prompts to cad_*. Configure backend with 'Cad:Provider':'SolidWorks'."
- Skills (inventor-new-part etc.): Leave as-is (legacy ok per spec/non-goal); optionally add comment/note. New neutral skills out of scope this increment.
- openspec specs already use neutral in scenarios.

## 3. Pluggable Server & Config (from pluggable-server/spec.md)
### Changes to McpCad.Server/Program.cs
Current (hard-coded):
```csharp
builder.Services.AddSingleton<InventorDriver>();
builder.Services.AddSingleton<IMechanicalCadProvider, InventorProvider>();
builder.Services.AddSingleton<ICadProvider>(sp => ...);
...
if (config.GetValue<bool>("Inventor:AutoConnect")) { var d = sp.Get<InventorDriver>(); d.Connect(); }
```

**New design** (provider-aware):
```csharp
var cadSection = builder.Configuration.GetSection("Cad");
string providerName = cadSection.GetValue<string>("Provider") ?? "Inventor";
providerName = providerName.Trim();

builder.Services.AddSingleton<IMechanicalCadProvider>(sp => { ... no, use conditionals });

if (string.Equals(providerName, "SolidWorks", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<SolidWorksDriver>();
    builder.Services.AddSingleton<IMechanicalCadProvider, SolidWorksProvider>();
}
else if (string.Equals(providerName, "Inventor", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(providerName))
{
    builder.Services.AddSingleton<InventorDriver>();
    builder.Services.AddSingleton<IMechanicalCadProvider, InventorProvider>();
}
else
{
    throw new InvalidOperationException($"Invalid Cad:Provider '{providerName}'. Valid: Inventor, SolidWorks");
}

builder.Services.AddSingleton<ICadProvider>(sp => sp.GetRequiredService<IMechanicalCadProvider>());
... MCP registration unchanged (tools are neutral) ...

// Auto-connect (after Build)
var cfg = app.Services.GetRequiredService<IConfiguration>();
bool auto = false;
if (string.Equals(providerName, "Inventor", ...))
    auto = cfg.GetValue<bool>("Inventor:AutoConnect");  // legacy honored
else if (string.Equals(providerName, "SolidWorks", ...))
    auto = cfg.GetValue<bool>("SolidWorks:AutoConnect") || cfg.GetValue<bool>("Cad:AutoConnect");

if (auto)
{
    // Resolve the concrete driver via the registered singleton (type or by interface if we add ICadDriver later)
    // Simple: use keyed or just if/else again (or register a factory)
    var driver = providerName == "SolidWorks" 
        ? (object)app.Services.GetRequiredService<SolidWorksDriver>()
        : app.Services.GetRequiredService<InventorDriver>();
    // Call Connect() via dynamic or common interface (add ICadDriver? rejected for minimal — use if for now or make drivers implement a small marker)
    // Preferred minimal: make both drivers have public Connect() (they do); use reflection or duplicate small block.
    // Cleaner future: extract interface, but for this: conditional resolve + ((dynamic)driver).Connect();
    if (driver is InventorDriver id) id.Connect();
    else if (driver is SolidWorksDriver sd) sd.Connect();
}
```

**Alternative clean pattern (recommended in design)**: Register the active driver under a key or as `ICadDriver` (new small interface in Core? minimal change — or just live with the if). Keep simple conditional blocks to avoid new contracts. Document.

**Legacy support**:
- "Inventor:AutoConnect" continues to control when provider=Inventor.
- Support "SolidWorks:AutoConnect" and/or "Cad:AutoConnect" for new.
- Non-blocking, best-effort (current behavior).

**appsettings.json** (Server + any template):
```json
{
  "Cad": {
    "Provider": "Inventor"   // or "SolidWorks". Case-insensitive. Default Inventor.
    // "AutoConnect": true   // future shared; per-provider below honored
  },
  "Inventor": { "AutoConnect": true },
  "SolidWorks": { "AutoConnect": false },  // or true when ready
  ...
}
```
Update comments: "Legacy Inventor section honored for transition. Prefer Cad section."

**Error paths**:
- Unknown provider: fail fast at startup with clear message listing valid values (preferred) or fallback+warning.
- Provider selected but interop/COM unavailable: first use (connect) returns `{connected:false, error:...}` — not startup crash (robustness).

**TDD for pluggability (Strict)**:
- New tests (or extend existing DI tests): host builder with `IConfiguration` (use `MemoryConfiguration` or `ConfigurationBuilder`), different "Cad:Provider" values.
- Assert resolved `IMechanicalCadProvider` concrete type (InventorProvider vs SolidWorksProvider — tests will need ref to both).
- Verify auto-connect path exercises only the selected driver's Connect (spy or CallLog).
- Legacy "Inventor:AutoConnect" honored only for Inventor selection.
- Invalid provider throws on build/start.
- Provider-agnostic: "given any registered provider, cad_connect works".
- No live CAD.

**Solution / csproj**:
- McpCad.Server.csproj: add unconditional `<ProjectReference Include="..\McpCad.SolidWorks\McpCad.SolidWorks.csproj" />` (runtime config decides; build succeeds if SW interop HintPath present on dev machine or handled gracefully).
- src/mcp-cad.sln: Add Project entry for McpCad.SolidWorks (new GUID e.g. `{A1B2C3D4-0005-0005-0005-000000000005}`), config platforms, etc. (copy pattern from Inventor).
- McpCad.Tools.csproj: no change (only Core dep).
- No change to Core.

## 4. SolidWorks Implementation Architecture (from solidworks-basic-loop/spec.md + #272)
### Project Structure (exact mirror of McpCad.Inventor)
```
src/McpCad.SolidWorks/
  McpCad.SolidWorks.csproj
  SolidWorksDriver.cs
  SolidWorksProvider.cs
  Managers/
    DocumentManager.cs
    SketchManager.cs
    FeatureManager.cs
    InspectionManager.cs
    // Note: other 5 (Parameter, Property, Export, WorkFeature, Assembly) will be present as stubs or full delegation throwing "Not yet implemented for SolidWorks"
  Helpers/
    SwTagStore.cs          // or TagStore.cs (SolidWorks-specific)
    SelectionHelper.cs     // SelectByID2, mark handling, PID basics
    // ComDispatchHelper.cs if dynamic patterns needed (SW interop often works with early + dynamic)
  Properties/ (if AssemblyInfo pattern used)
```
- Follow Inventor namespace/layout exactly (`McpCad.SolidWorks.Managers`, etc.).
- Only reference McpCad.Core (no Tools).
- Interop: HintPath (dev machine). Typical paths (document in project README or comments):
  - `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll`
  - `SolidWorks.Interop.swconst.dll`
  - `SolidWorks.Interop.swdimensions.dll` (if needed later)
  - `<Private>false</Private>` or true matching Inventor; `<EmbedInteropTypes>false</EmbedInteropTypes>` often for SW.
- Target: net8.0-windows (COM).

### SolidWorksDriver.cs (COM entry + lifetime)
Mirror InventorDriver structure 1:1 where possible:
- `using SldWorks = SolidWorks.Interop.sldworks.SldWorks; using ModelDoc2 = ...;`
- Same P/Invoke: CLSIDFromProgID + GetActiveObject (ProgID: "SldWorks.Application").
- `private SldWorks? _swApp; private bool _connected;`
- `public SldWorks SwApp { get { if null Connect(); return ... } }`
- `public bool IsConnected => ...`
- `Connect()`: idempotent + health probe. Try GetActiveComObject("SldWorks.Application"). Cast. Version: `_swApp.RevisionNumber()` or `GetBuildNumbers` / `GetCurrentLicense` etc. Return `{connected:true, version: "...", solidworks_version: ...}` or neutral `version` + `provider`.
- Specific COMException handling (REGDB_E_CLASSNOTREG, CO_E_*, RPC_E_DISCONNECTED 0x80010108) → clear error dicts ("SolidWorks is not installed...", "Permission denied...", stale → DisconnectedHealth).
- `Disconnect()`: best-effort `Marshal.ReleaseComObject(_swApp)` + null.
- `Health()`: safe, never throw. Probe docs count, ActiveDoc (cast to ModelDoc2), FullFileName/DisplayName. On RPC disconnect: clear + DisconnectedHealth. Return keys like `["connected", "solidworks_version" or "version", "documents_open", "active_document", "provider":"SolidWorks"]`.
- `ActiveDocument => try (ModelDoc2)_swApp.ActiveDoc catch null`
- Private `DisconnectedHealth()`.
- Comments: reference #272 challenges (sw-01 COM activation/lifetime, sw-02 GetActiveObject vs CreateObject, sw-03 version detection, release discipline).

Support both attach-to-running (preferred) and (optionally) launch via `CreateObject` / `new SldWorks()` + Visible=true for skeleton robustness, but default attach.

### SolidWorksProvider.cs
Thin delegator, identical shape to InventorProvider:
```csharp
public class SolidWorksProvider : IMechanicalCadProvider
{
    private readonly SolidWorksDriver _driver;
    private readonly DocumentManager _document;
    private readonly SketchManager _sketch;
    private readonly FeatureManager _feature;
    private readonly InspectionManager _inspection;
    // + the other 5 as NotImplemented or full later

    public SolidWorksProvider(SolidWorksDriver driver) { ... new managers(driver); }

    // Connection: delegate to driver
    public Dictionary... Connect() => _driver.Connect();
    ...

    // Documents: _document.XXX
    // Sketch: _sketch (only the ones in basic loop + others throw or error-dict)
    // ...
    public Dictionary<string,object?> Extrude(...) => _feature.Extrude(...);
    // For not-in-scope (e.g. full Asm, WorkPlane, many features): 
    //   return ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");
    //   or throw new CadComException("...") — both acceptable per spec (tool layer normalizes).
    public void TagFacesFromSketch(int sketchIndex) => /* stub or not-impl */;
    public Dictionary... GetFeatureTree() => _inspection.GetFeatureTree();
    ...
}
```
Implement **all** interface members (compile requirement). Stubs clear + actionable.

### Managers for Minimal Viable Loop (scoped per spec)
Only implement what's needed for the 10-step acceptance flow. Others: clear not-impl.

**DocumentManager** (full doc_* for basic):
- Uses `SldWorks` + `ModelDoc2`.
- Doc types: `const int swDocPART = 1; swDocASSEMBLY=2;`
- New: `swApp.Documents.Add( docType, template, ... )` or `NewDocument`.
- Open: `Documents.Open(...)` cast ModelDoc2.
- Save/SaveAs: `doc.SaveAs(...)` or `Extension.SaveAs`.
- Close: `doc.Close()` or `swApp.CloseDoc`.
- Return `{success, document, document_type}`.
- Error: CadCom / CadConnection (use generalized, **not** obsolete Inventor* in new code).

**SketchManager** (basics: create on plane, line/circle/rect/etc for closed profile, profiles, tag support):
- Track `_activeSketch : ISketch`, `_activeSketchIndex`.
- Plane handling: default Front/Top/Right or use IFeature (planes) or Extension.SelectByID2("Front Plane", ...).
- Create: `doc.SketchManager.InsertSketch(true);` after selection or direct.
- Entities: `sketchMgr.CreateLine2(x1,y1,0, x2,y2,0);` etc. Return indices or success. On tag: record in helper store + optionally set attribute/PID.
- SketchProfiles(): enumerate `sketch.GetSketchSegments()` or better `GetSketchRegions` / closed contours; return list with area/centroid/indexes usable as "1", "2". Support @tag resolution for profiles in extrude.
- Basic constraints/dims not required for skeleton extrude flow but implement minimal if needed by profiles().
- Use selection marks for later feature ops.

**FeatureManager** (at minimum basic extrude):
- `Extrude(profile, distance, direction, taper, operation)`:
  - Resolve profile via index ("1") or @tag → use `SelectionManager` + `SelectByID2` or `sketch.GetSketchSegment(idx)` + select with mark.
  - `doc.FeatureManager.InsertExtrude( ... )` or `CreateExtrudeDefinition` + Add (early-bound where possible).
  - Map strings to swEndConditions, swExtrudeDirection, etc. (consts or swconst assembly).
  - Return success + feature_name etc.
- Revolve etc. stub for now.
- Profile resolution lives in Helpers (SW-specific, different from Inventor ProfileResolver + AddForSolid).

**InspectionManager** (3 tools):
- `CaptureViewportImage(view, w, h, fmt)`: 
  - Best-effort: set view orientation via camera or standard views.
  - Export: `doc.SaveAs(tempPath)` (analog to Inventor trick; reliable), or `ModelDoc2.Extension.SaveAs` with image opts, or `IView.SaveAsImageFile` / `GetPreview`. Read bytes → base64.
  - Return standard keys + "content" for MCP image block (copy pattern from AtomicTools wrapper).
- `GetFeatureTree()`: `var feat = doc.FirstFeature(); while(feat != null){ collect name, type=feat.GetTypeName2(), suppressed, children=feat.GetFirstSubFeature()...; feat=feat.GetNextFeature(); }` Build recursive list. Include bodies/features/ for assemblies.
- `GetBoundingBox(target="")`: Use `IBody2.GetBoundingBox` or `doc.GetBoundingBox` / MassProp.GetBoundingBox or part.GetBodies + union. Return min/max/center/size. Target "" = whole; support simple for tagged later.

**Error handling**: All throw/return via Cad* (generalized). Drivers/managers never leak COM exceptions to tools.

### Helpers (minimal for skeleton)
- `SwTagStore` (or `TagStore` in namespace): static or instance. Store tag → (sketch, entity PID or runtime index + mark).
  - `SetTag(sketchId, tag, entityRef)` where entityRef can be persistent ref (byte[] from GetPersistReference3) or selection mark.
  - `Resolve(sketch, "@tag")` → returns object or index usable for selection.
- Selection patterns: Heavy use of `doc.Extension.SelectByID2(name, type, x,y,z, append, mark)` + `SelectionManager` for multi-select profiles (mark != 0 for extrude profiles).
- For basic MVP: index-based ("1" from profiles list) + simple named tags created at sketch time. Full persistent ID + IAttribute propagation is #272 follow-up.
- Trade-off documented: duplication vs common IEntityTagResolver (rejected for this increment to keep scope; surface stays string-based neutral).
- Com helpers: minimal; SW RCW usually plays nicer than Inventor for dynamic in many cases.

**Not-implemented methods**: Return `ErrorResult.Create("Not yet implemented for SolidWorks provider")` (reuse or copy the Core.Models.ErrorResult pattern) or throw CadComException (tool normalizes). Prefer error dict for user-facing.

## 5. Tagging / Entity Resolution Strategy
- **Public contract unchanged**: parameters like `profile`, `entities`, `tag=` accept `"1"`, `"1,2"`, `"@foo"`, `"e3"` etc. (documented in tools-reference).
- **Impl encapsulation**: All resolution inside McpCad.SolidWorks/Helpers + Managers. No surface or I* change.
- **SW specifics vs Inventor**:
  - Inventor: static TagStore (sketchIdx + entityIdx), AttributeSets on faces for TagFacesFromSketch, 1-based collections, ProfileResolver.
  - SW: No direct equivalent AttributeSets on all entities for cheap tagging. Use:
    - Persistent IDs (GetPersistReference / GetPersistentID — see web sources).
    - Selection marks (0-63 range) for feature ops like extrude.
    - In-memory map (SwTagStore) keyed by runtime sketch + tag → PID or segment ref.
    - For sketch entities: when tagged on create, store; on profile/extrude resolve → select the geometry.
- **MVP for basic loop**: Prioritize numeric indices from `sketch_profiles()` (reliable). Support `@tag` for entities created with `tag=` in same sketch session (in-mem sufficient). Document "advanced cross-session / face tagging via persistent refs is future work".
- **Cross-provider compatibility**: Tests assert that `extrude(profile="1")` and simple @tag cases succeed on both impls.
- Rejected: Immediate shared abstraction (would touch Inventor code + add risk/LOC). Per-provider is explicit trade-off called out in proposal/risks.

## 6. Trade-offs and Rejected Alternatives
- **Aliases vs immediate rename only or breaking**: Strong aliases + Obsolete + delegation chosen (spec + proposal mandate). Provides zero-breakage for existing skills/prompts/agents. Rejected: pure rename (user breakage), keep inventor_* primary forever (defeats agnostic goal), dual full surfaces (duplication hell).
- **One server binary (config switch) vs separate Inventor/SW executables**: Single binary (current). Enables easy switch, shared MCP registration, one install. Rejected separate (complex deployment, two MCP entries, version skew).
- **Scope of SW managers this change**: 4 managers (Document + Sketch basics + Feature/extrude + Inspection) + stubs. Matches "minimal viable basic loop" exactly. Full 9 + parity later chained changes. Avoids massive PR / review load.
- **COM approach**: Mirror Inventor exactly (P/Invoke GetActiveObject + CLSID, heavy dynamic + const maps in managers, early casts where stable like PartComponentDefinition in Inventor precedent). Rejected full early-bound wrappers for everything (brittle across SW versions) or pure late-bound (loss of discoverability).
- **Tagging unification**: Per-provider Helpers now; string surface stable. Rejected premature ITagStore/ICadEntityResolver (touches too much, violates "no core contract changes").
- **Auto-connect DI**: Conditional registration + small if for driver resolve (no new ICadDriver interface yet). Future: could extract.
- **Health response keys**: Allow provider-specific ("solidworks_version") + add "provider" for clarity. Neutral "version" + "cad_version" acceptable but not forced (per spec risk note).
- **Testing under Strict TDD**: Heavy mocks (extend Mock pattern or Moq for driver interactions). Provider-agnostic contract tests (loop against interface). Live SolidWorks **only** in verify phase (same as current Inventor integration tests). Rejected: live CAD in unit tests (flaky, env dep).
- **Chained vs big-bang**: Explicit slices (generalize → pluggable → SW skeleton) per proposal. Enables early value (aliases protect users immediately) and reviewable units.

## 7. Risks & Mitigations (reflected in design)
- **Tagging/selection model differences** (Inventor AttributeSets/TagStore vs SW SelectionManager/PID/Mark): Encapsulated in SW/Helpers. MVP uses indices + in-mem tags. Explicit cross-provider test scenarios. #272 sw- challenges documented in code comments.
- **COM lifetime/activation diffs + GetActiveObject quirks**: Exact pattern reuse + idempotent health probe + specific HR handling + ReleaseComObject. Multiple connect/disconnect cycles covered in acceptance.
- **Viewport / feature tree / bbox reliability on SW**: Basic working paths (SaveAs or native) + clear partial notes/TODOs. Acceptance requires verifiable non-empty image + tree with extrude + positive bbox.
- **Live CAD dependency + Strict TDD overhead**: Mocks + interface contract tests allow 95% development without SW. Live only explicit verify. Record in sdd state.
- **Scope creep / 400 LOC budget**: Scoped to skeleton + basic loop + generalization+pluggable. Chained delivery. ask-on-risk for any live pieces.
- **Dual-support / migration duration**: Aliases + docs + default Inventor = safe transition. Removal is future increment.
- **Interop paths / build on non-SW machines**: HintPath (dev-only). Build may require SW installed for full; skeleton can have conditional or documented "present on build machine". Portable packaging of SW interops is out (per proposal).
- **Health string keys / other minor vendor leakage**: Acceptable this increment; generalize further later.
- **MCP tool registration with aliases**: Distinct method names → both appear. Framework handles. Verified in registration tests.

## 8. Concrete File Change Plan (actionable for sdd-tasks)
**Tools layer (generalization slice)**:
- src/McpCad.Tools/AtomicTools.cs (primary renames + 3 alias methods + desc updates + comments).
- tests/McpCad.Tests/Tools/AtomicToolsTests.cs (new cad_* tests + alias + Obsolete reflection checks).
- tests/McpCad.Tests/Tools/ToolRegistrationTests.cs (name presence, DI, count updates).
- tests/McpCad.Tests/Tools/ErrorHandlingTests.cs (minor neutral call updates).
- (Optional) other tests using old names: leave or comment-migrate.

**Server / pluggability slice**:
- src/McpCad.Server/Program.cs (config read, conditional DI registration for driver/provider, provider-aware auto-connect logic, error on invalid, usings for new).
- src/McpCad.Server/appsettings.json (Cad section + comments; keep legacy).
- src/McpCad.Server/McpCad.Server.csproj (add SW project ref).
- src/mcp-cad.sln (add SW project entry + platform configs).

**New McpCad.SolidWorks (skeleton slice)**:
- src/McpCad.SolidWorks/McpCad.SolidWorks.csproj (full, HintPaths, net8-windows, Core ref).
- src/McpCad.SolidWorks/SolidWorksDriver.cs (COM, P/Invoke, Connect/Disconnect/Health/props, error dicts, comments #272).
- src/McpCad.SolidWorks/SolidWorksProvider.cs (ctor, all interface methods delegated or not-impl error).
- src/McpCad.SolidWorks/Managers/DocumentManager.cs (doc_* using ModelDoc2 / Documents).
- src/McpCad.SolidWorks/Managers/SketchManager.cs (create, line/circle/rect, profiles, active sketch, basic tag via helper).
- src/McpCad.SolidWorks/Managers/FeatureManager.cs (Extrude at minimum using profile resolve + InsertExtrude/FeatureManager).
- src/McpCad.SolidWorks/Managers/InspectionManager.cs (CaptureViewportImage via export, GetFeatureTree via First/NextFeature, GetBoundingBox).
- src/McpCad.SolidWorks/Helpers/SwTagStore.cs (or TagStore.cs + SelectionHelper.cs) — in-mem + PID/mark basics.
- (Possibly) small ErrorResult usage if not in Core.Models visible; or use same dict pattern.
- Optional: SolidWorks-README.md or comments with interop paths + known challenges (sw-01 etc.).

**Docs / other**:
- docs/tools-reference.md (connection table + migration section).
- README.md (update status, add config note, "SolidWorks support (skeleton + basic loop)").
- openspec/changes/solidworks-provider/design.md (this file).
- (Hybrid) engram: mem_save topic_key=`sdd/solidworks-provider/design`.
- Possibly minor: update skills/*.md with notes (non-mandatory).
- No changes: Core/* (except if small shared helper needed — avoid), existing Inventor code (pristine), McpCad.Tools.csproj.

**Build/publish**: dotnet build will require SW interop on the machine doing full SW tests. Portable scripts unchanged this increment (SW interop not bundled).

## 9. Delivery / Slicing & Task Alignment
Per proposal "chained recommendation":
- **Slice 1 (generalization)**: AtomicTools renames+aliases + tests (TDD first) + doc updates. Small, high user-protection value, zero regression risk for surface.
- **Slice 2 (pluggable)**: Program.cs + appsettings + csproj/sln + pluggability DI/config tests. Inventor default preserved. Enables the switch.
- **Slice 3 (SW skeleton)**: New project + driver + provider + 4 managers + helpers + provider-agnostic + SW-specific mock tests. Basic loop verifiable at end.
- Later (separate changes): flesh remaining managers, advanced tagging/PID, full viewport parity, installer updates, skill refreshes, remove aliases after window.

sdd-tasks will break into reviewable, TDD-friendly units (e.g. "Write alias tests for cad_connect", "Implement SolidWorksDriver Connect/Health with P/Invoke", "Add SwTagStore + basic index resolver", "Update ToolRegistrationTests for neutral names").

Each slice produces green tests before merge.

## 10. Open Questions / Follow-ups (not for this design)
- Full "Cad" config schema evolution.
- Extracting common driver interface or entity resolver (post basic loop).
- SW interop redist / licensing notes for portable.
- Health response canonical keys.
- Removing aliases (after migration period).

**End of Design Artifact**
