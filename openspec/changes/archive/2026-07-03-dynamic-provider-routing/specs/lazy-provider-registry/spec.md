# Lazy Provider Registry Specification

## Purpose

Defines the DI registration strategy where all CAD provider backends are registered at startup but connect lazily on first use. This ensures the server starts regardless of platform-specific COM availability and allows the router to dispatch to any registered backend per tool call.

## Requirements

### Requirement: All Providers Registered at Startup

The system MUST register every known CAD provider implementation in DI during server startup. Registration MUST NOT trigger provider initialization or connection.

#### Scenario: Registration without initialization

- GIVEN the server starts on Linux
- WHEN DI is configured
- THEN `IInventorClient` MUST be registered (with lazy singleton lifetime)
- AND `IOnshapeClient` MUST be registered (with lazy singleton lifetime)
- AND `ISolidWorksClient` (future) MUST be registered (with lazy singleton lifetime)
- AND no provider's constructor or `connect()` method SHALL execute during registration
- AND startup MUST complete without attempting COM interop

#### Scenario: All providers resolvable by name

- GIVEN DI configuration is complete
- WHEN `IProviderRouter.AvailableProviders` is enumerated
- THEN it MUST contain `"inventor"`, `"onshape"`, and (future) `"solidworks"`
- AND each provider name MUST map to its registered `ICadProvider` implementation

### Requirement: Lazy Singleton Lifetime

Each provider MUST be registered as a lazy singleton: the same instance is reused across all calls, but the instance is not created until the first `Resolve()` or method call.

#### Scenario: First access triggers construction

- GIVEN the server has started and no Inventor call has been made
- WHEN `router.Resolve("inventor")` is called for the first time
- THEN the `InventorProvider` constructor MUST execute at this point
- AND the `IInventorClient.Connect()` SHOULD be called (either in constructor or as explicit initialization)
- AND subsequent `Resolve("inventor")` calls MUST return the same instance

#### Scenario: Unused provider is never constructed

- GIVEN the server runs for an entire session with only Onshape tool calls
- WHEN the session ends
- THEN `InventorProvider` and `SolidWorksProvider` MUST NOT have been constructed
- AND no COM interop or HTTP client for those providers MUST have been created

### Requirement: Graceful Degradation on Connect Failure

If a provider's first-use connection fails (e.g., Inventor COM not installed), the router MUST return a descriptive error for that provider without affecting other providers or crashing the server.

#### Scenario: COM not available on Linux

- GIVEN the server runs on Linux
- WHEN `router.Resolve("inventor")` is called
- THEN the lazy initialization SHALL attempt COM connection
- AND when COM fails, it MUST throw `ProviderConnectionException` with message: *"Inventor requires Windows with Autodesk Inventor installed"*
- AND the server MUST remain fully operational for Onshape calls

#### Scenario: Network error on Onshape connect

- GIVEN the server is offline
- WHEN `router.Resolve("onshape")` is called
- THEN the lazy initialization SHALL attempt HTTP connection
- AND when network is unreachable, it MUST throw `ProviderConnectionException` with a network-level error message
- AND subsequent calls to other providers MUST NOT be affected

### Requirement: Registration API

The system MUST provide a `RegisterAllProviders(IServiceCollection)` extension method or equivalent that registers all known providers.

#### Scenario: Registration method

- GIVEN `IServiceCollection` in `Program.cs`
- WHEN `RegisterAllProviders(services)` is called
- THEN all known providers MUST be registered as lazy singletons
- AND `IProviderRouter` MUST be registered as a singleton
- AND each provider's `enabled` flag from config MUST be respected (disabled providers return errors on resolve)

#### Scenario: Provider-specific configurations

- GIVEN a provider requires configuration (e.g., Onshape API keys)
- WHEN `RegisterAllProviders` runs
- THEN it MUST read provider-specific config sections and bind them to the provider's options
- AND credentials MUST NOT be validated at registration time — only at first connect

### Requirement: Thread Safety

Lazy initialization MUST be thread-safe: concurrent calls to the same provider MUST NOT create multiple instances.

#### Scenario: Concurrent first access

- GIVEN two concurrent tool calls both require Inventor
- WHEN both resolve `"inventor"` simultaneously
- THEN only ONE `InventorProvider` instance MUST be constructed
- AND both calls MUST receive the same instance
- AND the constructor MUST execute exactly once

## Non-Goals

- Connection pooling or instance reuse beyond singleton — one provider instance per backend per server lifetime
- Provider health-check at registration — health is verified lazily at first use
- Dynamic provider registration at runtime — all providers are known at compile time; new backends require a code change
- Provider eviction or recycling — once connected, a provider lives for the server process lifetime
