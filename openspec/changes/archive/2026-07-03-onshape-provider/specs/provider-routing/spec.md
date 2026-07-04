# provider-routing Specification

## Purpose

Server-level selection of the active CAD provider at startup. One provider runs per process; tools register per provider from a DI branch keyed by `Provider:Active`.

## Requirements

### Requirement: Active Provider Switch

`appsettings.json` MUST contain `Provider:Active` ∈ {`Inventor`, `Onshape`}. The host MUST fail fast on missing or unknown values — never fall back.

#### Scenario: Wiring and fail-fast

- GIVEN `Provider:Active = "Onshape"`, `IOnshapeClient`, `OnshapeProvider`, `OnshapeAtomicTools` MUST resolve; `IMechanicalCadProvider` MUST resolve to `NullMechanicalCadProvider`
- GIVEN `Provider:Active = "Fusion360"` or key missing, the host MUST throw `ConfigurationException` before any handler registers

### Requirement: DI Branch Isolation

Onshape services MUST live in a child DI container built ONLY when `Provider:Active = "Onshape"`. Inventor COM MUST NOT be instantiated in Onshape mode.

#### Scenario: COM skipped, handler isolated

- GIVEN Onshape active, `InventorApprenticeConnector` MUST NOT be constructed (no COM, no STA thread)
- GIVEN `OnshapeAtomicTools.CreateSketch` is dispatched, `OnshapeProvider` MUST be injected; `IMechanicalCadProvider` MUST NOT appear

### Requirement: Per-Provider Tool Registration

The MCP tool list MUST contain ONLY tools for the active provider. Name collisions MUST fail startup.

#### Scenario: Tool list and collision rejection

- GIVEN `Provider:Active = "Inventor"`, `GET /tools` MUST list `InventorAtomicTools.*` and MUST NOT list `OnshapeAtomicTools.*`
- GIVEN two providers register `create_sketch`, startup MUST fail with `ToolRegistrationException` naming both providers
