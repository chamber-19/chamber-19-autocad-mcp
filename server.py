"""
server.py
---------
MCP server entry point for chamber-19-autocad-mcp.

Run this file to start the AutoCAD MCP server:

    python server.py

The server communicates over stdio and exposes AutoCAD drawing
operations as MCP tools that AI assistants (e.g. Claude) can call.
"""

from __future__ import annotations

import os
import logging

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")
logger = logging.getLogger("autocad-mcp")

from mcp.server.fastmcp import FastMCP  # type: ignore
from autocad.client import AutoCADClient
import autocad.tools as _tools

# ---------------------------------------------------------------------------
# Server initialisation
# ---------------------------------------------------------------------------

mcp = FastMCP(name=os.environ.get("MCP_SERVER_NAME", "autocad-mcp"))
client = AutoCADClient()

# Open a default DXF file if configured
_default_dxf = os.environ.get("AUTOCAD_DEFAULT_DXF")
if _default_dxf:
    try:
        client.open_file(_default_dxf)
        logger.info("Opened default DXF: %s", _default_dxf)
    except Exception as exc:
        logger.warning("Could not open default DXF '%s': %s", _default_dxf, exc)

# ---------------------------------------------------------------------------
# File-operation tools
# ---------------------------------------------------------------------------

@mcp.tool()
def autocad_new_drawing() -> dict:
    """Create a new empty AutoCAD drawing."""
    return _tools.new_drawing(client)


@mcp.tool()
def autocad_open_file(path: str) -> dict:
    """Open an existing DWG or DXF file.

    Args:
        path: Absolute path to the DWG or DXF file to open.
    """
    return _tools.open_file(client, path)


@mcp.tool()
def autocad_save_file(path: str = "") -> dict:
    """Save the current drawing.

    Args:
        path: Optional file path.  If empty, saves to the current file.
    """
    return _tools.save_file(client, path or None)


# ---------------------------------------------------------------------------
# Entity inspection tools
# ---------------------------------------------------------------------------

@mcp.tool()
def autocad_list_entities(layer: str = "", entity_type: str = "") -> dict:
    """List entities in the current drawing.

    Args:
        layer: Optional layer name filter.
        entity_type: Optional entity type filter (e.g. LINE, CIRCLE).
    """
    return _tools.list_entities(client, layer or None, entity_type or None)


@mcp.tool()
def autocad_get_entity_properties(handle: str) -> dict:
    """Return all properties of an entity.

    Args:
        handle: The entity handle (as returned by list or draw tools).
    """
    return _tools.get_entity_properties(client, handle)


# ---------------------------------------------------------------------------
# Drawing primitives
# ---------------------------------------------------------------------------

@mcp.tool()
def autocad_draw_line(x1: float, y1: float, x2: float, y2: float, layer: str = "0") -> dict:
    """Draw a line between two points.

    Args:
        x1: Start X coordinate.
        y1: Start Y coordinate.
        x2: End X coordinate.
        y2: End Y coordinate.
        layer: Layer name (default "0").
    """
    return _tools.draw_line(client, x1, y1, x2, y2, layer)


@mcp.tool()
def autocad_draw_circle(cx: float, cy: float, radius: float, layer: str = "0") -> dict:
    """Draw a circle.

    Args:
        cx: Centre X coordinate.
        cy: Centre Y coordinate.
        radius: Circle radius.
        layer: Layer name (default "0").
    """
    return _tools.draw_circle(client, cx, cy, radius, layer)


@mcp.tool()
def autocad_draw_arc(
    cx: float,
    cy: float,
    radius: float,
    start_angle: float,
    end_angle: float,
    layer: str = "0",
) -> dict:
    """Draw an arc.

    Args:
        cx: Centre X coordinate.
        cy: Centre Y coordinate.
        radius: Arc radius.
        start_angle: Start angle in degrees.
        end_angle: End angle in degrees.
        layer: Layer name (default "0").
    """
    return _tools.draw_arc(client, cx, cy, radius, start_angle, end_angle, layer)


@mcp.tool()
def autocad_draw_polyline(
    points: list[list[float]],
    closed: bool = False,
    layer: str = "0",
) -> dict:
    """Draw a polyline through a list of points.

    Args:
        points: List of [x, y] coordinate pairs, e.g. [[0,0],[10,0],[10,10]].
        closed: Whether to close the polyline (default False).
        layer: Layer name (default "0").
    """
    return _tools.draw_polyline(client, points, closed, layer)


@mcp.tool()
def autocad_draw_text(
    x: float,
    y: float,
    text: str,
    height: float = 2.5,
    layer: str = "0",
) -> dict:
    """Place a single-line text annotation.

    Args:
        x: Insertion X coordinate.
        y: Insertion Y coordinate.
        text: Text content.
        height: Text height (default 2.5).
        layer: Layer name (default "0").
    """
    return _tools.draw_text(client, x, y, text, height, layer)


@mcp.tool()
def autocad_draw_mtext(
    x: float,
    y: float,
    text: str,
    width: float = 100.0,
    height: float = 2.5,
    layer: str = "0",
) -> dict:
    """Place a multi-line text (MTEXT) annotation.

    Args:
        x: Insertion X coordinate.
        y: Insertion Y coordinate.
        text: Text content (supports MText formatting codes).
        width: Text boundary width (default 100).
        height: Character height (default 2.5).
        layer: Layer name (default "0").
    """
    return _tools.draw_mtext(client, x, y, text, width, height, layer)


# ---------------------------------------------------------------------------
# Entity modification / deletion
# ---------------------------------------------------------------------------

@mcp.tool()
def autocad_move_entity(handle: str, dx: float, dy: float) -> dict:
    """Move an entity by a displacement vector.

    Args:
        handle: Entity handle.
        dx: Displacement in X.
        dy: Displacement in Y.
    """
    return _tools.move_entity(client, handle, dx, dy)


@mcp.tool()
def autocad_delete_entity(handle: str) -> dict:
    """Delete an entity from the drawing.

    Args:
        handle: Entity handle.
    """
    return _tools.delete_entity(client, handle)


# ---------------------------------------------------------------------------
# Layer management
# ---------------------------------------------------------------------------

@mcp.tool()
def autocad_list_layers() -> dict:
    """List all layers in the current drawing with their properties."""
    return _tools.list_layers(client)


@mcp.tool()
def autocad_create_layer(name: str, color: int = 7) -> dict:
    """Create a new layer.

    Args:
        name: Layer name.
        color: AutoCAD colour index (1–255, default 7 = white/black).
    """
    return _tools.create_layer(client, name, color)


@mcp.tool()
def autocad_set_layer(name: str) -> dict:
    """Set the current active layer.

    Args:
        name: Name of the layer to activate.
    """
    return _tools.set_layer(client, name)


# ---------------------------------------------------------------------------
# Server entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    mcp.run()
