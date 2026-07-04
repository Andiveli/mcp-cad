# User Config Store Specification

## Purpose

Defines a cross-platform, single-source-of-truth configuration file (`config.json`) that unifies provider selection, credentials, and agent registration into one portable JSON schema. Replaces the fragmented config approach (user-secrets + `appsettings.json` + `scripts/tui/state.json`).

## Requirements

### Requirement: Cross-Platform File Path

The system MUST store the unified config file at a well-known cross-platform location determined at runtime.

#### Scenario: Linux path

- GIVEN `UserConfigStore` is instantiated on Linux
- WHEN `DefaultConfigPath` is resolved
- THEN it MUST return `~/.config/mcp-cad/config.json`
- AND `$XDG_CONFIG_HOME/mcp-cad/config.json` MUST be used when `XDG_CONFIG_HOME` is set

#### Scenario: Windows path

- GIVEN `UserConfigStore` is instantiated on Windows
- WHEN `DefaultConfigPath` is resolved
- THEN it MUST return `%APPDATA%\mcp-cad\config.json`
- AND it MUST use `Environment.SpecialFolder.ApplicationData` as the base

### Requirement: Config Schema Contract

The system MUST define a config schema with a `version` field, `active_provider`, per-provider sections, and an agent registration list.

#### Scenario: Schema shape

- GIVEN a valid `config.json` loaded by `UserConfigStore`
- WHEN deserialized
- THEN the root object MUST contain a `version` integer, `active_provider` string, `providers` dictionary, and `agents` array
- AND the `providers` dictionary MUST have keys matching registered provider names (`onshape`, `inventor`, `solidworks`)
- AND each provider entry MUST contain `enabled` boolean and `credentials` (provider-specific, nullable)
- AND each agent entry MUST contain `name` string and `url` string

#### Scenario: Concrete example round-trip

- GIVEN the following config object:

  ```json
  {
    "version": 1,
    "active_provider": "onshape",
    "providers": {
      "onshape": { "enabled": true, "credentials": { "access_key": "ak1", "secret_key": "sk1" } },
      "inventor": { "enabled": false, "credentials": null },
      "solidworks": { "enabled": false, "credentials": null }
    },
    "agents": [
      { "name": "OpenCode", "url": "http://localhost:51777" }
    ]
  }
  ```

- WHEN serialized then deserialized
- THEN all fields MUST round-trip exactly
- AND unknown provider keys MUST NOT cause deserialization failure (forward-compatible)

### Requirement: Atomic Read and Write

The system MUST read config with atomic semantics: no partial reads of concurrently-written files. Writes MUST use atomic file replacement.

#### Scenario: Atomic read

- GIVEN a `config.json` exists on disk
- WHEN `UserConfigStore.LoadAsync()` completes
- THEN the returned config MUST be a self-consistent snapshot (no torn reads)
- AND a `FileNotFoundException` returned as `null` (default config) when the file does not exist — not a crash

#### Scenario: Atomic write

- GIVEN a config object to persist
- WHEN `UserConfigStore.SaveAsync(config)` completes
- THEN the write MUST use a temp file + `File.Replace` (or OS-atomic equivalent) so concurrent readers see either the old or new file — never empty/corrupt
- AND the previous file content MUST be recoverable from a `.bak` backup

### Requirement: Default Values

The system MUST provide a factory method that returns a valid default config when the user has no file yet.

#### Scenario: Default config

- GIVEN `UserConfigStore.CreateDefault()`
- WHEN called
- THEN it MUST return a config with `version = 1`, `active_provider = null` (unset), all known providers present with `enabled = false`, and an empty `agents` list
- AND every provider MUST have a `credentials` section that is `null`

### Requirement: Schema Versioning

The config schema MUST carry a `version` field for future migration. Unknown versions MUST NOT crash the loader.

#### Scenario: Unknown version loads safely

- GIVEN a `config.json` with `version = 2` (future schema)
- WHEN `UserConfigStore.LoadAsync()` is called
- THEN it MUST return the deserialized object (forward-compatible)
- AND the loader MAY emit a warning about the unknown version
- AND `SaveAsync()` MUST preserve the existing version when only modifying a subset of fields

### Requirement: No External Dependencies

The `UserConfigStore` class MUST live in `McpCad.Core` with zero NuGet dependencies beyond the runtime.

#### Scenario: Zero-dependency enforcement

- GIVEN the `McpCad.Core.csproj` file
- WHEN inspected for package references
- THEN it MUST NOT have dependencies on `Microsoft.Extensions.Configuration`, `System.Text.Json`, or any other NuGet package
- AND it MUST use `System.Text.Json` (runtime-inbox) for serialization

## Non-Goals

- Encrypting credentials at rest — credentials are stored as plaintext JSON; encryption is a future concern
- Migration of existing config files to the new schema — the change provides a `null`-returning `LoadAsync` and a fallback path for existing `appsettings.json`
- Hot-reload of config while the server is running — config is read at startup only
- Merging config from multiple sources — the canonical source is the file; the server provides a fallback chain for migration
