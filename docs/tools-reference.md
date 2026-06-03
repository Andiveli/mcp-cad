# mcp-cad

MCP server for CAD automation — give AI agents direct parametric control over Autodesk Inventor. Built in C#/.NET 8 with early-bound COM interop for maximum reliability.

**80+ tools** across sketch, 3D features, assembly, work geometry, parameters, iProperties, and export. Tag-based entity resolution. Interactive TUI installer.

Works with OpenCode, Claude Desktop, VS Code, and Pi.

## Requirements

- **Windows** with Autodesk Inventor 2025+ installed
- .NET 8.0 SDK

## Quick start

```powershell
git clone https://github.com/Andiveli/mcp-cad.git
cd mcp-cad

# Build and publish
dotnet publish src/McpCad.Server -c Release -o dist/mcp-cad

# Run the TUI installer
dotnet run --project src/McpCad.Installer
```

The **TUI** lets you select MCP clients (OpenCode, Claude, Pi, VS Code) via keyboard navigation and registers the server automatically.

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
| `inspect_edges` | List edges with geometry info |

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

## Development

```bash
# Build
dotnet build src/mcp-cad.sln

# Run tests (118 unit tests)
dotnet test tests/McpCad.Tests

# Integration tests (require Inventor)
dotnet test tests/McpCad.Tests --filter "FullyQualifiedName~Integration"

# Publish server
dotnet publish src/McpCad.Server -c Release -o dist/mcp-cad
```

## Tags

Tag sketch entities with `@name` for reliable referencing in feature operations:
```
sketch_line 0 -1 0 5 tag=eje     →  revolve profile 1 axis=@eje
sketch_circle 3 0 1 tag=perfil   →  extrude profile=@perfil
```

## License

MIT
