# mcp-cad

MCP server for CAD automation — give AI agents direct parametric control over Autodesk Inventor. Built in C#/.NET 8 with early-bound COM interop for maximum reliability.

**80+ tools** across sketch, 3D features, assembly, work geometry, parameters, iProperties, and export. Tag-based entity resolution. Interactive TUI installer.

Works with OpenCode, Claude Desktop, Cursor, Grok, VS Code, Pi, and others.

## Requirements

- **Windows** with Autodesk Inventor 2025+ installed

For the portable release: nothing else (self-contained binaries include everything needed).

For building from source: .NET 8 SDK.

## Quick start (easiest for end users)

1. Download the latest portable release from GitHub Releases.
2. Extract the zip.
3. Double-click `McpCad-Install.bat` (or run `McpCad.Installer.exe`).
4. Toggle the AI clients you use with Space, then press Enter on them.
5. Restart your AI client (Claude Desktop, Cursor…). Keep Inventor running.

No git, no `dotnet`, no manual JSON editing.

The TUI (or `--recommended` / `--all` flags) automatically registers `mcp-cad` in the supported clients' MCP configuration.

For developers building from source, see the main [README.md](../README.md).

## Tools

### Connection
| Tool | Description |
|------|-------------|
| `inventor_connect` | Connect to running Inventor instance |
| `inventor_health` | Check connection and document state |
| `inventor_disconnect` | Release COM reference |

### Documents
| Tool | Description |
|------|-------------|
| `doc_new_part` | Create new part document |
| `doc_new_assembly` | Create new assembly document |
| `doc_open` | Open existing document |
| `doc_save` | Save active document |
| `doc_save_as` | Save to new path |
| `doc_close` | Close active document |

### Sketch
| Tool | Description |
|------|-------------|
| `sketch_create` | Create sketch on XY/XZ/YZ |
| `sketch_line` | Draw line segment |
| `sketch_circle` | Draw circle |
| `sketch_arc` | Draw arc |
| `sketch_rectangle` | Draw rectangle |
| `sketch_point` | Draw point |
| `sketch_ellipse` | Draw ellipse |
| `sketch_spline` | Draw spline |
| `sketch_dimension` | Add dimension constraint |
| `sketch_constraint` | Add geometric constraint |
| `sketch_offset` | Offset entities |
| `sketch_move` | Move entities |
| `sketch_rotate` | Rotate entities |
| `sketch_mirror` | Mirror entities |
| `sketch_scale` | Scale entities |
| `sketch_trim` | Trim entity at intersection |
| `sketch_profiles` | List closed profiles in sketch |
| `sketch_circular_pattern` | Circular pattern in sketch |
| `sketch_rectangular_pattern` | Rectangular pattern in sketch |
| `sketch_delete` | Delete active sketch |

### 3D Features
| Tool | Description |
|------|-------------|
| `extrude` | Extrude profile (new body/join/cut/intersect) |
| `revolve` | Revolve profile around axis |
| `sweep` | Sweep profile along path |
| `loft` | Loft between two or more profiles |
| `coil` | Create coil (spring) feature |
| `rib` | Create rib (reinforcement) |
| `fillet` | Apply fillet to edges |
| `chamfer` | Apply chamfer to edges |
| `hole` | Create hole feature |
| `thread` | Create thread on cylindrical face |
| `shell` | Hollow part by removing faces |
| `thicken` | Offset faces to create solid |
| `draft` | Apply draft angle to faces |
| `split` | Split body with work plane |
| `combine` | Boolean join/cut/intersect bodies |
| `mirror_feature` | Mirror feature across plane |
| `emboss` | Emboss profile onto face |
| `derive` | Derive part from external file |
| `circular_pattern` | Circular pattern of 3D feature |
| `rectangular_pattern` | Rectangular pattern of 3D feature |

### Work Geometry
| Tool | Description |
|------|-------------|
| `work_plane` | Create work plane (offset, 3-point, normal-to-curve) |
| `work_axis` | Create work axis (default, 2-point, normal-to-plane, along-edge) |
| `work_point` | Create work point (coordinates, on-curve, intersection) |

