# mcp-cad

**Give any AI coding agent direct parametric control over Autodesk Inventor.**  
Not an app. Not a plugin. An MCP server — infrastructure for the agent-first era of CAD.

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![80+ tools](https://img.shields.io/badge/tools-80%2B-orange)](docs/tools-reference.md)

---

## Showcase

<!-- Replace with your video: upload showcase.mp4 to the repo root, then uncomment below -->
<!-- <video src="showcase.mp4" controls width="100%"></video> -->
<!-- Alternative: animated GIF -->
<!-- ![Showcase](showcase.gif) -->

[▶️ Watch showcase video](showcase.mp4)

---

## Why mcp-cad is different

| | CAD add-ins | Browser CAD | **mcp-cad** |
|---|---|---|---|
| **Model** | Plugin (paid per seat) | Web app (SaaS) | MCP server (free, open source) |
| **Runs in** | Desktop CAD only | Browser tab | Claude, OpenCode, Cursor, Windsurf, VS Code, Pi — any agent |
| **Source** | Closed | Closed | MIT — fully open |
| **CAD engines** | Tied to one vendor | None (browser-only) | Inventor now, SolidWorks & KiCad planned |
| **Control** | High-level prompts | High-level prompts | 80+ atomic tools + 22 composable skills |
| **Privacy** | Cloud-dependent | Cloud-only | Local — your data never leaves your machine |
| **Setup** | Manual per-machine | Sign up + account | One-command TUI installer |

### The problem with CAD AI tools today

Existing tools are **apps**. They force you into their UI, their workflow, their pricing model. CAD add-ins lock you into one vendor and charge per seat. Browser-based tools hold your designs on their servers. Both give you high-level "prompt-to-part" with limited control over the result — and zero transparency into what the AI is actually doing.

### What mcp-cad unlocks

**mcp-cad is not an app.** It's an MCP server that any AI coding agent can use as a tool. You stay in your agent (Claude, OpenCode, Cursor, Windsurf, VS Code, Pi) and the agent drives Inventor directly — sketch by sketch, feature by feature, parameter by parameter.

- **80+ atomic tools** — not "generate a bracket", but `sketch_line`, `extrude`, `circular_pattern`, `combine`. Full parametric control.
- **Tag-based entity resolution** — name geometry `@hole_center` and reference it reliably across operations.
- **Composable skills** — higher-level abstractions built on the atomic tools, reducing tool calls for common workflows.
- **Early-bound COM** — no `dynamic`/reflection hacks. Real type safety, real reliability.
- **Provider-agnostic** — Inventor today, SolidWorks and KiCad tomorrow. Same MCP protocol, any CAD engine.

### Built at AI speed — 8 days, 80+ tools

The entire project was built using **SDD (Spec-Driven Development)** — AI-orchestrated planning and implementation. From first commit to 80+ production tools across sketch, 3D features, assembly, work geometry, parameters, iProperties, and export — in 8 days.

```
May 26 → Jun 3, 2026
  Sketch (20 tools)      ████████████████████
  3D Features (21)       █████████████████████
  Assembly (16)          ████████████████
  Work Geometry (3)      ███
  Params & Props (9)     █████████
  Export (4)             ████
  Skills (22)            ███████████████████████
```

---

## Quick start

```powershell
git clone https://github.com/Andiveli/mcp-cad.git
cd mcp-cad

# Build and publish
dotnet publish src/McpCad.Server -c Release -o dist/mcp-cad

# Run the TUI installer — registers with OpenCode, Claude, Cursor, Windsurf, VS Code, Pi
dotnet run --project src/McpCad.Installer
```

### Prerequisites

- **OS:** Windows 10/11
- **CAD:** Autodesk Inventor 2025+
- **Runtime:** .NET 8.0 SDK

---

## How it works

```
You → AI Agent (Claude / OpenCode / Cursor / Windsurf / VS Code / Pi)
        │
        ├── "Create a gear with 24 teeth, module 2, 10mm thick"
        │
        ▼
     MCP Protocol (stdio)
        │
        ▼
   mcp-cad server (.NET 8)
        │
        ├── sketch_circle XY 0 0 47.5
        ├── sketch_circle XY 0 0 50 tag=@tip
        ├── extrude 1 10
        ├── circular_pattern extrusion="Extrusion1" axis="Y Axis" count=24
        └── ...
        │
        ▼
   Early-bound COM → Autodesk Inventor
```

---

## Architecture

```
src/
├── McpCad.Core/           Protocol & models (zero COM)
├── McpCad.Inventor/        Inventor COM backend
│   ├── Managers/            Sketch, Feature, Assembly, Parameter, Property, Export
│   └── Helpers/              TagStore, AxisResolver, EdgeResolver, ComDispatch
├── McpCad.Tools/           MCP tool definitions (AtomicTools, SkillTools)
├── McpCad.Server/          MCP stdio transport
└── McpCad.Installer/       TUI installer (Spectre.Console)
```

**Provider pattern** — same protocol, multiple CAD engines:

```
MCP → ICadProvider (connection, docs, export)
       ├── IMechanicalCadProvider (sketch, 3D, assembly)
       │   ├── InventorProvider (COM)          ← today
       │   └── SolidWorksProvider (future)
       └── IElectronicCadProvider
           └── KiCadProvider (future)
```

---

## Full tool reference

See [docs/tools-reference.md](docs/tools-reference.md) for the complete list of 80+ tools and 22 composable skills.

---

## Tags

Tag sketch entities with `@name` for reliable referencing:

```
sketch_line 0 -1 0 5 tag=eje     →  revolve profile 1 axis=@eje
sketch_circle 3 0 1 tag=perfil   →  extrude profile=@perfil
```

---

## License

MIT — free, forever.
