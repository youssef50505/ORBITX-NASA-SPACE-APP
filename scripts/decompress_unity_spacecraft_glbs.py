import os
import struct
import json
import bpy

ROOT = r"c:\Users\yhsn5\Downloads\NASASPACEAPP"
SRC_DIR = os.path.join(ROOT, "data", "missions_3d", "nasa_official_extra")
DST_DIR = os.path.join(ROOT, "unity_project", "Assets", "Models", "Spacecraft")

FILES = [
    "Ares 1 (A).glb",
    "Astronaut.glb",
    "Habitat Demonstration Unit (part 1).glb",
    "Habitat Demonstration Unit (part 2).glb",
    "Mark III Spacesuit.glb",
    "Mobile Launcher (assembled).glb",
]

for fname in FILES:
    src = os.path.join(SRC_DIR, fname)
    dst = os.path.join(DST_DIR, fname)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    # Clean stray unrotated duplicate sub-parts sitting at Y < -2 in Ares 1 (A).glb
    if fname == "Ares 1 (A).glb":
        to_remove = []
        for obj in list(bpy.context.scene.objects):
            if obj.type == "MESH" and obj.data.vertices:
                ys = [(obj.matrix_world @ v.co).y for v in obj.data.vertices]
                if max(ys) < -2.0:
                    to_remove.append(obj)
        for obj in to_remove:
            bpy.data.objects.remove(obj, do_unlink=True)
        # Bake all parent/object rotations & scales into mesh vertices so Blender Z-up cleanly exports to glTF Y-up with zero stray node rotations
    for obj in list(bpy.context.scene.objects):
        if obj.type == "MESH":
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # Remove unused parent empties so glTF node hierarchy has zero legacy rotations
    for obj in list(bpy.context.scene.objects):
        if obj.type == "EMPTY":
            bpy.data.objects.remove(obj, do_unlink=True)

    # Relax T-pose arms & normalize Astronaut.glb to real 1.80m standing height (feet at Z=0 in Blender -> Y=0 in Unity)
    if fname == "Astronaut.glb":
        import math, mathutils
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH":
                # Smooth shoulder bend (~42 deg down) starting smoothly past |x| > 11.2 (raw height 74.26 units)
                pivot_x = 12.2
                pivot_z = 16.5
                ang = math.radians(42.0)
                for v in obj.data.vertices:
                    ax = abs(v.co.x)
                    if ax > 10.0 and v.co.z > 4.0 and v.co.z < 27.0:
                        t = min(1.0, max(0.0, (ax - 10.0) / 4.5))
                        w = t * t * (3.0 - 2.0 * t)
                        side = 1.0 if v.co.x > 0 else -1.0
                        px = side * pivot_x
                        dx = v.co.x - px
                        dz = v.co.z - pivot_z
                        theta = -side * ang * w
                        ca, sa = math.cos(theta), math.sin(theta)
                        v.co.x = px + dx * ca - dz * sa
                        v.co.z = pivot_z + dx * sa + dz * ca
                obj.data.update()
        # Normalize height to 1.80m and ground lowest vertex at Z = 0, facing +Y (Unity +Z)
        all_pts = [o.matrix_world @ v.co for o in bpy.context.scene.objects if o.type == "MESH" for v in o.data.vertices]
        min_z = min(p.z for p in all_pts)
        max_z = max(p.z for p in all_pts)
        cx = 0.5 * (min(p.x for p in all_pts) + max(p.x for p in all_pts))
        cy = 0.5 * (min(p.y for p in all_pts) + max(p.y for p in all_pts))
        scl = 1.80 / max(0.001, max_z - min_z)
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH":
                for v in obj.data.vertices:
                    v.co.x = (v.co.x - cx) * scl
                    v.co.y = (v.co.y - cy) * scl
                    v.co.z = (v.co.z - min_z) * scl
                obj.data.update()

    # Clean Blender material Principled nodes before glTF export so opaque hardware never turns pitch-black or glassy in URP
    for mat in bpy.data.materials:
        if not mat.use_nodes or not mat.node_tree:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                if "Transmission Weight" in node.inputs:
                    node.inputs["Transmission Weight"].default_value = 0.0
                elif "Transmission" in node.inputs:
                    node.inputs["Transmission"].default_value = 0.0
                if "Metallic" in node.inputs:
                    node.inputs["Metallic"].default_value = min(node.inputs["Metallic"].default_value, 0.28)
                if "Roughness" in node.inputs:
                    node.inputs["Roughness"].default_value = max(node.inputs["Roughness"].default_value, 0.45)

    # Compute Blender Z-up world mesh bounds before glTF Y-up export
    mins = [1e9, 1e9, 1e9]
    maxs = [-1e9, -1e9, -1e9]
    verts = 0
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            for corner in obj.bound_box:
                wc = obj.matrix_world @ __import__("mathutils").Vector(corner)
                for i in range(3):
                    mins[i] = min(mins[i], wc[i])
                    maxs[i] = max(maxs[i], wc[i])
            verts += len(obj.data.vertices)
    bpy.ops.export_scene.gltf(
        filepath=dst,
        export_format="GLB",
        export_yup=True,
        export_draco_mesh_compression_enable=False,
        export_apply=True,
    )

    # Patch exported GLB JSON chunk deterministically to strip bogus KHR_materials_transmission/ior and fix omitted metallicFactor=1 defaults
    with open(dst, "rb") as f:
        glb = f.read()
    magic, version, total_len = struct.unpack("<4sII", glb[:12])
    jlen, jtype = struct.unpack("<II", glb[12:20])
    jdata = json.loads(glb[20:20 + jlen].decode("utf-8"))
    blen, btype = struct.unpack("<II", glb[20 + jlen:28 + jlen])
    bin_chunk = glb[28 + jlen:28 + jlen + blen]

    changed = False
    for m_idx, mat in enumerate(jdata.get("materials", [])):
        mname = (mat.get("name") or "").lower()
        exts = mat.get("extensions") or {}
        for bad_ext in ("KHR_materials_transmission", "KHR_materials_ior", "KHR_materials_specular"):
            if bad_ext in exts:
                del exts[bad_ext]
                changed = True
        if not exts and "extensions" in mat:
            del mat["extensions"]
        pbr = mat.setdefault("pbrMetallicRoughness", {})
        # glTF 2.0 spec defaults omitted metallicFactor to 1.0 (pure mirror black without reflection probe!)
        if "metallicFactor" not in pbr or pbr["metallicFactor"] > 0.45:
            pbr["metallicFactor"] = 0.12 if ("hdu" in fname.lower() or "astronaut" in fname.lower() or "mark" in fname.lower() or "launcher" in fname.lower()) else 0.15
            changed = True
        if "roughnessFactor" not in pbr or pbr["roughnessFactor"] < 0.35:
            pbr["roughnessFactor"] = 0.56
            changed = True
        if fname in ("Habitat Demonstration Unit (part 1).glb", "Habitat Demonstration Unit (part 2).glb"):
            pbr["baseColorFactor"] = [1.0, 1.0, 1.0, 1.0]
            pbr["metallicFactor"] = 0.08
            pbr["roughnessFactor"] = 0.55
            changed = True
        elif fname == "Ares 1 (A).glb":
            # Crisp NASA Ares I thermal white upper stage / SRB with dark interstage/skirt & silver nozzle
            if m_idx in (0, 2, 4, 6):
                pbr["baseColorFactor"] = [0.92, 0.93, 0.95, 1.0]
                pbr["metallicFactor"] = 0.10
                pbr["roughnessFactor"] = 0.48
            elif m_idx in (1, 5):
                pbr["baseColorFactor"] = [0.14, 0.15, 0.18, 1.0]
                pbr["metallicFactor"] = 0.35
                pbr["roughnessFactor"] = 0.50
            else:
                pbr["baseColorFactor"] = [0.76, 0.46, 0.24, 1.0] if m_idx == 3 else [0.58, 0.62, 0.68, 1.0]
                pbr["metallicFactor"] = 0.45
                pbr["roughnessFactor"] = 0.42
            changed = True
        elif fname == "Mark III Spacesuit.glb":
            bc = pbr.get("baseColorFactor")
            if bc and max(bc[:3]) < 0.25 and "blinn1" not in mname:
                pbr["baseColorFactor"] = [0.82, 0.84, 0.88, 1.0]
            elif "lambert5" in mname:
                pbr["baseColorFactor"] = [0.95, 0.95, 0.95, 1.0]
            changed = True
    for ext_list_key in ("extensionsUsed", "extensionsRequired"):
        if ext_list_key in jdata:
            filtered = [e for e in jdata[ext_list_key] if e not in ("KHR_materials_transmission", "KHR_materials_ior", "KHR_materials_specular")]
            if filtered:
                jdata[ext_list_key] = filtered
            else:
                del jdata[ext_list_key]
            changed = True

    if changed:
        new_jbytes = json.dumps(jdata, separators=(",", ":")).encode("utf-8")
        pad_j = (4 - (len(new_jbytes) % 4)) % 4
        new_jbytes += b" " * pad_j
        pad_b = (4 - (len(bin_chunk) % 4)) % 4
        bin_chunk_padded = bin_chunk + (b"\x00" * pad_b)
        new_total = 12 + 8 + len(new_jbytes) + 8 + len(bin_chunk_padded)
        with open(dst, "wb") as f:
            f.write(struct.pack("<4sII", b"glTF", 2, new_total))
            f.write(struct.pack("<II", len(new_jbytes), 0x4E4F534A))
            f.write(new_jbytes)
            f.write(struct.pack("<II", len(bin_chunk_padded), 0x004E4942))
            f.write(bin_chunk_padded)

    ext_req = jdata.get("extensionsRequired", [])
    dx, dy, dz = maxs[0] - mins[0], maxs[1] - mins[1], maxs[2] - mins[2]
    print(f"CONVERTED {fname}: size={os.path.getsize(dst):,} B verts={verts} blenderSize(X,Y,Z)=({dx:.2f},{dy:.2f},{dz:.2f}) extReq={ext_req}")
