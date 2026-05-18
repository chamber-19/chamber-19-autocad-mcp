# Chamber-19 AutoCAD Ecosystem

See [autocad-knowledge/ECOSYSTEM.md](../autocad-knowledge/ECOSYSTEM.md) for the full map.

## This repo: `chamber-19-autocad-mcp`

MCP server exposing AutoCAD drawing operations to Claude Desktop and Claude Code.

### Related repos

| Repo | Relationship |
|------|-------------|
| [autocad-knowledge](../autocad-knowledge) | Patterns, gotchas, API docs this server was built against |
| [autocad-llm-pipeline](../autocad-llm-pipeline) | LLM routing layer that calls into this MCP server |
| [autocad-assistant-desktop](../autocad-assistant-desktop) | Desktop UI shell — human approval gate before drawing modifications |
| [SubstationTools](../SubstationTools) | Domain plugin loaded in AutoCAD alongside this MCP server |

### Examples

- `examples/easy-mcp-autocad/` — Python COM reference implementation using win32com. Shows how to control AutoCAD via COM automation (alternative to ObjectARX .NET). Includes shape creation, layer management, entity queries, and SQLite metadata storage.
