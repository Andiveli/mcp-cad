# Server Config Loader Specification

## Purpose

Defines how `McpCad.Server` loads configuration at startup. The unified `config.json` (via `UserConfigStore`) is the primary source; `appsettings.json` + `Provider:Active` are the fallback chain for migration. Phase 2 extends this with multi-provider DI registration and router awareness.

## Requirements

### Requirement: Primary Config Source

The server MUST attempt to load `config.json` via `UserConfigStore.LoadAsync()` before reading any other configuration source.

#### Scenario: Config file exists

- GIVEN `~/.config/mcp-cad/config.json` (or `%APPDATA%\mcp-cad\config.json`) exists with valid content
- WHEN the server starts
- THEN `UserConfigStore.LoadAsync()` MUST return a valid config object
- AND the server MUST use `active_provider` from the config to select the provider
- AND `appsettings.json` `Provider:Active` MUST be ignored

#### Scenario: Config file does not exist

- GIVEN no config file exists at the expected path
- WHEN the server starts
- THEN `UserConfigStore.LoadAsync()` MUST return `null`
- AND the server MUST fall back to `appsettings.json` + `Provider:Active` (backward compatible)
- AND the server MUST NOT crash due to a missing config file

#### Scenario: Config file is corrupt

- GIVEN the config file contains invalid JSON
- WHEN the server starts
- THEN `UserConfigStore.LoadAsync()` MUST throw a `ConfigLoadException` with the parse error details
- AND the server MUST fall back to `appsettings.json` + `Provider:Active` with a warning logged
- AND the server MUST continue startup (degraded, not blocked)

### Requirement: Fallback Chain

The server MUST support a fallback chain: `config.json` → `appsettings.json` `Provider:Active` → hardcoded default.

#### Scenario: Provider:Active fallback

- GIVEN no config file exists but `appsettings.json` has `Provider:Active = "Inventor"`
- WHEN the server starts
- THEN the provider MUST resolve to Inventor
- AND the server behavior MUST match the pre-unified-config behavior exactly

#### Scenario: No config at all

- GIVEN neither config file nor `Provider:Active` is set
- WHEN the server starts
- THEN it MUST fail with `ConfigurationException` explaining the options: run the installer or add `Provider:Active` to `appsettings.json`

### Requirement: Provider DI Registration (Phase 1)

The server MUST register the selected provider's services in DI based on the resolved active provider, using the existing DI branch pattern.

#### Scenario: Onshape from config file

- GIVEN `active_provider = "onshape"` in config
- WHEN DI is configured
- THEN `IOnshapeClient` and `OnshapeProvider` MUST resolve
- AND `IMechanicalCadProvider` MUST resolve to `NullMechanicalCadProvider`
- AND `ICadWebProvider` MUST resolve to `OnshapeProvider`

#### Scenario: Inventor from fallback

- GIVEN `Provider:Active = "Inventor"` in `appsettings.json` (no config file)
- WHEN DI is configured
- THEN `InventorApprenticeConnector` and `InventorProvider` MUST resolve
- AND `IMechanicalCadProvider` MUST resolve to `InventorProvider`
- AND Onshape services MUST NOT be instantiated

### Requirement: Multi-Provider Registration (Phase 2)

The server SHALL register ALL available providers in DI at startup. Each provider MUST connect lazily on first use. The active provider for a given request is resolved at runtime by the router, not at startup.

#### Scenario: All providers registered, none connected at startup

- GIVEN Phase 2 is active
- WHEN DI is configured
- THEN `IProviderRouter` MUST be registered as a singleton
- AND `IOnshapeClient`, `InventorApprenticeConnector`, and (future) SolidWorks provider MUST all be registered
- AND no provider's `connect()` method SHALL be called during DI resolution
- AND startup MUST succeed even if Inventor COM is unavailable on Linux

#### Scenario: Onshape as fallback default

- GIVEN `active_provider = "onshape"` in config (or no config, fallback to `Provider:Active`)
- WHEN a tool call arrives with no per-call provider context
- THEN `IProviderRouter.CurrentProvider` MUST return `"onshape"`
- AND the Onshape provider MUST be used for the call

### Requirement: Config Structure for Router (Phase 2)

The config schema MUST support per-provider `enabled` flags that the router uses to determine which providers are available for dispatch.

#### Scenario: Provider disabled in config

- GIVEN `providers.inventor.enabled = false` in config
- WHEN `IProviderRouter.Resolve("inventor")` is called
- THEN it MUST throw or return null indicating the provider is disabled
- AND the error MUST surface as a user-visible message: *"Inventor is not enabled in config"*

### Requirement: Logging at Startup

The server MUST log the active config source and resolved provider at startup for debugging.

#### Scenario: Startup log

- GIVEN the server starts with a config file
- WHEN startup completes
- THEN the log MUST contain: `"Config source: ~/.config/mcp-cad/config.json"`
- AND `"Active provider: onshape (from config file)"`
- AND the fallback path MUST be logged if used: `"Config source: appsettings.json (fallback)"`

## Non-Goals

- Hot-reload of config while the server is running — config is read at startup only
- Merging multiple config files — the primary/fallback chain is linear
- Migration of existing `appsettings.json` to unified config — that is the installer's role
- Credential validation at startup — credentials are validated lazily on first provider connect
