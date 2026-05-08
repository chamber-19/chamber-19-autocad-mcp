"""
autocad/tools.py
----------------
MCP tool implementations that delegate to AutoCADClient.
Each public function in this module is registered as an MCP tool
by server.py.
"""

from __future__ import annotations

import logging
from typing import Any

logger = logging.getLogger(__name__)

# COM VARIANT type constant: VT_ARRAY | VT_R8 (array of 64-bit floats)
_VT_ARRAY_R8 = 8192 | 0x2000


# ---------------------------------------------------------------------------
# File operations
# ---------------------------------------------------------------------------

def new_drawing(client) -> dict[str, Any]:
    """Create a new empty drawing."""
    client.new_drawing()
    return {"status": "ok", "message": "New drawing created."}


def open_file(client, path: str) -> dict[str, Any]:
    """Open an existing DWG or DXF file at *path*."""
    client.open_file(path)
    return {"status": "ok", "message": f"Opened: {path}"}


def save_file(client, path: str | None = None) -> dict[str, Any]:
    """Save the current drawing, optionally to *path*."""
    client.save_file(path)
    msg = f"Saved to: {path}" if path else "Drawing saved."
    return {"status": "ok", "message": msg}


# ---------------------------------------------------------------------------
# Entity inspection
# ---------------------------------------------------------------------------

def list_entities(client, layer: str | None = None, entity_type: str | None = None) -> dict[str, Any]:
    """
    Return a list of entities in the current drawing.

    Parameters
    ----------
    layer:
        Optional layer name filter.
    entity_type:
        Optional entity type filter (e.g. ``"LINE"``, ``"CIRCLE"``).
    """
    msp = client.get_modelspace()
    entities = []

    if client.is_com:
        for obj in msp:
            try:
                etype = obj.ObjectName
                elayer = obj.Layer
                if layer and elayer.upper() != layer.upper():
                    continue
                if entity_type and entity_type.upper() not in etype.upper():
                    continue
                entities.append({"handle": obj.Handle, "type": etype, "layer": elayer})
            except Exception:
                pass
    else:
        query = entity_type.upper() if entity_type else "*"
        for e in msp.query(query):
            elayer = e.dxf.get("layer", "0")
            if layer and elayer.upper() != layer.upper():
                continue
            entities.append({"handle": e.dxf.handle, "type": e.dxftype(), "layer": elayer})

    return {"entities": entities, "count": len(entities)}


def get_entity_properties(client, handle: str) -> dict[str, Any]:
    """Return the full property set of the entity identified by *handle*."""
    msp = client.get_modelspace()
    props: dict[str, Any] = {}

    if client.is_com:
        for obj in msp:
            try:
                if obj.Handle == handle:
                    for attr in dir(obj):
                        if not attr.startswith("_"):
                            try:
                                props[attr] = str(getattr(obj, attr))
                            except Exception:
                                pass
                    break
            except Exception:
                pass
    else:
        for e in msp:
            if e.dxf.handle == handle:
                props = {k: str(v) for k, v in e.dxf.all_existing_dxf_attribs().items()}
                break

    if not props:
        return {"error": f"Entity with handle '{handle}' not found."}
    return {"handle": handle, "properties": props}


# ---------------------------------------------------------------------------
# Drawing primitives
# ---------------------------------------------------------------------------

def draw_line(client, x1: float, y1: float, x2: float, y2: float, layer: str = "0") -> dict[str, Any]:
    """Draw a line from (x1, y1) to (x2, y2) on *layer*."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        p1 = win32.VARIANT(_VT_ARRAY_R8, [x1, y1, 0.0])
        p2 = win32.VARIANT(_VT_ARRAY_R8, [x2, y2, 0.0])
        line = msp.AddLine(p1, p2)
        line.Layer = layer
        handle = line.Handle
    else:
        e = msp.add_line((x1, y1, 0), (x2, y2, 0), dxfattribs={"layer": layer})
        handle = e.dxf.handle

    return {"status": "ok", "handle": handle, "type": "LINE"}


def draw_circle(client, cx: float, cy: float, radius: float, layer: str = "0") -> dict[str, Any]:
    """Draw a circle centered at (cx, cy) with *radius* on *layer*."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        center = win32.VARIANT(_VT_ARRAY_R8, [cx, cy, 0.0])
        circle = msp.AddCircle(center, radius)
        circle.Layer = layer
        handle = circle.Handle
    else:
        e = msp.add_circle((cx, cy, 0), radius, dxfattribs={"layer": layer})
        handle = e.dxf.handle

    return {"status": "ok", "handle": handle, "type": "CIRCLE"}


