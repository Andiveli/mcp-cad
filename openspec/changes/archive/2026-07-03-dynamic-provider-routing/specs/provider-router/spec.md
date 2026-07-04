# Provider Router Specification

## Purpose

Defines `IProviderRouter` — the runtime dispatch layer that resolves which CAD provider backend handles each tool call. Enables one MCP server process to route requests to Inventor, SolidWorks, or Onshape per invocation without restart. Supersedes the single-provider-per-process model.

## Requirements

### Requirement: Router Interface

The system MUST define an `IProviderRouter` interface with methods to query the current provider, scope a provider for a call, and resolve a provider backend by name.

#### Scenario: Interface shape

- GIVEN the `IProviderRouter` interface
- WHEN inspected
- THEN it MUST expose:
  - `string CurrentProvider` — the active provider name for the current execution context
  - `IDisposable PushProvider(string provider)` — scopes a provider for the duration of a tool call, returns a disposable that restores the previous scope
  - `ICadProvider Resolve(string provider)` — returns the registered `ICadProvider` implementation for the given name
  - `IReadOnlyCollection<string> AvailableProviders` — lists all registered and enabled provider names

#### Scenario: PushProvider returns disposable

- GIVEN `CurrentProvider` is `"onshape"`
- WHEN `PushProvider("inventor")` is called
- THEN `CurrentProvider` becomes `"inventor"` immediately
- AND when the returned `IDisposable` is disposed, `CurrentProvider` reverts to `"onshape"`

### Requirement: AsyncLocal Execution Context

The router MUST use `AsyncLocal<CadExecutionContext>` to scope the active provider per asynchronous flow, ensuring that concurrent tool calls do not interfere.

#### Scenario: Concurrent calls have isolated context

- GIVEN two concurrent tool calls to the same server
- WHEN call A uses `PushProvider("inventor")` and call B uses `PushProvider("onshape")`
- THEN `CurrentProvider` in call A's flow MUST be `"inventor"`
- AND `CurrentProvider` in call B's flow MUST be `"onshape"`
- AND neither call's context MUST leak to the other

#### Scenario: Default context from config

- GIVEN no `PushProvider` has been called
- WHEN `CurrentProvider` is read
- THEN it MUST return the `active_provider` from config (or `Provider:Active` fallback)
- AND it MUST NOT be null

### Requirement: Per-Call Middleware

The MCP tool invocation pipeline MUST set the execution context before the tool handler runs, using the resolved provider from config, prior skill selection, or explicit provider keyword.

#### Scenario: Tool middleware sets context

- GIVEN a tool call to `create_sketch` arrives
- WHEN the middleware runs
- THEN it MUST inspect the tool call for a provider hint (from session state or explicit keyword)
- AND it MUST call `PushProvider(resolvedProvider)` before the tool handler
- AND after the handler returns, the `IDisposable` MUST restore the prior context

#### Scenario: No provider hint uses default

- GIVEN a tool call without any provider hint
- WHEN the middleware runs
- THEN `CurrentProvider` MUST remain at the config default
- AND the tool MUST dispatch to the default provider's backend

### Requirement: Router Resolves from Registry

The router MUST resolve `ICadProvider` implementations by name from the lazy provider registry.

#### Scenario: Resolve success

- GIVEN `lazy-provider-registry` has registered `IOnshapeClient` as provider name `"onshape"`
- WHEN `router.Resolve("onshape")` is called
- THEN it MUST return a non-null `ICadProvider` instance
- AND the provider on first access SHALL trigger its lazy connection

#### Scenario: Resolve unknown provider

- GIVEN the registry has no provider named `"fusion360"`
- WHEN `router.Resolve("fusion360")` is called
- THEN it MUST throw `ProviderNotFoundException` with a message listing available providers
- AND the tool MUST surface this as `{"success": false, "error": "...", "available_providers": [...]}`

#### Scenario: Resolve disabled provider

- GIVEN `providers.inventor.enabled = false` in config
- WHEN `router.Resolve("inventor")` is called
- THEN it MUST throw `ProviderDisabledException`
- AND the error message MUST include instructions to enable the provider in config

### Requirement: Tool Convergence (Phase 2)

Tool classes MUST become stateless. They receive an `IProviderRouter` and resolve the backend per call, instead of having provider-specific tool families.

#### Scenario: AtomicTools uses router

- GIVEN an `AtomicTools` instance with `IProviderRouter` injected
- WHEN `CreateSketch` is called
- THEN it MUST call `_router.Resolve(_router.CurrentProvider)`
- AND dispatch the operation on the returned `ICadProvider`
- AND it MUST NOT have separate branches for Inventor vs Onshape

#### Scenario: OnshapeAtomicTools deprecation

- GIVEN the converged `AtomicTools` is complete
- WHEN a tool call arrives for an Onshape-only operation
- THEN the `ICadProvider` resolved for `"onshape"` MUST implement `ICadWebProvider`
- AND operations exclusive to the mechanical surface (e.g., fillet on a web CAD) MUST return a clear error: *"Onshape does not support feature-level fillet operations"*
- AND `OnshapeAtomicTools` class MAY be removed or deprecated once all callers migrate

### Requirement: Provider Capability Validation

Tools MUST validate that the resolved provider supports the requested operation before dispatching. Unsupported operations MUST return a clear error, not crash.

#### Scenario: Mechanical-only operation on Onshape

- GIVEN the current provider is Onshape
- WHEN an `IMechanicalCadProvider`-only operation (e.g., `fillet_edges`) is called
- THEN the tool MUST check `provider is IMechanicalCadProvider`
- AND if the check fails, return `{"success": false, "error": "Onshape does not support fillet operations"}`
- AND it MUST NOT attempt dispatch to a provider that doesn't implement the required interface

### Requirement: Tool Naming Unification

After convergence, each MCP tool MUST be registered exactly once. Name collisions between providers MUST be resolved by the router, not by separate registrations.

#### Scenario: No duplicate tool names

- GIVEN all provider backends are registered
- WHEN the tool list is enumerated
- THEN `create_sketch` MUST appear exactly once (not `inventor_create_sketch` and `onshape_create_sketch`)
- AND the single registration MUST route through `IProviderRouter`

## Non-Goals

- Out-of-process COM isolation — documented as a future concern
- Async `Task<>` tool signatures — synchronous tool signatures are preserved
- Provider-specific parameter schemas — all tools accept the same parameters; provider validation is internal
- Operation queuing or retry — each call dispatches synchronously to the resolved provider
- Middleware for non-tool MCP endpoints (lifecycle, resources) — only tool invocation is scoped
