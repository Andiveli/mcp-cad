# mcp-cad

MCP server for CAD automation — give AI agents direct parametric control over Autodesk Inventor. Built in C#/.NET 8 with early-bound COM interop for maximum reliability.

**28+ tools** across sketch, 3D features, parameters, iProperties, and export. Tag-based entity resolution. Interactive TUI installer.

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
| `sketch_circular_pattern` | Circular pattern in sketch |
| `sketch_rectangular_pattern` | Rectangular pattern in sketch |
| `sketch_delete` | Delete active sketch |

### 3D Features
| Tool | Description |
|------|-------------|
| `extrude` | Extrude profile (new body/join/cut/intersect) |
| `revolve` | Revolve profile around axis |
| `fillet` | Apply fillet to edges |
| `chamfer` | Apply chamfer to edges |
| `hole` | Create hole feature |
| `thread` | Create thread on face |
| `circular_pattern` | Circular pattern of 3D feature |
| `inspect_edges` | List edges with geometry info |

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
| `skill_extrude` | Extrude with auto-default profile |
| `skill_revolve` | Revolve with auto-drawn profile + axis |
| `skill_line` | Draw line (simple/midpoint modes) |
| `skill_circle` | Draw circle (center/3point modes) |
| `skill_arc` | Draw arc (center/sweep/3point) |
| `skill_rect` | Draw rectangle (diagonal/center) |
| `skill_offset` | Offset entities |
| `skill_mirror` | Mirror entities |
| `skill_trim` | Trim lines at intersection |

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
MCP client → McpCad.Server → ICadProvider (protocol)
                            → InventorProvider (COM via Interop)
                            → MockInventorProvider (tests)
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
