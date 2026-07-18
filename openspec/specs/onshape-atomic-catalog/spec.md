# Onshape Atomic Catalog Specification

## Purpose

Registered atomic MCP tools for Onshape: naming conventions, `CadElementAddress` contract, cm unit boundary at MCP interface, tool registration metadata, and collision-free integration with Inventor `AtomicTools`.

## Requirements

### Requirement: Onshape Naming Convention

Canonical Onshape MCP tools MUST use the `onshape_` prefix (e.g., `onshape_sketch_rectangle`, `onshape_extrude`). Unprefixed Inventor names MUST NOT appear on the Onshape MCP server. Legacy tools (`create_sketch`, `add_extrude`) MUST remain available but MUST be marked deprecated in their description with a pointer to the canonical `onshape_*` equivalent.

#### Scenario: No cross-provider name collisions

- GIVEN the MCP server registers both Inventor and Onshape tools
- WHEN a validator enumerates all registered tool names
- THEN no Onshape-registered tool name MAY match an unprefixed Inventor `AtomicTools` entry
- AND every Onshape tool MUST start with `onshape_`

#### Scenario: Legacy deprecation notice

- GIVEN `create_sketch` is called
- WHEN the tool description is read
- THEN it MUST contain `DEPRECATED: use onshape_sketch_rectangle` or equivalent
- AND calling the legacy tool MUST still work (wired to real BT path or documented delegate)

### Requirement: CadElementAddress at MCP Boundary

Part Studio and assembly operations MUST accept an `address_json` parameter conforming to the `CadElementAddress` shape: `{ documentId, workspaceOrVersionId, microversionId?, elementId }`. The MCP interface MUST NOT accept raw `did`/`eid` strings for modeling operations.

#### Scenario: Rectangle sketch with address_json

- GIVEN an agent calls `onshape_sketch_rectangle` with `address_json`
- WHEN the argument is deserialized to `CadElementAddress`
- THEN `documentId`, `workspaceOrVersionId`, and `elementId` MUST be non-empty
- AND `microversionId` MAY be present; if absent the provider MUST use the last-cached microversion

#### Scenario: address_json deserialization failure

- GIVEN `address_json` is missing required fields (`documentId` or `elementId`)
- WHEN the tool validates the argument
- THEN it MUST reject with a descriptive validation error
- AND it MUST NOT send an API request

### Requirement: Unit Boundary (cm at MCP, m at REST)

MCP tool arguments for linear dimensions MUST be in centimeters (cm). The provider MUST convert cm → meters before sending to the Onshape API. Read responses MUST convert meters → cm before returning to the agent.

#### Scenario: Extrude depth in cm

- GIVEN an agent calls `onshape_extrude` with `"depth": 5.0`
- WHEN the provider constructs the BT JSON for the Onshape API
- THEN the value in the request body MUST be `0.05` (meters)
- AND the feature response depth value MUST be converted back to cm for the agent

#### Scenario: Conversion precision

- GIVEN a dimension of `0.1` cm
- WHEN converted to meters
- THEN the result MUST be `0.001` (no floating-point truncation below 1e-6 m)
- AND when converted back it MUST equal `0.1` cm

### Requirement: Tool Registration Metadata

Each atomic MCP tool MUST be registered with:
- Name: `onshape_<verb>_<noun>` (snake_case)
- Description: one-line purpose, input and output summary
- Input schema: typed parameters with units noted in descriptions
- Output shape: documented as `Dictionary<string, object?>` success envelope

#### Scenario: onshape_sketch_rectangle registration

- GIVEN the MCP server enumerates its tool list
- WHEN `onshape_sketch_rectangle` is inspected
- THEN it MUST have `name: "onshape_sketch_rectangle"`, input schema with `address_json: object` and `entities: array`, and description mentioning "BTMSketch"
