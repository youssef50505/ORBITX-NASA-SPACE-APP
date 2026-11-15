import bpy
import os
import sys
import tempfile
from mathutils import Vector

def render_curiosity():
    print("=== CURIOSITY_RENDER_TEST_BEGIN ===")
    
    # 1. Locate curiosity root
    curiosity_root = bpy.data.objects.get("MS_Curiosity_Root")
    if not curiosity_root:
        print("ERROR: MS_Curiosity_Root not found")
        sys.exit(1)
        
    c_loc = curiosity_root.location
    print(f"Curiosity location: {c_loc}")
    
    # 2. Create camera pointing at Curiosity
    cam_data = bpy.data.cameras.new("TestCam_Curiosity")
    cam_data.clip_start = 0.1
    cam_data.clip_end = 1000.0
    cam_obj = bpy.data.objects.new("TestCam_Curiosity", cam_data)
    bpy.context.scene.collection.objects.link(cam_obj)
    
    # Place camera 4 meters away, slightly elevated
    cam_obj.location = Vector((c_loc.x + 3.0, c_loc.y - 3.0, c_loc.z + 2.0))
    # Point at curiosity
    direction = c_loc - cam_obj.location
    rot_quat = direction.to_track_quat('-Z', 'Y')
    cam_obj.rotation_euler = rot_quat.to_euler()
    
    bpy.context.scene.camera = cam_obj
    
    # 3. Configure render settings
    bpy.context.scene.render.resolution_x = 256
    bpy.context.scene.render.resolution_y = 256
    bpy.context.scene.render.resolution_percentage = 100
    bpy.context.scene.render.image_settings.file_format = 'PNG'
    
    # Use fast EEVEE render
    bpy.context.scene.render.engine = 'BLENDER_EEVEE'
    
    temp_dir = tempfile.gettempdir()
    out_path = os.path.join(temp_dir, "curiosity_test_render.png")
    bpy.context.scene.render.filepath = out_path
    
    print(f"Rendering to: {out_path}")
    bpy.ops.render.render(write_still=True)
    
    if not os.path.isfile(out_path):
        print(f"ERROR: Render output not found at {out_path}")
        sys.exit(1)
        
    sz = os.path.getsize(out_path)
    print(f"Rendered PNG size: {sz} bytes")
    
    # 4. Load rendered image and check for missing texture color (#FF00FF / bright magenta)
    rendered_img = bpy.data.images.load(out_path)
    w, h = rendered_img.size
    pixels = list(rendered_img.pixels) # RGBA floats in [0.0, 1.0]
    
    total_pixels = w * h
    magenta_pixels = 0
    non_black_pixels = 0
    
    for i in range(0, len(pixels), 4):
        r, g, b, a = pixels[i:i+4]
        if r > 0.05 or g > 0.05 or b > 0.05:
            non_black_pixels += 1
        # Blender missing texture magenta: R ~ 1.0, G ~ 0.0, B ~ 1.0
        if r > 0.85 and g < 0.15 and b > 0.85:
            magenta_pixels += 1
            
    print(f"Total pixels: {total_pixels}")
    print(f"Non-black pixels: {non_black_pixels}")
    print(f"Magenta (missing texture) pixels: {magenta_pixels}")
    
    # Clean up test camera
    bpy.data.objects.remove(cam_obj)
    bpy.data.cameras.remove(cam_data)
    bpy.data.images.remove(rendered_img)
    try:
        os.remove(out_path)
    except Exception:
        pass
        
    if magenta_pixels > 0:
        print(f"FAIL: Detected {magenta_pixels} magenta missing texture pixels!")
        sys.exit(2)
        
    print("PASS: Curiosity rendered cleanly with ZERO missing texture magenta pixels!")
    print("=== CURIOSITY_RENDER_TEST_END ===")

if __name__ == "__main__":
    render_curiosity()
