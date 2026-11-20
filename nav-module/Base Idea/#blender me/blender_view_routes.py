"""Esraa - show the A* routes on the real Jezero CTX DEM in a NEW Blender scene.
Run: Blender > File > New > General > Scripting tab > Open this file > Run Script.
It refuses to run inside an already-saved .blend, so it can never touch the team's scene."""
import os, json, math
import bpy
import numpy as np
from mathutils import Vector

PRESET = 0          # 0, 1 or 2 -> which preset from routes.json
EXAG = 1.0          # vertical exaggeration (try 2.0 if the terrain looks flat)
HOME = os.path.expanduser("~")
GRID = os.path.join(HOME, "Desktop", "NASASPACEAPP", "data", "jez_ctx_20m_grid.json")
ROUTES = os.path.join(HOME, "Desktop", "NASASPACEAPP", "nav-module", "routes.json")
OUT = os.path.join(HOME, "Desktop", "nav_view_esraa.blend")

if bpy.data.filepath:
    raise RuntimeError("An existing .blend is open (%s). Use File > New > General first." % bpy.data.filepath)

g = json.load(open(GRID))
z = np.array(g["elevations_m"], dtype=float)
R, C = z.shape
h = float(g["mesh_sample_spacing_m"])
routes = json.load(open(ROUTES))["presets"][PRESET]

for o in list(bpy.data.objects):                      # clear the default cube/light/camera of the NEW file
    bpy.data.objects.remove(o, do_unlink=True)

def material(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name); m.diffuse_color = (*rgb, 1)
    try:
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (*rgb, 1)
        if emit:
            b.inputs["Emission Color"].default_value = (*rgb, 1); b.inputs["Emission Strength"].default_value = emit
    except Exception as e:
        print("material nodes skipped:", e)
    return m

# ---- terrain: x = col*80 (east), y = (R-1-row)*80 (north), z = elevation (same frame as routes.json)
cc, rr = np.meshgrid(np.arange(C), np.arange(R))
verts = np.column_stack([(cc * h).ravel(), ((R - 1 - rr) * h).ravel(), (z * EXAG).ravel()])
i = (rr[:-1, :-1] * C + cc[:-1, :-1]).ravel()
faces = np.column_stack([i, i + C, i + C + 1, i + 1])
mesh = bpy.data.meshes.new("JezeroDEM"); mesh.from_pydata(verts.tolist(), [], faces.tolist()); mesh.update()
terrain = bpy.data.objects.new("Jezero_CTX_DEM_80m", mesh); bpy.context.scene.collection.objects.link(terrain)
terrain.data.materials.append(material("MarsRock", (0.55, 0.25, 0.12)))
for p in mesh.polygons: p.use_smooth = True

# ---- routes
def lift(col, row): return Vector((col * h, (R - 1 - row) * h, z[row][col] * EXAG + 15))
def draw(mode, rgb):
    pts = [lift(c, r) for c, r in routes[mode]["path"]]
    cu = bpy.data.curves.new(mode, "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = 25; cu.bevel_resolution = 2
    sp = cu.splines.new("POLY"); sp.points.add(len(pts) - 1)
    for p, v in zip(sp.points, pts): p.co = (v.x, v.y, v.z, 1)
    ob = bpy.data.objects.new("Route_" + mode, cu); ob.data.materials.append(material("M_" + mode, rgb, 3.0))
    bpy.context.scene.collection.objects.link(ob)
    for w in routes[mode]["waypoints"]:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=70, location=lift(w["col"], w["row"]))
        bpy.context.object.data.materials.append(material("M_wp_" + mode, rgb, 3.0))
    return pts
a = draw("shortest", (0.2, 0.6, 1.0)); b = draw("safest", (1.0, 1.0, 1.0))

# ---- camera + sun framed on the routes
allp = a + b; lo = Vector((min(p.x for p in allp), min(p.y for p in allp), min(p.z for p in allp)))
hi = Vector((max(p.x for p in allp), max(p.y for p in allp), max(p.z for p in allp)))
ctr = (lo + hi) / 2; ext = max(hi.x - lo.x, hi.y - lo.y, 500)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); cam.data.clip_end = 200000; cam.data.lens = 35
cam.location = Vector((ctr.x, ctr.y - 1.3 * ext, hi.z + 1.0 * ext))
cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
bpy.context.scene.collection.objects.link(cam); bpy.context.scene.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); sun.data.energy = 4
sun.rotation_euler = (math.radians(50), 0, math.radians(35)); bpy.context.scene.collection.objects.link(sun)
for area in bpy.context.screen.areas:
    if area.type == "VIEW_3D":
        area.spaces[0].clip_end = 200000

bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("Done. Saved", OUT, "| shortest pts", len(a), "| safest pts", len(b))