### Assembly
| Tool | Description |
|------|-------------|
| `asm_place_component` | Place component into assembly |
| `asm_ground_component` | Ground (fix) component in place |
| `asm_replace_component` | Replace occurrence with different part |
| `asm_list_components` | List placed components |
| `asm_bom` | Bill of materials |
| `asm_constraint_mate` | Mate constraint |
| `asm_constraint_flush` | Flush constraint |
| `asm_constraint_angle` | Angle constraint |
| `asm_constraint_insert` | Insert constraint (concentric + planar) |
| `asm_constraint_tangent` | Tangent constraint |
| `asm_list_constraints` | List all constraints |
| `asm_delete_constraint` | Delete constraint by name or index |
| `asm_pattern_circular` | Circular pattern of occurrence |
| `asm_pattern_rectangular` | Rectangular pattern of occurrence |
| `asm_extrude_cut` | Extrude cut across components |
| `asm_hole` | Hole at assembly level |

### Parameters & Properties
| Tool | Description |
|------|-------------|
| `param_list` | List model parameters |
| `param_get` | Get parameter value |
| `param_set` | Set parameter value |
| `param_set_expression` | Set parameter by expression |
| `iproperty_get` | Get iProperty |
| `iproperty_set` | Set iProperty |
| `iproperty_summary` | Get summary properties |
| `iproperty_custom_get` | Get custom iProperty |
| `iproperty_custom_set` | Set custom iProperty |

### Inspection & Verification

These tools let agents **observe and verify** what actually happened in Inventor after modeling operations (the critical feedback loop for reliable agent-driven CAD).

mcp-cad supports two complementary approaches:

- **Visual / multimodal feedback**: `capture_viewport_image` returns Base64 PNG screenshots of the 3D viewport (supports "Iso", "Front", "Top", "Right", "Bottom", "Back", "Left", "Current", etc.). Vision models can directly inspect the rendered result (crown shape, ring grooves, internal bosses, proportions, etc.).
- **Structured / data-driven feedback**: `get_feature_tree` (the "Árbol de Operaciones"), `get_bounding_box`, and `inspect_edges` provide exact, machine-readable information without relying on vision.

| Tool | Description |
|------|-------------|
| `capture_viewport_image` | Capture screenshot of the active viewport. Parameters: `view` (default "Iso"), `width`, `height`, `format`. Returns Base64 image data. |
| `get_feature_tree` | Return the structured feature/operation tree (features for parts, occurrences for assemblies). Includes name, type, suppressed state, and health. |
| `get_bounding_box` | Return precise bounds (min, max, size, center) for the whole model or a specific target. |
| `inspect_edges` | List edges of the active body with geometry information (useful for selection and detailed measurement). |

**Typical pattern** (tested in practice):
1. Perform one or more modeling operations.
2. Immediately call `capture_viewport_image` (one or more views) + `get_feature_tree` + `get_bounding_box`.
3. The agent compares the returned state against the intended result and corrects in subsequent steps if needed.

See also `inventor_health` for quick connection + active document status.

### Export
| Tool | Description |
|------|-------------|
| `export_step` | Export to STEP (.stp) |
| `export_stl` | Export to STL (.stl) |
| `export_pdf` | Export to PDF |
| `export_dxf` | Export to DXF |

### Skills (composable)
| Skill | Description |
|-------|-------------|
| `skill_sketch` | Create or activate sketch on work plane |
| `skill_line` | Draw line (simple/midpoint modes) |
| `skill_circle` | Draw circle (center/3point modes) |
| `skill_arc` | Draw arc (center/sweep/3point) |
| `skill_rect` | Draw rectangle (diagonal/center) |
| `skill_point` | Draw point |
| `skill_ellipse` | Draw ellipse |
| `skill_spline` | Draw spline through fit points |
| `skill_extrude` | Extrude with auto-default profile |
| `skill_revolve` | Revolve with auto-drawn profile + axis |
| `skill_sweep` | Sweep profile along path |
| `skill_offset` | Offset entities |
| `skill_mirror` | Mirror sketch entities |
| `skill_move` | Move sketch entities |
| `skill_rotate` | Rotate sketch entities |
| `skill_scale` | Scale sketch entities |
| `skill_trim` | Trim lines at intersection |
| `skill_constraint` | Add geometric constraint |
| `skill_dimension` | Add dimension constraint |
| `skill_pattern_circular` | Circular pattern of sketch entities |
| `skill_pattern_rectangular` | Rectangular pattern of sketch entities |
| `skill_delete_sketch` | Delete active sketch |

