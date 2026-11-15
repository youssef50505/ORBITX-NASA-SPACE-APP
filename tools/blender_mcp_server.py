"""Blender MCP Server for Antigravity & AI Assistants.

Bridges Antigravity to the running Blender instance via local TCP socket.
Communicates using the official Blender Lab MCP socket protocol.
Exposes read-only inspection tools for Scene, Objects, Meshes, Cameras, and Collections.
"""
from __future__ import annotations

import json
import socket
from typing import Any, Dict, List, Optional
try:
    from mcp.server.mcpserver import MCPServer
    mcp = MCPServer("Blender-MCP-Bridge")
except Exception:
    from mcp.server.fastmcp import FastMCP
    mcp = FastMCP("Blender-MCP-Bridge")

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 9876


def _query_blender(code: str) -> Dict[str, Any]:
    """Execute code in Blender via TCP socket and return the parsed result."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(120.0)
    try:
        sock.connect((DEFAULT_HOST, DEFAULT_PORT))
    except Exception as e:
        return {"status": "error", "message": f"Could not connect to Blender on {DEFAULT_HOST}:{DEFAULT_PORT}. Is Blender running with the MCP add-on enabled? Error: {e}"}

    payload = {
        "type": "execute",
        "code": code,
        "strict_json": True,
    }
    msg = json.dumps(payload).encode("utf-8") + b"\0"
    sock.sendall(msg)

    buf = bytearray()
    while b"\0" not in buf:
        chunk = sock.recv(65536)
        if not chunk:
            break
        buf.extend(chunk)
    sock.close()

    try:
        data = json.loads(buf.rstrip(b"\0").decode("utf-8"))
        return data
    except Exception as e:
        return {"status": "error", "message": f"Failed to parse response: {e}, raw: {buf[:200]!r}"}


@mcp.tool()
def get_scene_info() -> Dict[str, Any]:
    """Get general information about the active Blender scene (name, current frame, active camera, render engine)."""
    code = """
import bpy
s = bpy.context.scene
result = {
    'scene_name': s.name,
    'current_frame': s.frame_current,
    'frame_start': s.frame_start,
    'frame_end': s.frame_end,
    'fps': s.render.fps,
    'active_camera': s.camera.name if s.camera else None,
    'render_engine': s.render.engine,
    'unit_system': s.unit_settings.system,
    'unit_scale': s.unit_settings.scale_length
}
"""
    return _query_blender(code)


@mcp.tool()
def get_objects(object_type: Optional[str] = None, limit: int = 50) -> Dict[str, Any]:
    """List objects in the active scene, optionally filtered by type (e.g. MESH, CAMERA, LIGHT, EMPTY, ARMATURE, CURVE)."""
    code = f"""
import bpy
s = bpy.context.scene
target_type = {repr(object_type)}
counts = {{}}
matched = []
for o in s.objects:
    counts[o.type] = counts.get(o.type, 0) + 1
    if target_type is None or o.type == target_type:
        matched.append({{
            'name': o.name,
            'type': o.type,
            'location': [round(v, 3) for v in o.location],
            'hide_viewport': o.hide_viewport,
            'hide_render': o.hide_render
        }})
result = {{
    'total_objects': len(s.objects),
    'counts_by_type': counts,
    'returned_count': min(len(matched), {limit}),
    'objects': matched[:{limit}]
}}
"""
    return _query_blender(code)


@mcp.tool()
def get_collections() -> Dict[str, Any]:
    """Retrieve the hierarchy of Collections in the active scene and object counts for each."""
    code = """
import bpy
s = bpy.context.scene
colls = []
for c in s.collection.children:
    colls.append({
        'name': c.name,
        'objects_count': len(c.objects),
        'children': [child.name for child in c.children]
    })
result = {
    'root_collections_count': len(s.collection.children),
    'collections': colls
}
"""
    return _query_blender(code)


@mcp.tool()
def get_mesh_details(mesh_name: str) -> Dict[str, Any]:
    """Retrieve detailed mesh geometry metrics (vertex count, polygon count, dimensions, location, materials)."""
    code = f'''
import bpy
o = bpy.data.objects.get({repr(mesh_name)})
if not o:
    result = {{"error": "Object not found"}}
else:
    info = {{
        "name": o.name,
        "type": o.type,
        "location": [round(v, 4) for v in o.location],
        "dimensions": [round(v, 4) for v in o.dimensions],
        "scale": [round(v, 4) for v in o.scale],
        "modifiers": [m.name + " (" + m.type + ")" for m in o.modifiers]
    }}
    if o.type == "MESH" and o.data:
        info["vertices_count"] = len(o.data.vertices)
        info["polygons_count"] = len(o.data.polygons)
        info["materials"] = [m.name for m in o.data.materials if m]
    result = info
'''
    return _query_blender(code)


@mcp.tool()
def get_cameras() -> Dict[str, Any]:
    """List all cameras in the Blender file with their focal lengths, clipping ranges, and positions."""
    code = """
import bpy
cams = []
for o in bpy.data.objects:
    if o.type == 'CAMERA':
        cams.append({
            'name': o.name,
            'focal_length_mm': round(o.data.lens, 2),
            'clip_start': round(o.data.clip_start, 4),
            'clip_end': round(o.data.clip_end, 2),
            'location': [round(v, 3) for v in o.location],
            'is_active': (bpy.context.scene.camera == o)
        })
result = {'cameras': cams, 'total_cameras': len(cams)}
"""
    return _query_blender(code)


@mcp.tool()
def get_materials() -> Dict[str, Any]:
    """List materials present in the Blender file."""
    code = """
import bpy
mats = [m.name for m in bpy.data.materials]
result = {'total_materials': len(mats), 'materials': mats}
"""
    return _query_blender(code)


@mcp.tool()
def execute_read_only_query(python_code: str) -> Dict[str, Any]:
    """Execute a custom read-only python query in Blender. Code must set `result = {...}`."""
    return _query_blender(python_code)


if __name__ == "__main__":
    mcp.run()
