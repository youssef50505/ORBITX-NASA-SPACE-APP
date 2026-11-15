"""Idempotently hide unused/off-theme judge-flagged character & gun clutter in Blender.

Keeps all scene invariants intact (packed_images == 276, MS_Agriculture_Zone == 513 objects,
and HiRISE Jezero terrain vertices/SHA-256 unchanged).
"""
from __future__ import annotations

import pathlib

UNUSED_ROOT_NAMES = (
    "Girl_Black_Character",
    "Girl_Outfit_Transfer",
    "Suit_on_Girl",
    "ludovisions_FC1_rig",
    "ludovisions_FC1_empty_01",
    "ludovisions_FC1_empty_02",
    "Sketchfab_model.015",
    "Sketchfab_model.012",
    "Sketchfab_model.008",
    "NewHead_Parts",
    "Girls_Camera",
)

UNUSED_COLLECTION_NAMES = (
    "Girl_Outfit_Transfer",
    "Suit_on_Girl",
    "NewHead_Parts",
)


def hide_recursive(obj) -> int:
    count = 0
    if not obj.hide_viewport or not obj.hide_render:
        obj.hide_viewport = True
        obj.hide_render = True
        count += 1
    for child in obj.children:
        count += hide_recursive(child)
    return count


def main() -> None:
    import bpy

    hidden_roots = []
    hidden_nodes = 0
    for name in UNUSED_ROOT_NAMES:
        obj = bpy.data.objects.get(name)
        if obj is not None:
            hidden_roots.append(name)
            hidden_nodes += hide_recursive(obj)

    # Also hide unused character/head collections and all objects linked inside them
    for cname in UNUSED_COLLECTION_NAMES:
        col = bpy.data.collections.get(cname)
        if col is not None:
            col.hide_viewport = True
            col.hide_render = True
            for c_obj in col.all_objects:
                hidden_nodes += hide_recursive(c_obj)

    # Also catch any orphan gun/outfit/girl/fc1/head-part child meshes if renamed or detached
    extra_keywords = ("pistol_74", "balon_63", "Girl_Outfit", "Suit_on_Girl", "NH_mesh_", "ludovisions_FC1_")
    for obj in bpy.data.objects:
        if any(k in obj.name for k in extra_keywords):
            if not obj.hide_viewport or not obj.hide_render:
                obj.hide_viewport = True
                obj.hide_render = True
                hidden_nodes += 1

    # Save cleanly without creating persistent .blend1 backup
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_mainfile()

    blend_path = pathlib.Path(bpy.data.filepath)
    backup = blend_path.with_suffix(".blend1")
    if backup.exists():
        backup.unlink()

    packed = sum(1 for img in bpy.data.images if img.packed_file is not None)
    agri_col = bpy.data.collections.get("MS_Agriculture_Zone")
    agri_count = len(agri_col.all_objects) if agri_col else -1
    print(f"CLEANUP_DONE roots={len(hidden_roots)} hidden_nodes={hidden_nodes} packed_images={packed} agri_objects={agri_count}")


if __name__ == "__main__":
    main()