## Architecture

```
src/
├── McpCad.Core/           Protocol & models (zero COM)
├── McpCad.Inventor/        Inventor COM backend
│   ├── Managers/            Sketch, Feature, Parameter, Property, Export
│   ├── Helpers/              TagStore, AxisResolver, EdgeResolver, ComDispatch
│   └── InventorDriver.cs    COM lifecycle (GetActiveObject via oleaut32)
├── McpCad.Tools/           MCP tool definitions (AtomicTools, SkillTools)
├── McpCad.Server/          Console app — MCP stdio transport
├── McpCad.Installer/       TUI installer (Spectre.Console)
└── McpCad.sln
```

### Provider pattern

```
MCP → McpCad.Server → ICadProvider (common: connection, docs, export)
                     ├── IMechanicalCadProvider (sketch, 3D features, …)
                     │   ├── InventorProvider (COM)
                     │   └── SolidWorksProvider (future)
                     └── IElectronicCadProvider (KiCad, future)
```

## Configuration

Run the TUI installer (`dotnet run --project src/McpCad.Installer`) to register automatically. Manual config examples:

### OpenCode (`~/.config/opencode/opencode.json`)
```json
{
  "mcp": {
    "mcp-cad": {
      "type": "local",
      "command": ["path/to/dist/mcp-cad/McpCad.Server.exe"]
    }
  }
}
```

### Claude / Pi (`mcpServers`)
```json
{
  "mcpServers": {
    "mcp-cad": {
      "command": "path/to/dist/mcp-cad/McpCad.Server.exe",
      "args": []
    }
  }
}
```

### VS Code (`servers`)
```json
{
  "servers": {
    "mcp-cad": {
      "command": "path/to/dist/mcp-cad/McpCad.Server.exe",
      "args": []
    }
  }
}
```

### Cursor (`~/.cursor/mcp.json`)
```json
{
  "mcpServers": {
    "mcp-cad": {
      "command": "path/to/dist/mcp-cad/McpCad.Server.exe",
      "args": []
    }
  }
}
```

### Grok CLI / TUI (`~/.grok/config.toml`)
```toml
[mcp_servers.mcp-cad]
command = "path/to/dist/mcp-cad/McpCad.Server.exe"
args = []
```

When you run the installer and select any agent (Grok, Cursor, Claude, VS Code, OpenCode, Pi...), it automatically:
- Registers the mcp-cad MCP server for that client
- Copies the CAD skills (`macro-basic-part`, `inventor-new-part`, `macro-selector`, ...) from the package into that agent's skills directory (e.g. `~/.grok/skills/`, `~/.cursor/skills/`, `%APPDATA%/Claude/skills/`, etc.).

The "CAD Skills" item deploys them to every supported agent at once. The skills become globally/native to the agent.

## Development

```bash
# Build
dotnet build src/mcp-cad.sln

# Run tests (118 unit tests)
dotnet test tests/McpCad.Tests

# Integration tests (require Inventor)
dotnet test tests/McpCad.Tests --filter "FullyQualifiedName~Integration"

# Publish server + installer (self-contained single-file, best for distribution)
dotnet publish src/McpCad.Server   -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/mcp-cad
dotnet publish src/McpCad.Installer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/mcp-cad
```

## Tags

Tag sketch entities with `@name` for reliable referencing in feature operations:
```
sketch_line 0 -1 0 5 tag=eje     →  revolve profile 1 axis=@eje
sketch_circle 3 0 1 tag=perfil   →  extrude profile=@perfil
```

## License

MIT
