# chamber-19-autocad-mcp

An **AutoCAD MCP (Model Context Protocol) server** that enables AI assistants such as Claude to interact with AutoCAD directly — creating drawings, querying entities, modifying geometry, and more.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Project Structure](#project-structure)
- [Requirements](#requirements)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
  - [Running the MCP Server](#running-the-mcp-server)
  - [Connecting with Claude Desktop](#connecting-with-claude-desktop)
- [Available Tools](#available-tools)
- [Development](#development)
- [License](#license)

---

## Overview

`chamber-19-autocad-mcp` is a [Model Context Protocol](https://modelcontextprotocol.io/) server that bridges AI language models with AutoCAD. It exposes AutoCAD operations as MCP tools, allowing AI assistants to:

- Open and save DWG/DXF drawings
- Create and modify geometric entities (lines, circles, arcs, polylines, etc.)
- Query and inspect drawing entities
- Manage layers, blocks, and annotations
- Export drawings to various formats

The server communicates with a locally running AutoCAD instance via COM automation (`pyautocad`) and also supports DXF file manipulation via `ezdxf`.

---

## Features

- 🤖 **AI-native** — exposes AutoCAD as MCP tools consumable by Claude and other MCP-compatible AI assistants
- 📐 **Drawing creation** — create lines, circles, arcs, rectangles, polylines, text, and more
- 🔍 **Entity inspection** — list, query, and describe all entities in a drawing
- 🏗️ **Layer management** — create, rename, toggle visibility, change colours of layers
- 💾 **File operations** — open, save, save-as, and export DWG/DXF files
- 🧩 **Block support** — insert, list, and modify blocks/symbols
- 🔄 **DXF fallback** — works with DXF files even without a running AutoCAD instance (via `ezdxf`)

---

## Project Structure

```
chamber-19-autocad-mcp/
├── server.py              # MCP server entry point
├── requirements.txt       # Python dependencies
├── .gitignore
├── autocad/
│   ├── __init__.py
│   ├── client.py          # AutoCAD COM connection helper
│   └── tools.py           # MCP tool implementations
└── README.md
```

---

## Requirements

- Python 3.10+
- AutoCAD 2018 or later (running locally on Windows) **or** DXF files only (cross-platform via `ezdxf`)
- [mcp](https://pypi.org/project/mcp/) Python library
- [pyautocad](https://pypi.org/project/pyautocad/) (Windows + AutoCAD COM automation)
- [ezdxf](https://pypi.org/project/ezdxf/) (cross-platform DXF read/write)

---

## Installation

```bash
# 1. Clone the repository
git clone https://github.com/chamber-19/chamber-19-autocad-mcp.git
cd chamber-19-autocad-mcp

# 2. Create and activate a virtual environment
python -m venv .venv
# Windows
.venv\Scripts\activate
# macOS / Linux
source .venv/bin/activate

# 3. Install dependencies
pip install -r requirements.txt
```

---

## Configuration

The server reads the following optional **environment variables**:

| Variable | Default | Description |
|---|---|---|
| `AUTOCAD_COM_ENABLED` | `true` | Set to `false` to disable COM automation and use DXF-only mode |
| `AUTOCAD_DEFAULT_DXF` | _(none)_ | Path to a default DXF file opened on startup |
| `MCP_SERVER_NAME` | `autocad-mcp` | Name reported to MCP clients |

---

## Usage

### Running the MCP Server

```bash
python server.py
```

The server listens on **stdio** (standard MCP transport) by default, ready to be consumed by an MCP client.

### Connecting with Claude Desktop

Add the following block to your Claude Desktop configuration file (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "autocad": {
      "command": "python",
      "args": ["/absolute/path/to/chamber-19-autocad-mcp/server.py"]
    }
  }
}
```

Restart Claude Desktop and the AutoCAD tools will appear in the tool palette.

---

## Available Tools

| Tool | Description |
|---|---|
| `autocad_new_drawing` | Create a new empty drawing |
| `autocad_open_file` | Open an existing DWG or DXF file |
| `autocad_save_file` | Save the current drawing |
| `autocad_list_entities` | List all entities in the current drawing (optionally filtered by type or layer) |
| `autocad_draw_line` | Draw a line between two points |
| `autocad_draw_circle` | Draw a circle given a centre point and radius |
| `autocad_draw_arc` | Draw an arc given centre, radius, start angle, and end angle |
| `autocad_draw_polyline` | Draw a polyline through a list of points |
| `autocad_draw_text` | Place a single-line text annotation |
| `autocad_draw_mtext` | Place a multi-line text annotation |
| `autocad_move_entity` | Move one or more entities by a displacement vector |
| `autocad_delete_entity` | Delete one or more entities by handle |
| `autocad_list_layers` | List all layers with their properties |
| `autocad_create_layer` | Create a new layer |
| `autocad_set_layer` | Set the current active layer |
| `autocad_get_entity_properties` | Return the full property set of a single entity |

---

## Development

```bash
# Run linter
pip install ruff
ruff check .

# Run tests
pip install pytest
pytest tests/
```

Contributions welcome — please open an issue or pull request.

---

## License

MIT License. See [LICENSE](LICENSE) for details.