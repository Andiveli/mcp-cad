# Installer Wizard Specification

## Purpose

Defines the cross-platform TUI wizard (`McpCad.Installer`) that guides users through CAD provider selection, credential entry, agent registration, and unified config creation. Runs via `dotnet run` — no pre-built binaries required.

## Requirements

### Requirement: Cross-Platform TUI

The installer MUST use Spectre.Console to render a TUI on both Linux and Windows. The TUI MUST be fully keyboard-navigable with no mouse requirement.

#### Scenario: TUI renders on both platforms

- GIVEN the installer is started via `dotnet run` on Linux or Windows
- WHEN the TUI initializes
- THEN it MUST render a `SelectionPrompt`-based wizard in the terminal
- AND the wizard MUST NOT depend on Windows-specific console APIs

#### Scenario: Keyboard-only navigation

- GIVEN the TUI is rendered
- WHEN a user navigates with arrow keys and Enter
- THEN every step MUST be completable without mouse input
- AND `Ctrl+C` MUST cleanly exit without leaving artifacts

### Requirement: Provider Selection Step

The installer MUST present a provider selection screen. On Linux, desktop-CAD providers MUST be shown as disabled with a platform note.

#### Scenario: Windows provider selection

- GIVEN the installer runs on Windows
- WHEN the provider selection step is shown
- THEN it MUST list **Autodesk Inventor**, **SolidWorks**, and **Onshape** as selectable options
- AND each option MUST be a `SelectionPrompt` choice with a clear label

#### Scenario: Linux provider selection

- GIVEN the installer runs on Linux
- WHEN the provider selection step is shown
- THEN **Autodesk Inventor** and **SolidWorks** MUST appear as disabled entries with `(Windows only)` suffix
- AND only **Onshape** MUST be selectable
- AND the help text MUST explain that desktop CAD requires Windows

### Requirement: Credential Entry Step

After provider selection, the installer MUST prompt for provider-specific credentials.

#### Scenario: Onshape credentials

- GIVEN the user selected Onshape
- WHEN the credential step runs
- THEN it MUST prompt for **Access Key** (required) and **Secret Key** (required) as masked text inputs
- AND it MAY prompt for **Secret Key ID** (optional)

#### Scenario: Inventor credentials (Windows)

- GIVEN the user selected Inventor on Windows
- WHEN the credential step runs
- THEN it MUST offer an optional **Auto-connect to running Inventor** toggle (`true`/`false`)
- AND it MUST accept the toggle without requiring API keys

#### Scenario: SolidWorks credentials (Windows)

- GIVEN the user selected SolidWorks on Windows
- WHEN the credential step runs
- THEN it MUST show a note: *"SolidWorks provider is planned — selection is saved for future compatibility"*
- AND it MUST NOT prompt for SolidWorks-specific credentials (Phase 2 will add them)

### Requirement: Agent Registration Step

The installer MUST present a multi-select list of MCP hosts to register.

#### Scenario: Agent registration list

- GIVEN the installer reaches the agent registration step
- WHEN the user is prompted
- THEN the list MUST contain known MCP hosts: **OpenCode**, **Claude Desktop**, **Pi**, **VS Code**, and a **Custom URL** option
- AND each selected agent MUST be written to the config's `agents` array with a default URL
- AND pre-selection MUST match the current defaults (all selected)

### Requirement: Server Discovery

The installer MUST discover the published server executable path on both platforms.

#### Scenario: Linux server discovery

- GIVEN the installer runs on Linux
- WHEN `FindServerPath()` is called
- THEN it MUST search for `McpCad.Server` (no `.exe` suffix) in the publish directory
- AND it MUST NOT require a `.exe` extension

#### Scenario: Windows server discovery

- GIVEN the installer runs on Windows
- WHEN `FindServerPath()` is called
- THEN it MUST search for `McpCad.Server.exe` in the publish directory
- AND it MUST use the same search logic as Linux but with the `.exe` suffix

#### Scenario: Server not found

- GIVEN the server binary is not in the expected publish directory
- WHEN `FindServerPath()` returns null
- THEN the wizard MUST print a warning with the expected path
- AND it MUST still write the config file so the server can be started manually

### Requirement: Config File Write

On completion, the wizard MUST write a valid `config.json` via `UserConfigStore`.

#### Scenario: Successful completion

- GIVEN all wizard steps completed successfully
- WHEN the finish step runs
- THEN the installer MUST call `UserConfigStore.SaveAsync(config)` with the accumulated data
- AND it MUST print *"mcp-cad configured. Restart your MCP host."*
- AND it MUST NOT require admin/root privileges for the write

#### Scenario: Write failure

- GIVEN the config file path is not writable
- WHEN `SaveAsync` throws
- THEN the installer MUST display the error message and the target path
- AND it MUST offer to retry or exit

### Requirement: `dotnet run` Distribution

The installer MUST be runnable via `dotnet run` from the repository root. No pre-built binaries are shipped.

#### Scenario: One-command setup

- GIVEN a user has cloned the repository
- WHEN they run `dotnet run --project src/McpCad.Installer`
- THEN the wizard MUST start without additional build steps
- AND the README MUST document this as the setup command

### Requirement: Legacy State File Unchanged

The installer MUST write only the new unified config. It MUST NOT modify `scripts/tui/state.json` or other legacy state files.

#### Scenario: No legacy file mutation

- GIVEN an existing `scripts/tui/state.json`
- WHEN the installer completes
- THEN `scripts/tui/state.json` MUST remain unchanged
- AND the new `config.json` is the sole output

## Non-Goals

- Building a GUI installer — TUI only
- Publishing release binaries — `dotnet run` distribution
- Detecting installed CAD versions — only provider selection (manual choice)
- Uninstalling or repairing existing config — clean install or re-run
