# Select Provider Skill Specification

## Purpose

Defines `mcp-cad-select-provider`, an agent skill that resolves provider ambiguity when a user makes a modeling request without naming a CAD provider. The skill prompts the user to choose, persists the choice for the current session only, and sets the execution context for subsequent tool calls.

## Requirements

### Requirement: Skill Trigger Heuristic

The skill MUST fire when a user request contains modeling intent keywords but NO explicit provider keyword (inventor, onshape, solidworks, or their case variants).

#### Scenario: Skill fires on ambiguous request

- GIVEN the user sends: *"create a part"*
- WHEN the agent interprets the request
- THEN the skill MUST trigger with a provider selection prompt
- AND it MUST NOT execute any modeling tool before the user selects a provider

#### Scenario: Skill does NOT fire with explicit provider

- GIVEN the user sends: *"create a part in Onshape"*
- WHEN the agent interprets the request
- THEN the skill MUST NOT trigger
- AND the request MUST route directly to the Onshape provider tools

#### Scenario: Skill trigger keywords

- GIVEN a user request
- WHEN the skill checks for trigger keywords
- THEN it MUST match: "create", "make", "model", "design", "draw", "build", "extrude", "fillet", "sketch", "part", "assembly"
- AND it MUST NOT match if any of "inventor", "onshape", "solidworks", "autodesk", "sw", "cad" appear as a provider keyword

### Requirement: Provider Selection Prompt

The skill MUST present a list of available CAD providers for the current platform and let the user pick one.

#### Scenario: Selection on Windows

- GIVEN the skill triggers on Windows
- WHEN the provider prompt is shown
- THEN it MUST list: **1. Inventor 2. SolidWorks 3. Onshape**
- AND each entry MUST be clickable/typeable by number or name

#### Scenario: Selection on Linux

- GIVEN the skill triggers on Linux
- WHEN the provider prompt is shown
- THEN it MUST list: **1. Onshape**
- AND it MUST NOT offer desktop CAD options
- AND a note SHALL explain: *"Desktop CAD (Inventor, SolidWorks) requires Windows"*

### Requirement: Session-Only Persistence

The selected provider MUST be persisted only for the current conversation/session. The `active_provider` in `config.json` MUST NOT be modified by the skill.

#### Scenario: Session state

- GIVEN the user selects "Onshape" via the skill
- WHEN the next tool call is made in the same conversation
- THEN the tool MUST route to Onshape without re-prompting
- AND `config.json`'s `active_provider` MUST remain unchanged

#### Scenario: New session resets

- GIVEN a conversation where the user previously selected "Inventor"
- WHEN a new MCP session starts
- THEN the skill MUST NOT remember the previous selection
- AND the default from `config.json` (or `appsettings.json`) MUST apply for the first ambiguous request in the new session

### Requirement: Execution Context for Routing

The skill MUST set the selected provider in the execution context (`AsyncLocal<CadExecutionContext>`) so subsequent tool calls dispatch through the correct provider.

#### Scenario: Context is set after selection

- GIVEN the user selects "Inventor" from the prompt
- WHEN the skill handler returns
- THEN `CadExecutionContext.CurrentProvider` MUST be `"inventor"`
- AND the next tool call (e.g., `create_sketch`) MUST resolve via `IProviderRouter` to the Inventor backend

#### Scenario: Context is per-call on ambiguous requests

- GIVEN the user makes a request without a provider keyword
- WHEN the skill sets the context
- THEN the context MUST scope to the current tool call only (or the current conversation turn)
- AND an explicit provider keyword in a later request MUST override the skill's context

### Requirement: Skill Registration

The `mcp_cad_select_provider` tool MUST be registered as both an MCP tool and an agent skill file.

#### Scenario: Tool registration

- GIVEN the server starts
- WHEN tools are enumerated
- THEN `mcp_cad_select_provider` MUST appear in the tool list
- AND it MUST accept an optional `provider` parameter (for direct invocation without prompt) and a `request` parameter (the original user message)

#### Scenario: Skill file presence

- GIVEN a file listing of the skills directory
- WHEN the skill system initializes
- THEN a skill file `mcp-cad-select-provider.md` or equivalent MUST be present
- AND it MUST describe the trigger keywords, prompt format, and usage example for instructing the agent

### Requirement: Suppression via Config

Users MUST be able to suppress the skill via config, disabling the ambiguous-request prompt.

#### Scenario: Skill disabled

- GIVEN `config.json` contains `"auto_select_provider": true` or equivalent suppression flag
- WHEN an ambiguous request arrives
- THEN the skill MUST NOT fire
- AND the server MUST use `active_provider` from config as the default

## Non-Goals

- Persisting skill selection to `config.json` — session-only per product decision
- Detecting provider from context beyond keywords — keyword-based heuristic only
- Fallback to a default without prompting when skill is active — skill always asks per "always ask" product decision
- Supporting multiple simultaneous provider selections within one conversation — one provider per tool call
