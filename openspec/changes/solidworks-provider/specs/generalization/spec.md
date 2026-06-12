# Generalization of Public MCP Surface — Spec

**Change**: solidworks-provider
**Related**: pluggable-server, solidworks-basic-loop
**Strict TDD**: Enabled (all acceptance criteria require tests-first; no implementation change without covering test)

## Purpose
Generalize the public MCP tool surface (primarily the three connection tools) from Inventor-specific names/descriptions to CAD-neutral names while preserving full backward compatibility via deprecated aliases. This enables a single set of tools to work across providers selected by config. Updates to docs, skills, and internal references are included. All other ~80+ tools remain unchanged as they are already neutral.

## Requirements

### Requirement: Neutral Connection Tool Names and Descriptions
AtomicTools SHALL expose primary connection methods named `cad_connect`, `cad_disconnect`, `cad_health` (snake_case to match existing convention).
- Descriptions SHALL be CAD-neutral (e.g., "Connect to the running CAD application.", "Check CAD connection health and active document state.").
- The `[McpServerTool]` and `[Description]` attributes SHALL be on the neutral methods.
- Method signatures and behavior SHALL be identical to the prior inventor_* equivalents (delegating to `IMechanicalCadProvider.Connect/Disconnect/Health`).

#### Scenario: Neutral names registered as primary tools
- GIVEN the server starts with any provider
- WHEN the tool list is inspected (via MCP or reflection)
- THEN `cad_connect`, `cad_disconnect`, `cad_health` are present with neutral descriptions
- AND the old inventor_* names are NOT the primary (they exist only as aliases)

#### Scenario: Neutral names delegate correctly
- GIVEN AtomicTools instance with injected IMechanicalCadProvider (mock or real)
- WHEN `cad_connect()` is invoked
- THEN it calls provider.Connect() and returns the standardized Dictionary envelope
- AND ToolHelpers.Catch wraps errors

### Requirement: Deprecated Aliases for inventor_*
AtomicTools SHALL retain `inventor_connect`, `inventor_disconnect`, `inventor_health` as public methods.
- These SHALL be annotated with `[Obsolete("Use cad_connect (and Cad:Provider config) instead.")]`.
- Implementation SHALL delegate to the neutral implementation (or directly to provider) so behavior is identical (including success/error envelopes).
- No duplication of logic.

#### Scenario: Legacy alias calls succeed identically
- GIVEN existing client code / skills / agent prompts using `inventor_connect`
- WHEN the call is executed against the server (Inventor or SolidWorks selected)
- THEN it succeeds with the exact same result shape and side-effects as before the change
- AND a deprecation warning is observable in metadata or logs (at minimum the Obsolete attribute is present for compile-time in .NET hosts)

#### Scenario: Alias and neutral coexist
- GIVEN tests or runtime
- WHEN both `tools.inventor_health()` and `tools.cad_health()` are called
- THEN both return success (when connected) and the CallLog (in mocks) shows provider method invoked (once per call)

### Requirement: Update Internal References and Tests for Neutral Preference
- ToolRegistrationTests, AtomicToolsTests, and any other tests SHALL be updated (or augmented) to assert on neutral names preferentially while also verifying aliases continue to work.
- DI tests that resolve tools and call health SHALL use neutral names where new code is added; legacy calls in tests may remain temporarily with comments.
- ErrorResult / exception string expectations (e.g. "InventorComException") SHALL tolerate both the alias type name and the primary Cad* name during transition (or be updated to check base type).

#### Scenario: Tool count and attribute tests pass post-rename
- GIVEN the post-generalization AtomicTools
- WHEN reflection enumerates public instance methods declared on the type
- THEN count remains the same (aliases add no net new unique operations)
- AND every method (including aliases) has [McpServerTool] and [Description] (aliases may inherit or duplicate attr as needed for registration)

### Requirement: Documentation and Skill Surface Updates (Generalization)
- README.md and docs/tools-reference.md SHALL document `cad_*` as the primary/recommended connection tools.
- A "Migration from inventor_*" section SHALL be added explaining aliases, config-driven backend, and recommended updates for new work.
- Existing skills (e.g. inventor-new-part) MAY retain legacy references during this increment but SHALL include notes or be accompanied by neutral examples. New neutral-named skills are out-of-scope for full rewrite.
- appsettings.json template and any published examples SHALL show the new Cad section (while keeping legacy Inventor section for transition).

#### Scenario: Docs reflect agnostic model
- GIVEN a reader following the updated tools-reference
- WHEN looking at Connection section
- THEN primary table shows cad_connect / cad_health / cad_disconnect with neutral text
- AND a note/callout explains the legacy inventor_* aliases and how to select provider via "Cad:Provider"

### Requirement: Strict TDD for Generalization
- Unit tests covering rename (method presence, attributes, delegation, Obsolete presence, alias delegation) SHALL be written or updated BEFORE or in lockstep with the AtomicTools.cs changes.
- Contract/registration tests SHALL pass for both name sets.
- Existing integration tests using live CAD SHALL continue to pass (using whichever names they currently use; preferably migrate calls in test updates).

#### Edge Cases and Error Conditions
- Calling alias on a non-default provider still works (aliases are in the tool layer, not provider-specific).
- Description text changes do not affect tool invocation (MCP uses the attribute at registration time).
- Duplicate registration or name collision is prevented by the MCP framework; aliases are distinct method names.
- If Obsolete is triggered in a hosting environment that treats warnings as errors, the alias path remains usable at runtime.

## Non-Goals (for Generalization slice)
- Renaming any of the other 80+ neutral tools (doc_*, sketch_*, extrude, etc.).
- Changing IMechanicalCadProvider or ICadProvider method names/signatures.
- Removing the inventor_* aliases (removal is a future increment after migration window).
- Full skill rewrite or mass update of all historical prompts/docs.

## Risks Addressed
- User breakage from rename: mitigated by strong aliases + docs + default Inventor.
- Test pollution: addressed by explicit dual verification in acceptance scenarios and TDD requirement.
- Inconsistent health response keys (e.g. "inventor_version"): noted as follow-up; for this increment, Health implementation in drivers may retain provider-specific keys when appropriate (neutral "cad_version" or "version" + "provider" key is acceptable but not mandated here).

## References
- Proposal: openspec/changes/solidworks-provider/proposal.md (locked decision, scope in/out, backward compat strategy).
- Prior: cad-provider-protocol spec (contracts already neutral), weld-feature (provider-agnostic precedent).
- Code: AtomicTools.cs, ToolRegistrationTests.cs, AtomicToolsTests.cs, Program.cs, ToolHelpers.cs, ErrorResult.cs.
