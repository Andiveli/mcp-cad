# Obsolete canonical specs (do not use)

The files in this directory (`cad-provider-protocol`, `mcp-tool-registration`, etc.) describe the **abandoned Python prototype** (`mcp_cad/core/protocol.py`, FastMCP). The product is **C# / .NET 8** under `src/McpCad.*`.

**Authoritative sources for SDD and implementation:**

- `openspec/config.yaml` — stack and conventions
- `openspec/changes/*/specs/` — active change deltas (e.g. `dynamic-provider-routing`)
- `openspec/changes/onshape-provider/specs/` — C# Onshape/routing specs
- Source: `src/McpCad.Core` interfaces (`ICadProvider`, `IMechanicalCadProvider`, `ICadWebProvider`)

Do not cite Python paths or `CADProvider` ABC from this folder in new design, tasks, or code reviews.