def draw_arc(
    client,
    cx: float, cy: float,
    radius: float,
    start_angle: float,
    end_angle: float,
    layer: str = "0",
) -> dict[str, Any]:
    """Draw an arc centered at (cx, cy)."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        import math
        center = win32.VARIANT(_VT_ARRAY_R8, [cx, cy, 0.0])
        arc = msp.AddArc(center, radius, math.radians(start_angle), math.radians(end_angle))
        arc.Layer = layer
        handle = arc.Handle
    else:
        e = msp.add_arc(
            (cx, cy, 0), radius, start_angle, end_angle,
            dxfattribs={"layer": layer},
        )
        handle = e.dxf.handle

    return {"status": "ok", "handle": handle, "type": "ARC"}


def draw_polyline(client, points: list[list[float]], closed: bool = False, layer: str = "0") -> dict[str, Any]:
    """Draw a polyline through *points* (list of [x, y] pairs)."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        flat = [coord for pt in points for coord in (pt[0], pt[1], 0.0)]
        pts_variant = win32.VARIANT(_VT_ARRAY_R8, flat)
        pl = msp.AddPolyline(pts_variant)
        pl.Layer = layer
        if closed:
            pl.Closed = True
        handle = pl.Handle
    else:
        e = msp.add_lwpolyline(
            [(p[0], p[1]) for p in points],
            dxfattribs={"layer": layer, "closed": closed},
        )
        handle = e.dxf.handle

    return {"status": "ok", "handle": handle, "type": "POLYLINE"}


def draw_text(client, x: float, y: float, text: str, height: float = 2.5, layer: str = "0") -> dict[str, Any]:
    """Place a single-line text annotation at (x, y)."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        pt = win32.VARIANT(_VT_ARRAY_R8, [x, y, 0.0])
        t = msp.AddText(text, pt, height)
        t.Layer = layer
        handle = t.Handle
    else:
        e = msp.add_text(text, dxfattribs={"insert": (x, y, 0), "height": height, "layer": layer})
        handle = e.dxf.handle

    return {"status": "ok", "handle": handle, "type": "TEXT"}


def draw_mtext(client, x: float, y: float, text: str, width: float = 100.0, height: float = 2.5, layer: str = "0") -> dict[str, Any]:
    """Place a multi-line text (MTEXT) annotation at (x, y)."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        pt = win32.VARIANT(_VT_ARRAY_R8, [x, y, 0.0])
        mt = msp.AddMText(pt, width, text)
        mt.Layer = layer
        handle = mt.Handle
    else:
        e = msp.add_mtext(
            text,
            dxfattribs={"insert": (x, y, 0), "char_height": height, "width": width, "layer": layer},
        )
        handle = e.dxf.handle

    return {"status": "ok", "handle": handle, "type": "MTEXT"}


# ---------------------------------------------------------------------------
# Entity modification / deletion
# ---------------------------------------------------------------------------

def move_entity(client, handle: str, dx: float, dy: float) -> dict[str, Any]:
    """Move the entity identified by *handle* by (dx, dy)."""
    msp = client.get_modelspace()
    if client.is_com:
        import win32com.client as win32  # type: ignore
        for obj in msp:
            try:
                if obj.Handle == handle:
                    p1 = win32.VARIANT(_VT_ARRAY_R8, [0.0, 0.0, 0.0])
                    p2 = win32.VARIANT(_VT_ARRAY_R8, [dx, dy, 0.0])
                    obj.Move(p1, p2)
                    return {"status": "ok", "handle": handle}
            except Exception:
                pass
        return {"error": f"Entity '{handle}' not found."}
    else:
        for e in msp:
            if e.dxf.handle == handle:
                e.translate((dx, dy, 0))
                return {"status": "ok", "handle": handle}
        return {"error": f"Entity '{handle}' not found."}


def delete_entity(client, handle: str) -> dict[str, Any]:
    """Delete the entity identified by *handle*."""
    msp = client.get_modelspace()
    if client.is_com:
        for obj in msp:
            try:
                if obj.Handle == handle:
                    obj.Delete()
                    return {"status": "ok", "handle": handle}
            except Exception:
                pass
        return {"error": f"Entity '{handle}' not found."}
    else:
        for e in msp:
            if e.dxf.handle == handle:
                msp.delete_entity(e)
                return {"status": "ok", "handle": handle}
        return {"error": f"Entity '{handle}' not found."}


# ---------------------------------------------------------------------------
# Layer management
# ---------------------------------------------------------------------------

def list_layers(client) -> dict[str, Any]:
    """List all layers in the current drawing."""
    doc = client.get_doc()
    layers = []
    if client.is_com:
        for layer in doc.Layers:
            layers.append({
                "name": layer.Name,
                "color": layer.Color,
                "on": layer.LayerOn,
                "frozen": layer.Freeze,
            })
    else:
        for layer in doc.layers:
            layers.append({
                "name": layer.dxf.name,
                "color": layer.dxf.get("color", 7),
                "on": not layer.is_off(),
                "frozen": layer.is_frozen(),
            })
    return {"layers": layers, "count": len(layers)}


def create_layer(client, name: str, color: int = 7) -> dict[str, Any]:
    """Create a new layer with the given *name* and *color* index."""
    doc = client.get_doc()
    if client.is_com:
        layer = doc.Layers.Add(name)
        layer.Color = color
    else:
        doc.layers.add(name, dxfattribs={"color": color})
    return {"status": "ok", "name": name, "color": color}


def set_layer(client, name: str) -> dict[str, Any]:
    """Set the current active layer to *name*."""
    if client.is_com:
        doc = client.get_doc()
        doc.ActiveLayer = doc.Layers.Item(name)
    else:
        doc = client.get_doc()
        if doc.layers.get(name) is None:
            return {"error": f"Layer '{name}' does not exist."}
        doc.header["$CLAYER"] = name
    return {"status": "ok", "active_layer": name}
