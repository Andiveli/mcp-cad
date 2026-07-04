---
name: mcp-cad-select-provider
description: "Resolve CAD provider ambiguity before modeling tools. Trigger when the user wants to create, model, or design without naming Inventor, SolidWorks, or Onshape."
---

## When to use

Call `mcp_cad_select_provider` **before** any sketch/part/assembly tool when the user message has modeling intent but **no** provider keyword (`inventor`, `onshape`, `solidworks`, `autodesk`, `sw`).

Examples that need this skill:

- "Create a part"
- "Make a bracket"
- "Design something with a fillet"

Examples that do **not** need it:

- "Create a part in Onshape"
- "Sketch a circle in Inventor"

## How to ask the user

Always confirm with the user which provider to use. On Linux, only Onshape is available for desktop-style CAD via this server; note that Inventor and SolidWorks require Windows.

Present choices by name or number, then call the tool with the chosen provider id: `inventor`, `onshape`, or `solidworks`.

## Tool

- **MCP tool**: `mcp_cad_select_provider(provider: string)`
- Sets **session-only** routing context; does **not** change `config.json`.

## After selection

Proceed with the user's modeling request using tools appropriate to the selected provider. Do not re-prompt in the same session unless the user explicitly switches provider.
