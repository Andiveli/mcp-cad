# mcp-cad

MCP server for CAD automation. Give AI agents direct parametric control over your CAD models — starting with Autodesk Inventor, architected for AutoCAD, SolidWorks, Revit, KiCad, and more.

**46 atomic tools** + **19 composable skills** across 8 domains. Provider-based architecture: add a new CAD backend by implementing one protocol. No VBA, no macros.

## Requirements

- **Windows** with Autodesk Inventor installed (any recent version)
- Python 3.10+
- OpenCode, Claude Desktop, VS Code (Copilot Chat), Pi, or any MCP-compatible client

## Quick start

```powershell
git clone https://github.com/Andiveli/mcp-cad.git
cd mcp-cad

# Interactive installation (recommended)
.\scripts\install.ps1                # creates venv, installs dependencies, runs tests
python -m scripts.tui                # TUI menu — select agents to register

# Or: non-interactive (OpenCode only)
.\scripts\install.ps1 -RegisterIn OpenCode
```

The installer creates a virtual environment, installs dependencies, and runs the test suite. The **TUI** lets you choose which MCP clients to register (OpenCode, Claude Desktop, Pi, VS Code) via an interactive terminal menu. Open the folder in your agent and the server starts automatically.

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
| `fillet` | Apply fillet to specific edges |
| `chamfer` | Apply chamfer to specific edges |

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

### Skills (composable operations)

| Panel | Skills |
|-------|--------|
| **Sketch → Draw** | `skill_sketch`, `skill_point`, `skill_line`, `skill_circle`, `skill_arc`, `skill_rect`, `skill_ellipse`, `skill_spline` |
| **Sketch → Pattern** | `skill_pattern_circular`, `skill_pattern_rectangular` |
| **Sketch → Modify** | `skill_offset`, `skill_move`, `skill_rotate`, `skill_scale`, `skill_trim`, `skill_mirror`, `skill_delete_sketch` |
| **Sketch → Constrain** | `skill_constraint` (12 modes), `skill_dimension` |

Skills chain atomic tools into reliable multi-step operations. Define a skill once and the LLM calls it as a single tool — no round-trips, no hallucinations. Each skill includes usage examples in its docstring so the LLM never guesses parameter formats.

## Architecture

```
mcp_cad/
├── server.py                     FastMCP instance — 56 lines
├── errors.py                     Exception hierarchy
├── core/                         Abstract protocol (zero COM dependencies)
│   ├── protocol.py               CADProvider — 32 methods
│   └── models.py                 Point2D, Plane, ExtrudeDef, …
├── providers/                    CAD backends
│   └── inventor/                 Inventor via COM (pywin32)
│       ├── adapter.py            InventorProvider implements CADProvider
│       ├── client.py             COM lifecycle
│       ├── document.py           Documents
│       ├── sketch.py             2D geometry
│       ├── feature.py            3D features
│       ├── parameter.py          Model parameters
│       ├── property.py           iProperties
│       └── export.py             STEP/STL/PDF/DXF
├── tools/                        Generic MCP tools (backend-agnostic)
│   ├── connection.py             connect / health / disconnect
│   ├── documents.py              open / new / save / close
│   ├── sketches.py               line / circle / arc / rectangle / dimension
│   ├── features.py               extrude / revolve / fillet / chamfer
│   ├── parameters.py             list / get / set / expression
│   ├── properties.py             iProperty operations
│   └── export.py                 STEP / STL / PDF / DXF
├── skills/                       Composable operations
│   ├── base.py                   Skill base + SkillResult
│   ├── sketch.py                 skill_sketch
│   ├── line.py                   skill_line
│   ├── circle.py                 skill_circle
│   ├── arc.py                    skill_arc
│   ├── rect.py                   skill_rect
│   ├── point.py                  skill_point
│   ├── ellipse.py                skill_ellipse
│   ├── spline.py                 skill_spline
│   ├── pattern.py                skill_pattern_circular/rectangular
│   ├── modify.py                 skill_offset/move/rotate/scale/trim/mirror/delete
│   ├── constrain.py              skill_constraint (12 modes)
│   └── dimension.py              skill_dimension
└── tests/
      394 tests, COM-mocked, run on Linux
```

### How it works

```
MCP client → server.py → tools/ (generic) → CADProvider (protocol) → providers/inventor (COM)
                                                                    → providers/solidworks (COM)
                                                                    → providers/kicad (Python API)
```

Tools and skills depend only on `CADProvider`. Adding a new CAD backend means writing one adapter — zero changes to tools, skills, or server.

### Skills

Skills compose atomic tools into reliable multi-step operations:

```python
# Define once — LLM calls as single tool
def skill_rect(provider, mode="diagonal", x1=0, y1=0, x2=10, y2=10, **kwargs):
    if mode == "diagonal":
        return provider.sketch_rectangle(x1, y1, x2, y2)
    elif mode == "center":
        opp_x = 2.0 * kwargs["cx"] - kwargs["corner_x"]
        opp_y = 2.0 * kwargs["cy"] - kwargs["corner_y"]
        return provider.sketch_rectangle(kwargs["corner_x"], kwargs["corner_y"], opp_x, opp_y)
```

No round-trips to the LLM mid-operation. No hallucinated tool sequences. Deterministic and fast.

## Development

```bash
# Install dev dependencies
pip install -e ".[test]"

# Run tests (works on Linux — no Inventor needed)
python -m pytest tests/ -v

# Run with coverage
python -m pytest tests/ --cov=mcp_cad --cov-report=term-missing

# Verify server starts correctly (cross-client diagnostic)
python -m mcp_cad --version
```

394 tests covering happy paths, error scenarios, delegation verification, skills composition, and disconnected guards. Entire COM layer is mocked — cross-platform development.

## Configuration

The **TUI** (`python -m scripts.tui`) registers mcp-cad in one or more agents with a single interactive menu. Manual configs below for reference.

### OpenCode

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

### Claude Desktop

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

### Pi (IntelliJ agent)

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

Config location: `~/.pi/agent/mcp.json`

### VS Code (GitHub Copilot Chat)

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

Config location: `.vscode/mcp.json` (workspace-level)

## Limitations

- Windows-only (Inventor COM requires native Windows)
- stdio transport only (SSE/remote transport planned)
- No Docker support (COM cannot cross container boundaries)
- Assemblies, drawings, iLogic, and sheet metal not yet implemented
- HoleFeatures, CircularPatternFeatures, and ThreadFeatures blocked by COM bridge type conversion (see `docs/com-bridge-investigation.md` for workarounds and research paths)
