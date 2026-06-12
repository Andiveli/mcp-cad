# Pluggable Server and Config-Driven Provider Selection — Spec

**Change**: solidworks-provider
**Related**: generalization, solidworks-basic-loop
**Strict TDD**: Enabled (tests for config selection, DI registration, auto-connect, legacy compat, error paths written first)

## Purpose
Make McpCad.Server pluggable: a single server binary hosts exactly one active IMechanicalCadProvider implementation selected at startup via configuration. Default remains Inventor for zero user-visible breakage. Legacy "Inventor:AutoConnect" is honored during transition. The tool surface (post-generalization) is identical regardless of selected backend.

## Requirements

### Requirement: Config-Driven Provider Selection
McpCad.Server/Program.cs SHALL read provider selection from IConfiguration under "Cad:Provider" (string, case-insensitive).
- Valid values: "Inventor" (default if missing/empty), "SolidWorks".
- The selected concrete type (InventorDriver + InventorProvider or SolidWorks equivalent) SHALL be registered as singleton for IMechanicalCadProvider and ICadProvider.
- Registration of the other provider's driver/provider SHALL be skipped (single active backend per process; no simultaneous multi-CAD).
- Builder.Services.AddSingleton<IMechanicalCadProvider, XXXProvider>(); and the ICadProvider forwarding registration SHALL be conditional.

#### Scenario: Default selects Inventor (zero breakage)
- GIVEN appsettings.json with no "Cad" section or "Cad:Provider" omitted/empty
- WHEN server starts
- THEN InventorDriver and InventorProvider are registered
- AND all existing cad_* / inventor_* calls and auto-connect behavior are unchanged

#### Scenario: Explicit SolidWorks selection
- GIVEN configuration "Cad:Provider": "SolidWorks"
- WHEN server starts and a tool is invoked (e.g. cad_connect)
- THEN the call is serviced by the SolidWorksProvider implementation
- AND the same call with Inventor config uses InventorProvider

### Requirement: Legacy Inventor:AutoConnect Support (Transition)
The auto-connect logic SHALL continue to honor "Inventor:AutoConnect": true (non-blocking).
- During transition, the server MAY read both "Inventor:AutoConnect" and a future "Cad:AutoConnect" or provider-specific.
- When "Cad:Provider" == "Inventor", the legacy key controls auto-connect for the Inventor driver.
- When "Cad:Provider" == "SolidWorks", a "SolidWorks:AutoConnect" (or shared Cad:AutoConnect) is supported; legacy Inventor key is ignored for SW.
- Auto-connect SHALL be non-blocking and safe to fail (as current).

#### Scenario: Legacy auto-connect still triggers for Inventor default
- GIVEN "Inventor:AutoConnect": true and no overriding Cad section
- WHEN Host is built
- THEN the InventorDriver.Connect() is invoked on startup (best-effort)

### Requirement: Provider-Aware Auto-Connect and Health
Auto-connect code SHALL be refactored to obtain the driver via DI (or the active provider) instead of hard-coded InventorDriver type.
- Health responses may include provider-specific version keys (e.g. "inventor_version" or "solidworks_version") for diagnostic value; a neutral "provider" and "version" is encouraged but not required for this increment.

#### Scenario: Switching config switches the active driver without code change in tools
- GIVEN two runs of the server with different Cad:Provider
- WHEN the same MCP client sends cad_connect then doc_new_part then get_feature_tree
- THEN each run succeeds using the selected backend's native behavior and returns consistent envelope shapes

### Requirement: Clear Error on Invalid Provider Selection
On startup, if "Cad:Provider" is set to an unrecognized value, the server SHALL log a clear error and either fail fast or fall back to Inventor (with warning). Preference: fail fast with actionable message listing valid values.

#### Scenario: Invalid provider value
- GIVEN "Cad:Provider": "FreeCAD"
- WHEN builder.Build() or app.RunAsync starts
- THEN an exception or logged fatal error identifies the invalid value and lists supported providers ("Inventor", "SolidWorks")

### Requirement: Solution, Project References, and Packaging
- src/mcp-cad.sln SHALL include the new McpCad.SolidWorks project (added in the impl slice).
- McpCad.Server.csproj SHALL add a ProjectReference to McpCad.SolidWorks (conditional on build or unconditional; the runtime selection decides usage).
- No change to McpCad.Tools.csproj (it only depends on Core + MCP).
- Build (dotnet build / publish) SHALL succeed for all configurations with the new reference.
- Portable publish scripts may need no change for this increment (SW interop not bundled yet; documented as follow-up per proposal).

### Requirement: Strict TDD for Pluggability
- Tests SHALL be added (or existing DI/registration tests extended) that:
  - Construct a host with mocked IConfiguration providing different "Cad:Provider" values.
  - Verify the resolved IMechanicalCadProvider is the expected concrete type (InventorProvider vs. a mock or SolidWorksProvider).
  - Verify auto-connect path is exercised only for the selected provider.
  - Verify legacy "Inventor:AutoConnect" still works when provider=Inventor.
- These tests use Microsoft.Extensions.Configuration and DI container (as in current ToolRegistrationTests).
- No live CAD required for these unit-level pluggability tests.

#### Edge Cases and Error Conditions
- Missing configuration section → defaults to Inventor.
- Case variations ("solidworks", "SOLIDWORKS") → accepted.
- Provider selected but its assembly not referenced or COM not available → runtime failure on first use (connect returns {connected:false, error:...}) rather than startup crash (preferred for robustness).
- Concurrent use of multiple provider singletons is prevented by design (only one registered).

## Non-Goals
- Simultaneous registration of multiple concrete providers in one process.
- Dynamic runtime switching of provider without process restart.
- Installer / redistributable packaging of SolidWorks interop assemblies (skeleton only).
- Full "Cad" section schema finalization (evolve in later increments).

## Acceptance Criteria (Strict TDD)
All scenarios above SHALL have passing automated tests before the corresponding production changes are considered complete. Provider-agnostic tests (e.g. "given any registered IMechanicalCadProvider, the connection tools work") are required.

## References
- Proposal sections on pluggable mechanism, config, legacy compat, delivery (chained phases).
- Current: Program.cs (hardcoded), appsettings.json, McpCad.Server.csproj.
- Generalized exceptions already in Core (CadComException etc.).
