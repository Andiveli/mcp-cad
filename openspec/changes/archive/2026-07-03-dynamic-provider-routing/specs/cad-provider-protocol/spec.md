# CAD Provider Protocol — Delta Spec

## ADDED Requirements

### Requirement: SolidWorks as IMechanicalCadProvider

SolidWorks SHALL register as an `IMechanicalCadProvider` implementation alongside Inventor. The backend SHALL implement all mechanical CAD operations (sketch, feature, parameter, property, export) that SolidWorks API supports.

#### Scenario: SolidWorks implements the mechanical contract

- GIVEN the `IMechanicalCadProvider` interface
- WHEN `SolidWorksProvider` is inspected
- THEN it MUST implement `IMechanicalCadProvider`
- AND it MUST provide sketch, feature, parameter, property, and export operations matching the interface signature
- AND it MUST NOT implement `ICadWebProvider` (SolidWorks is desktop CAD, not web CAD)

#### Scenario: SolidWorks registration is platform-gated

- GIVEN the server starts on Linux
- WHEN provider registration runs
- THEN `SolidWorksProvider` MUST be registered (lazy) in DI
- BUT when resolved on Linux, it MUST fail with a clear error: *"SolidWorks requires Windows"*
- AND the error MUST NOT prevent other providers from working

### Requirement: Provider Identity for Router

Each `ICadProvider` implementation MUST expose a well-known string name that the router uses for dispatch resolution.

#### Scenario: Provider name attribute

- GIVEN a registered `ICadProvider` implementation
- WHEN inspected for a provider name
- THEN `InventorProvider` MUST be named `"inventor"`
- AND `OnshapeProvider` MUST be named `"onshape"`
- AND `SolidWorksProvider` MUST be named `"solidworks"`
- AND the name MUST match the config `providers` dictionary key

#### Scenario: NullMechanicalCadProvider name

- GIVEN `NullMechanicalCadProvider` is resolved by the router (fallback case)
- WHEN its `ProviderName` is read
- THEN it MUST return `"none"` or an equivalent sentinel indicating no mechanical backend is active
- AND tool-level validation MUST use `provider is IMechanicalCadProvider` checks, not the name string

### Requirement: Provider Connection Semantics

Providers MUST support lazy connection: construction and connection are separate. The protocol SHALL define a `ConnectAsync()` or `Connect()` method that can fail independently of construction.

#### Scenario: Lazy connect protocol

- GIVEN an `ICadProvider` implementation
- WHEN constructed
- THEN it MUST NOT attempt external connection (COM, HTTP) in the constructor
- AND `Connect()` MUST be a separate method callable on first use
- AND `Connect()` MUST throw `ProviderConnectionException` on failure
- AND `Health()` MUST return `false` before `Connect()` is called

## MODIFIED Requirements

### Requirement: NullMechanicalCadProvider Returns Errors

`NullMechanicalCadProvider` MUST return descriptive errors for all mechanical operations instead of silently succeeding as a no-op.

(Previously: `NullMechanicalCadProvider` returned empty/no-op results when a mechanical CAD provider was not active. With router-based dispatch, silent no-ops mask configuration errors.)

#### Scenario: Fillet returns error for Onshape session

- GIVEN the current provider is Onshape (no mechanical backend)
- WHEN `NullMechanicalCadProvider.Fillet()` is called
- THEN it MUST return `OperationNotSupportedException` or `{"success": false, "error": "Onshape does not support fillet operations"}`
- AND it MUST NOT silently return an empty success

#### Scenario: Sketch operations return error

- GIVEN the current provider is Onshape
- WHEN any mechanical operation is called on `NullMechanicalCadProvider`
- THEN every method MUST throw or return an error indicating the provider does not support mechanical operations
- AND the error message MUST include the active provider name for context

### Requirement: Provider Adapter Factory Superseded by Registration

The adapter factory pattern is superseded by the lazy provider registry for router-aware deployments. The adapter factory MAY remain as a backward-compatibility shim for single-provider setups.

(Previously: A single `create_inventor_provider()` factory constructed one provider from individual manager instances. With multi-provider registration, each provider is self-registering via DI and the `RegisterAllProviders()` method.)

#### Scenario: Factory still works for single-provider mode

- GIVEN `config.json` has only one provider enabled
- WHEN the server starts in backward-compatible mode (Phase 1, no router)
- THEN the existing adapter factory MAY still be used to create the single provider
- AND the factory MUST accept the provider name to construct: `create_provider("inventor")`

#### Scenario: Router mode bypasses factory

- GIVEN the router is active (Phase 2)
- WHEN the server starts
- THEN `RegisterAllProviders()` MUST register all providers in DI
- AND the adapter factory MUST NOT be called
- AND tools MUST resolve providers through `IProviderRouter`, not through the factory

## Non-Goals

- Changing existing `ICadProvider` method signatures — all are preserved
- Adding web CAD operations to `IMechanicalCadProvider` — SolidWorks is mechanical, not web
- Removing the adapter factory — kept as a backward-compat shim until Phase 2 fully lands
- Changing the error hierarchy — errors remain backend-agnostic per the existing requirement
