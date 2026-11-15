import bpy
import os
import sys
import array
import hashlib
import json

def inspect_blender():
    report = {}
    
    # 1. HiRISE Terrain Invariant
    site = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
    if not site or not site.data:
        report["hirise"] = {"exists": False}
    else:
        v_count = len(site.data.vertices)
        p_count = len(site.data.polygons)
        coords = array.array('f', [0.0]) * (v_count * 3)
        site.data.vertices.foreach_get('co', coords)
        h = hashlib.sha256(coords.tobytes()).hexdigest()
        report["hirise"] = {
            "exists": True,
            "vertex_count": v_count,
            "polygon_count": p_count,
            "sha256": h,
            "expected_verts": 1132096,
            "expected_hash": "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd",
            "verts_match": (v_count == 1132096),
            "hash_match": (h == "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd")
        }

    # 2. Curiosity Rover Asset Hierarchy and Materials
    curiosity_root = bpy.data.objects.get("MS_Curiosity_Root")
    if not curiosity_root:
        report["curiosity"] = {"root_exists": False}
    else:
        def get_all_children(obj):
            res = [obj]
            for child in obj.children:
                res.extend(get_all_children(child))
            return res

        curiosity_objs = get_all_children(curiosity_root)
        mesh_objs = [o for o in curiosity_objs if o.type == 'MESH']
        
        curiosity_materials = set()
        tex_03_011_objects = []
        for o in mesh_objs:
            for slot in o.material_slots:
                if slot.material:
                    curiosity_materials.add(slot.material.name)
                    if slot.material.name == "tex_03.011":
                        tex_03_011_objects.append(o.name)
                        
        mat_tex_03_011 = bpy.data.materials.get("tex_03.011")
        tex_node_info = []
        if mat_tex_03_011 and mat_tex_03_011.node_tree:
            for node in mat_tex_03_011.node_tree.nodes:
                if node.type == 'TEX_IMAGE':
                    tex_node_info.append({
                        "node_name": node.name,
                        "image_name": node.image.name if node.image else None,
                        "is_packed": (node.image.packed_file is not None) if node.image else False,
                        "filepath": node.image.filepath if node.image else None,
                        "size": list(node.image.size) if node.image else None
                    })

        report["curiosity"] = {
            "root_exists": True,
            "total_objects": len(curiosity_objs),
            "mesh_objects_count": len(mesh_objs),
            "materials_count": len(curiosity_materials),
            "materials": sorted(list(curiosity_materials)),
            "tex_03_011_assigned_objects": sorted(tex_03_011_objects),
            "tex_03_011_nodes": tex_node_info
        }

    # 3. Overall Image Datablocks and Missing Images Check
    images_detail = []
    missing_images = []
    for img in bpy.data.images:
        if img.name in ('Render Result', 'Viewer Node'):
            continue
        is_packed = (img.packed_file is not None)
        abs_p = bpy.path.abspath(img.filepath)
        exists_on_disk = os.path.exists(abs_p) if not is_packed else True
        if not is_packed and not exists_on_disk:
            missing_images.append(img.name)
        images_detail.append({
            "name": img.name,
            "packed": is_packed,
            "filepath": img.filepath,
            "exists_on_disk": exists_on_disk,
            "size": list(img.size)
        })

    report["images"] = {
        "total_images": len(images_detail),
        "missing_count": len(missing_images),
        "missing_images": missing_images,
        "detail": images_detail
    }

    # 4. Check for obsolete missing image datablocks
    report["pruned_blocks"] = {
        "tex_03.png.006_exists": (bpy.data.images.get("tex_03.png.006") is not None),
        "tex_03.png.008_exists": (bpy.data.images.get("tex_03.png.008") is not None),
        "tex_03.png.007_exists": (bpy.data.images.get("tex_03.png.007") is not None)
    }

    # 5. External Libraries
    libs = [lib.filepath for lib in bpy.data.libraries]
    report["libraries"] = {
        "count": len(libs),
        "paths": libs
    }

    print("JSON_START")
    print(json.dumps(report, indent=2))
    print("JSON_END")

if __name__ == "__main__":
    inspect_blender()
