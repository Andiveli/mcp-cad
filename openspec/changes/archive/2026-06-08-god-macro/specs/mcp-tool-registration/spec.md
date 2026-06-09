# Delta for mcp-tool-registration

## ADDED Requirements

### Requirement: macro_god_part Tool Registration

The system MUST register `macro_god_part` as a new MCP tool in `src/McpCad.Tools/MacroTools.cs` using the `[McpServerTool]` attribute pattern. Every parameter MUST have a rich `[Description]` attribute.

#### Scenario: Tool is discoverable via MCP

- GIVEN the MCP server is running
- WHEN the tool list is retrieved
- THEN `macro_god_part` is listed among available tools
- AND it accepts all documented parameters

#### Scenario: Description attributes present

- GIVEN the macro_god_part method declaration
- WHEN inspected via reflection
- THEN every parameter has a non-empty [Description] attribute
- AND the method has a [Description] on the tool itself

## MODIFIED Requirements

### Requirement: Generic register_tools Function

The system SHALL provide a `register_tools(mcp_instance: FastMCP, provider: CADProvider)` function in `mcp_cad/tools/__init__.py` that registers all 33 MCP tools on the FastMCP instance.

(Previously: registered all 32 MCP tools on the FastMCP instance)

#### Scenario: register_tools accepts protocol only

- GIVEN the `register_tools` function
- WHEN called with `mcp_instance` and a `CADProvider`
- THEN it MUST register all 33 tools with identical names and signatures as before
- AND it MUST NOT accept individual manager instances (driver, doc_mgr, sketch_mgr, etc.)

#### Scenario: All 33 tools preserved

- GIVEN the new `register_tools` function
- WHEN tools are registered
- THEN the following 33 tools MUST exist with identical signatures:
  `inventor_connect`, `inventor_health`, `inventor_disconnect`,
  `doc_open`, `doc_new_part`, `doc_new_assembly`, `doc_save`, `doc_save_as`, `doc_close`,
  `sketch_create`, `sketch_line`, `sketch_circle`, `sketch_arc`, `sketch_rectangle`, `sketch_dimension`,
  `extrude`, `revolve`, `fillet`, `chamfer`,
  `param_list`, `param_get`, `param_set`, `param_set_expression`,
  `iproperty_get`, `iproperty_set`, `iproperty_summary`, `iproperty_custom_get`, `iproperty_custom_set`,
  `export_step`, `export_stl`, `export_pdf`, `export_dxf`,
  `macro_god_part`
- AND `macro_god_part` follows the same error envelope pattern as all other tools

## REMOVED Requirements (Non-Goals)

### Non-Goal: Adding New MCP Tools

(Reason: this change adds macro_god_part — the "no new tools" non-goal no longer applies)
