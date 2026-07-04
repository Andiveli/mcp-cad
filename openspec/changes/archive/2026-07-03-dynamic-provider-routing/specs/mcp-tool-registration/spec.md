# MCP Tool Registration — Delta Spec

## ADDED Requirements

### Requirement: Tools Accept IProviderRouter

Tool factories and registration MUST accept `IProviderRouter` instead of a single `ICadProvider`. Tools resolve the provider per call from the router, not at registration time.

#### Scenario: Tool registration with router

- GIVEN the `RegisterTools` method in `McpCad.Tools`
- WHEN called
- THEN it MUST accept `IProviderRouter` as its dependency source
- AND each tool MUST call `_router.Resolve(_router.CurrentProvider)` on every invocation
- AND it MUST NOT capture a single `ICadProvider` at registration time

#### Scenario: Tool resolves provider per call

- GIVEN a single `CreateSketch` tool registered once
- WHEN called with current provider = "inventor"
- THEN it dispatches to `InventorProvider.CreateSketch()`
- AND when called in another invocation with current provider = "onshape"
- THEN it dispatches to `OnshapeProvider.CreateSketch()`
- AND both calls use the same tool registration — no tool-name collision

### Requirement: Provider-Agnostic Tool Names

Tool names MUST NOT include the provider name prefix. The router determines the backend; the tool name describes the operation.

#### Scenario: Unified tool names in registry

- GIVEN the MCP tool registry
- WHEN tools are listed
- THEN the following tools MUST appear without provider prefix:
  `connect`, `disconnect`, `health`,
  `doc_open`, `doc_new_part`, `doc_new_assembly`, `doc_save`, `doc_save_as`, `doc_close`,
  `sketch_create`, `sketch_line`, `sketch_circle`, `sketch_arc`, `sketch_rectangle`, `sketch_dimension`,
  `extrude`, `revolve`, `fillet`, `chamfer`,
  `param_list`, `param_get`, `param_set`, `param_set_expression`,
  `iproperty_get`, `iproperty_set`, `iproperty_summary`, `iproperty_custom_get`, `iproperty_custom_set`,
  `export_step`, `export_stl`, `export_pdf`, `export_dxf`
- AND `inventor_connect`, `onshape_connect`, `inventor_health`, etc. MUST NOT appear
- AND `mcp_cad_select_provider` SHALL appear as an additional tool

#### Scenario: Backward-compat aliases (Phase 2 transition)

- GIVEN existing clients call `inventor_connect` by name
- WHEN the server is in transition mode (Phase 2 initial deployment)
- THEN `inventor_connect` MAY be registered as a deprecated alias for `connect`
- AND the deprecated aliases MUST be removed before the final Phase 2 PR

## MODIFIED Requirements

### Requirement: RegisterTools Accepts IProviderRouter

The `RegisterTools` signature MUST change from accepting a single `ICadProvider` to accepting `IProviderRouter`.

(Previously: `RegisterTools(IMcpInstance mcp, ICadProvider provider)` — tools captured a single provider at registration time.)

#### Scenario: New signature shape

- GIVEN the new `RegisterTools` method
- WHEN called
- THEN it MUST accept `IMcpInstance mcp` and `IProviderRouter router`
- AND each tool closure MUST use `router.Resolve(router.CurrentProvider)` to dispatch
- AND tools MUST NOT hold a direct reference to any `ICadProvider` implementation

#### Scenario: Tool statelessness verified

- GIVEN any tool handler
- WHEN inspected for provider state
- THEN the handler MUST NOT have a field of type `ICadProvider`, `InventorProvider`, `OnshapeProvider`, or any concrete provider type
- AND the ONLY provider-related dependency MUST be `IProviderRouter`

### Requirement: Single Registration of Each Tool

Each tool name MUST be registered exactly once. Provider-specific tool families (`OnshapeAtomicTools`, `InventorAtomicTools`) MUST converge into a single registration set.

(Previously: `OnshapeAtomicTools` and `AtomicTools` were separate registration sets, each containing provider-specific implementations. With the router, one set suffices.)

#### Scenario: OnshapeAtomicTools removed

- GIVEN the converged tool set
- WHEN `RegisterTools` runs
- THEN it MUST NOT register any tools from `OnshapeAtomicTools`
- AND all Onshape operations MUST be handled by the single `AtomicTools` set dispatching through the router
- AND the `McpCad.Tools.Onshape` project/assembly MAY be removed once all references migrate

#### Scenario: Provider-specific validation via interface check

- GIVEN a tool like `fillet` that requires `IMechanicalCadProvider`
- WHEN the resolved provider does not implement `IMechanicalCadProvider`
- THEN the tool MUST return `{"success": false, "error": "Current provider does not support fillet operations"}`
- AND it MUST NOT register a separate `fillet` for each provider

## REMOVED Requirements

### Requirement: Provider-Specific Tool Families

(Reason: `OnshapeAtomicTools` and `AtomicTools` were separate tool families registered per provider. With `IProviderRouter`, all tools register once and the router selects the backend per call. Separate provider tool families are no longer needed.)

(Migration: Existing `OnshapeAtomicTools` callers MUST migrate to `AtomicTools` with router injection. The `McpCad.Tools.Onshape` project is deprecated and its tools converge into `McpCad.Tools`. Tests MUST verify that Onshape-only operations (e.g., web-CAD-specific) return clear error messages when the resolved provider is not `ICadWebProvider`.)

## Non-Goals

- Removing `mcp_cad_select_provider` registration — it is an additional tool, not a replacement for existing tools
- Changing the `{success: bool, error?: string}` envelope pattern — error handling remains identical
- Adding new tool operations — the 32 existing tools + 1 new skill tool are the complete set for this change
- Changing tool parameter types, default values, or return shapes — all are preserved
