# mcp-cad

MCP server for Autodesk Inventor. Give AI agents direct parametric control over your CAD models.

32 tools across 7 domains — connection, documents, sketches, 3D features, parameters, iProperties, and export. All through COM automation. No VBA, no macros.

## Requirements

- **Windows** with Autodesk Inventor installed (any recent version)
- Python 3.10+
- OpenCode, Claude Desktop, or any MCP-compatible client

## Quick start

```powershell
git clone https://github.com/Andiveli/mcp-cad.git
cd mcp-cad
.\scripts\install.ps1
```

The installer creates a virtual environment, installs dependencies, runs the test suite, and auto-configures OpenCode. Open the folder in OpenCode and the server starts automatically.

## Tools

### Connection
| Tool | Description |
|------|-------------|
| `inventor_connect` | Connect to a running Inventor instance or launch a new one |
| `inventor_health` | Check connection health and document state |
| `inventor_disconnect` | Release COM reference without closing Inventor |

### Documents
| Tool | Description |
|------|-------------|
| `doc_open` | Open an existing Inventor document |
| `doc_new_part` | Create a new part document |
| `doc_new_assembly` | Create a new assembly document |
| `doc_save` | Save the active document |
| `doc_save_as` | Save to a new path |
| `doc_close` | Close the active document |

### Sketches
| Tool | Description |
|------|-------------|
| `sketch_create` | Create a new sketch on XY, XZ, or YZ plane |
| `sketch_line` | Draw a line segment |
| `sketch_circle` | Draw a circle |
| `sketch_arc` | Draw an arc |
| `sketch_rectangle` | Draw a rectangle (2-corner) |
| `sketch_dimension` | Add a dimension constraint |

### 3D Features
| Tool | Description |
|------|-------------|
| `extrude` | Extrude a sketch profile (join/cut/intersect) |
| `revolve` | Revolve a profile around an axis |
| `fillet` | Apply fillet to edges |
| `chamfer` | Apply chamfer to edges |

### Parameters
| Tool | Description |
|------|-------------|
| `param_list` | List model parameters (with optional filter) |
| `param_get` | Get a parameter value |
| `param_set` | Set a parameter value |
| `param_set_expression` | Set a parameter using an expression |

### iProperties
| Tool | Description |
|------|-------------|
| `iproperty_get` | Get an iProperty |
| `iproperty_set` | Set an iProperty |
| `iproperty_summary` | Get all Summary iProperties |
| `iproperty_custom_get` | Get a custom property |
| `iproperty_custom_set` | Set (or create) a custom property |

### Export
| Tool | Description |
|------|-------------|
| `export_step` | Export to STEP |
| `export_stl` | Export to STL |
| `export_pdf` | Export to PDF |
| `export_dxf` | Export sketch/flat pattern to DXF |

## Architecture

```
mcp_cad/
├── server.py              FastMCP instance + 32 tool registrations
├── errors.py              Exception hierarchy (InventorError → COM/permission/not-found)
├── inventor/
│   ├── client.py          COM connection lifecycle (connect, health, disconnect)
│   ├── document.py        Document operations
│   ├── sketch.py          2D sketch geometry
│   ├── feature.py         3D features
│   ├── parameter.py       Model parameters
│   ├── property.py        iProperties
│   └── export.py          STEP/STL/PDF/DXF export
└── tests/
    200 tests, COM-mocked, run on Linux
```

All managers receive the driver and access the COM object via `driver.inventor` — a property that always reflects the current connection state. No stale references.

## Development

```bash
# Run tests (works on Linux)
python -m pytest tests/ -v

# Run with coverage (needs pytest-cov in venv)
python -m pytest tests/ --cov=mcp_cad --cov-report=term-missing
```

Tests mock the entire COM layer — no Inventor installation needed for development. 200 tests covering happy paths, error scenarios, disconnected guards, and edge cases.

## Configuration

### OpenCode (auto-configured by installer)

```json
{
  "mcp": {
    "mcp-cad": {
      "type": "local",
      "command": [".venv\\Scripts\\python.exe", "-m", "mcp_cad"]
    }
  }
}
```

### Claude Desktop (manual)

```json
{
  "mcpServers": {
    "mcp-cad": {
      "command": "C:\\path\\to\\mcp-cad\\.venv\\Scripts\\python.exe",
      "args": ["-m", "mcp_cad"]
    }
  }
}
```

## Limitations

- Windows-only (Inventor COM requires native Windows)
- stdio transport only (SSE/remote transport planned)
- No Docker support (COM cannot cross container boundaries)
- Assemblies, drawings, iLogic, and sheet metal not yet implemented
