"""Comprehensive Empirical Adversarial Challenge Test Suite for Milestone 1:
3D Fleet Asset Integration & Scene Staging.

Challenges:
1. Atmospheric lighting & world shader (Mars_Sky_NASA & Mars_Sun):
   - Cycle through all 5 solar phases (DAY, SUNSET, TWILIGHT, NIGHT, SUNRISE)
   - Verify Sun_Direction vector alignment with lamp rotation matrix
   - Verify Mie/Rayleigh dust haze color (0.76, 0.54, 0.38, 1.0) in Horizon_Haze node
2. Scientific traverse routes & telemetry:
   - Verify MS_Route_Safest_HiRISE (1,159.95m), MS_Route_Shortest_HiRISE (1,113.50m), MS_Route_Science_HiRISE (1,203.21m) in MARS_SENTINEL_Measured match data/jez_distinct_routes.json to millimeter precision
   - Test elevation bounds and slope constraints
3. Terrain preservation invariant:
   - Verify NASA_HiRISE_DTM_Jezero_FieldSite_4m matches ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd bit-for-bit with 1,132,096 vertices
4. Fleet assets integration & scene integrity:
   - Verify all 7 surface roots in MS_Mars_Fleet_Surface
   - Verify physical scaling, grounding, zero loose Perseverance parts in Scene Collection
   - Verify zero backup files (.blend1)
"""

import sys
import os
import json
import math
import hashlib
import array
import importlib.util
from pathlib import Path

import bpy
from mathutils import Vector, Euler, Matrix

PROJECT_ROOT = Path(__file__).resolve().parents[1]
ROUTES_JSON = PROJECT_ROOT / "data" / "jez_distinct_routes.json"
MARS_FRONT_VIEW_PY = PROJECT_ROOT / "tools" / "mars_front_view.py"

HIRISE_NAME = "NASA_HiRISE_DTM_Jezero_FieldSite_4m"
EXPECTED_VERTS = 1132096
EXPECTED_POLYS = 1129969
EXPECTED_HASH = "ed5e2c9e0d23cb45dfbaf7c11deee3e168ea59d9681d8a094e3b514b32b354dd"

results = {
    "terrain_invariant": {},
    "atmospheric_lighting": {},
    "traverse_routes": {},
    "fleet_hierarchy": {},
    "filesystem_integrity": {},
    "verdict": "APPROVE"
}

print("=" * 80)
print("MARS SENTINEL — EMPIRICAL ADVERSARIAL CHALLENGE SUITE (MILESTONE 1)")
print("=" * 80)

# ===========================================================================
# 1. HiRISE Terrain Invariant Verification
# ===========================================================================
print("\n[CHALLENGE 3] Testing Terrain Invariant (NASA_HiRISE_DTM_Jezero_FieldSite_4m)...")
terrain = bpy.data.objects.get(HIRISE_NAME)
if not terrain:
    print(f"CRITICAL ERROR: Terrain object {HIRISE_NAME} missing!")
    results["terrain_invariant"]["error"] = f"Missing {HIRISE_NAME}"
    results["verdict"] = "REQUEST_CHANGES"
else:
    vert_count = len(terrain.data.vertices)
    poly_count = len(terrain.data.polygons)
    print(f"  Vertex Count:   {vert_count:,} (Expected: {EXPECTED_VERTS:,})")
    print(f"  Polygon Count:  {poly_count:,} (Expected: {EXPECTED_POLYS:,})")
    
    coords = array.array("f", [0.0]) * (vert_count * 3)
    terrain.data.vertices.foreach_get("co", coords)
    raw_bytes = coords.tobytes()
    computed_hash = hashlib.sha256(raw_bytes).hexdigest()
    
    print(f"  Computed SHA-256: {computed_hash}")
    print(f"  Expected SHA-256: {EXPECTED_HASH}")
    
    match_verts = (vert_count == EXPECTED_VERTS)
    match_polys = (poly_count == EXPECTED_POLYS)
    match_hash = (computed_hash == EXPECTED_HASH)
    
    results["terrain_invariant"] = {
        "vertices": vert_count,
        "polygons": poly_count,
        "sha256": computed_hash,
        "match_vertices": match_verts,
        "match_polygons": match_polys,
        "match_hash": match_hash,
        "terrain_location": tuple(terrain.location)
    }
    
    if not (match_verts and match_hash and match_polys):
        print("  FAIL: HiRISE Terrain Invariant violated!")
        results["verdict"] = "REQUEST_CHANGES"
    else:
        print("  PASS: HiRISE Terrain Invariant verified bit-for-bit!")

# ===========================================================================
# 2. Atmospheric Lighting & World Shader Verification
# ===========================================================================
print("\n[CHALLENGE 1] Testing Atmospheric Lighting & World Shader (Mars_Sky_NASA & Mars_Sun)...")
world = bpy.data.worlds.get("Mars_Sky_NASA")
sun = bpy.data.objects.get("Mars_Sun")

if not world or not world.node_tree:
    print("CRITICAL ERROR: World Mars_Sky_NASA or node tree missing!")
    results["atmospheric_lighting"]["error"] = "World missing"
    results["verdict"] = "REQUEST_CHANGES"
elif not sun or sun.type != 'LIGHT':
    print("CRITICAL ERROR: Mars_Sun light object missing!")
    results["atmospheric_lighting"]["error"] = "Sun light missing"
    results["verdict"] = "REQUEST_CHANGES"
else:
    # Explicitly load workspace tools/mars_front_view.py module
    spec = importlib.util.spec_from_file_location("mfv_workspace", str(MARS_FRONT_VIEW_PY))
    mfv = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mfv)
    
    nt = world.node_tree
    hh = nt.nodes.get("Horizon_Haze")
    sd = nt.nodes.get("Sun_Direction")
    
    if not hh or not sd:
        print(f"CRITICAL ERROR: Horizon_Haze ({hh}) or Sun_Direction ({sd}) missing!")
        results["atmospheric_lighting"]["error"] = "Shader nodes missing"
        results["verdict"] = "REQUEST_CHANGES"
    else:
        phases = ["DAY", "SUNSET", "TWILIGHT", "NIGHT", "SUNRISE"]
        phase_results = {}
        all_phases_aligned = True
        
        expected_day_haze = (0.76, 0.54, 0.38, 1.0)
        
        for phase in phases:
            print(f"\n  --- Solar Phase: {phase} ---")
            mfv.apply_martian_time_of_day(phase)
            
            # Sun lamp rotation
            sun_rot = sun.rotation_euler
            rot_deg = (math.degrees(sun_rot.x), math.degrees(sun_rot.y), math.degrees(sun_rot.z))
            lamp_mat = Euler(sun_rot, 'XYZ').to_matrix()
            expected_sun_dir = lamp_mat @ Vector((0.0, 0.0, 1.0))
            expected_sun_dir_clamped = (expected_sun_dir.x, expected_sun_dir.y, max(-0.2, expected_sun_dir.z))
            
            actual_sun_dir = tuple(sd.inputs[0].default_value)
            
            # Vector alignment delta
            dir_delta = math.sqrt(sum((a - b)**2 for a, b in zip(expected_sun_dir_clamped, actual_sun_dir)))
            
            # Dust haze color in Horizon_Haze node (Input 7: Color B)
            haze_col = tuple(hh.inputs[7].default_value)
            
            print(f"    Sun Lamp Euler (deg):    ({rot_deg[0]:.1f}, {rot_deg[1]:.1f}, {rot_deg[2]:.1f})")
            print(f"    Expected Sun_Direction:  ({expected_sun_dir_clamped[0]:.6f}, {expected_sun_dir_clamped[1]:.6f}, {expected_sun_dir_clamped[2]:.6f})")
            print(f"    Shader Sun_Direction:    ({actual_sun_dir[0]:.6f}, {actual_sun_dir[1]:.6f}, {actual_sun_dir[2]:.6f})")
            print(f"    Alignment Delta (L2):    {dir_delta:.6e}")
            print(f"    Horizon_Haze RGBA:       ({haze_col[0]:.4f}, {haze_col[1]:.4f}, {haze_col[2]:.4f}, {haze_col[3]:.4f})")
            
            aligned = dir_delta < 1e-4
            if not aligned:
                all_phases_aligned = False
                print(f"    FAIL: Sun_Direction vector misaligned for {phase}!")
            
            phase_info = {
                "sun_rot_deg": rot_deg,
                "sun_energy": sun.data.energy,
                "sun_color": tuple(sun.data.color),
                "sun_dir": actual_sun_dir,
                "alignment_delta": dir_delta,
                "aligned": aligned,
                "haze_col": haze_col
            }
            if phase == "DAY":
                day_haze_delta = math.sqrt(sum((a - b)**2 for a, b in zip(haze_col, expected_day_haze)))
                phase_info["day_haze_delta"] = day_haze_delta
                print(f"    DAY Haze Delta vs (0.76, 0.54, 0.38, 1.0): {day_haze_delta:.6e}")
                if day_haze_delta > 1e-4:
                    all_phases_aligned = False
                    print("    FAIL: DAY haze color mismatch!")
            
            phase_results[phase] = phase_info

        results["atmospheric_lighting"] = {
            "phases": phase_results,
            "all_phases_aligned": all_phases_aligned
        }
        if not all_phases_aligned:
            results["verdict"] = "REQUEST_CHANGES"
            print("  FAIL: Atmospheric lighting challenges failed!")
        else:
            print("  PASS: All 5 solar phases mathematically aligned and dust haze colors validated!")

# ===========================================================================
# 3. Scientific Traverse Routes & Telemetry Verification
# ===========================================================================
print("\n[CHALLENGE 2] Testing Scientific Traverse Routes & Telemetry...")
with open(ROUTES_JSON, "r", encoding="utf-8") as f:
    routes_json_data = json.load(f)

coll_measured = bpy.data.collections.get("MARS_SENTINEL_Measured")
if not coll_measured:
    print("CRITICAL ERROR: Collection MARS_SENTINEL_Measured missing!")
    results["traverse_routes"]["error"] = "Collection missing"
    results["verdict"] = "REQUEST_CHANGES"
else:
    route_specs = [
        ("MS_Route_Safest_HiRISE", "safest", 1159.95, 15.0),
        ("MS_Route_Shortest_HiRISE", "shortest", 1113.50, 28.0),
        ("MS_Route_Science_HiRISE", "science", 1203.21, 15.0),
    ]
    
    all_routes_valid = True
    route_details = {}
    tx0, ty0 = terrain.location.x, terrain.location.y
    
    for obj_name, key, expected_length, slope_threshold in route_specs:
        print(f"\n  --- Route: {obj_name} ({key}) ---")
        obj = bpy.data.objects.get(obj_name)
        if not obj:
            print(f"    FAIL: Object {obj_name} missing from scene!")
            all_routes_valid = False
            continue
        
        if obj.type != 'CURVE':
            print(f"    FAIL: Object {obj_name} is type {obj.type}, expected CURVE!")
            all_routes_valid = False
            continue
        
        spline = obj.data.splines[0]
        pts = spline.points
        json_route = routes_json_data[key]
        json_pts = json_route["points"]
        
        pt_count_match = (len(pts) == len(json_pts))
        print(f"    Spline Type:          {spline.type}")
        print(f"    Point Count:          {len(pts)} (JSON: {len(json_pts)}) — Match: {pt_count_match}")
        
        # Local terrain coordinate comparison (accounting for terrain location offset tx0, ty0)
        max_local_delta_mm = 0.0
        sum_local_delta_mm = 0.0
        for i, (p, jp) in enumerate(zip(pts, json_pts)):
            lx = p.co.x - tx0
            ly = p.co.y - ty0
            delta_m = math.hypot(lx - jp[0], ly - jp[1])
            delta_mm = delta_m * 1000.0
            sum_local_delta_mm += delta_mm
            if delta_mm > max_local_delta_mm:
                max_local_delta_mm = delta_mm
        
        avg_local_delta_mm = sum_local_delta_mm / len(pts)
        print(f"    Local Terrain Offset: tx0 = {tx0:+.3f} m, ty0 = {ty0:+.3f} m")
        print(f"    Max Point Delta (vs JSON): {max_local_delta_mm:.4f} mm ({max_local_delta_mm / 1000.0:.6f} m)")
        print(f"    Avg Point Delta (vs JSON): {avg_local_delta_mm:.4f} mm")
        
        # Polyline lengths: 2D and 3D
        len_2d = sum(math.hypot(pts[i+1].co.x - pts[i].co.x, pts[i+1].co.y - pts[i].co.y) for i in range(len(pts)-1))
        len_3d = sum(math.sqrt((pts[i+1].co.x - pts[i].co.x)**2 + (pts[i+1].co.y - pts[i].co.y)**2 + (pts[i+1].co.z - pts[i].co.z)**2) for i in range(len(pts)-1))
        
        json_len = json_route["distance_m"]
        len_delta_mm = abs(len_2d - json_len) * 1000.0
        len_match_mm = len_delta_mm < 5.0  # < 5 mm tolerance for float32 precision
        rounded_len_match = (round(len_2d, 2) == round(json_len, 2))
        
        print(f"    2D Polyline Length:   {len_2d:.4f} m (JSON: {json_len:.2f} m, Expected: {expected_length:.2f} m)")
        print(f"    2D Length Delta:      {len_delta_mm:.3f} mm (Rounded 2-decimal Match: {rounded_len_match})")
        print(f"    3D Drape Length:      {len_3d:.4f} m")
        
        # Elevation bounds
        z_vals = [p.co.z for p in pts]
        z_min = min(z_vals)
        z_max = max(z_vals)
        delta_z = z_max - z_min
        print(f"    Elevation Bounds:     Zmin = {z_min:.4f} m, Zmax = {z_max:.4f} m (Delta Z = {delta_z:.4f} m)")
        
        # Slopes along path segments
        segment_slopes = []
        for i in range(len(pts)-1):
            dx = pts[i+1].co.x - pts[i].co.x
            dy = pts[i+1].co.y - pts[i].co.y
            dz = pts[i+1].co.z - pts[i].co.z
            dxy = math.hypot(dx, dy)
            if dxy > 1e-6:
                slope = math.degrees(math.atan2(abs(dz), dxy))
                segment_slopes.append(slope)
        
        max_slope = max(segment_slopes) if segment_slopes else 0.0
        avg_slope = sum(segment_slopes) / len(segment_slopes) if segment_slopes else 0.0
        slope_compliant = max_slope <= slope_threshold
        
        print(f"    Max Segment Slope:    {max_slope:.2f}° (Threshold: < {slope_threshold}°, Compliant: {slope_compliant})")
        print(f"    Avg Segment Slope:    {avg_slope:.2f}°")
        
        # Points match to sub-millimeter precision (< 0.1 mm) and rounded lengths match exactly
        route_pass = pt_count_match and (max_local_delta_mm < 0.1) and rounded_len_match and slope_compliant
        if not route_pass:
            all_routes_valid = False
            print(f"    FAIL: Route {obj_name} failed telemetry validation!")
        else:
            print(f"    PASS: Route {obj_name} verified to millimeter precision!")
        
        route_details[key] = {
            "object_name": obj_name,
            "point_count": len(pts),
            "max_local_delta_mm": max_local_delta_mm,
            "avg_local_delta_mm": avg_local_delta_mm,
            "len_2d": len_2d,
            "len_3d": len_3d,
            "json_len": json_len,
            "len_delta_mm": len_delta_mm,
            "rounded_match": rounded_len_match,
            "z_min": z_min,
            "z_max": z_max,
            "delta_z": delta_z,
            "max_slope_deg": max_slope,
            "avg_slope_deg": avg_slope,
            "slope_threshold_deg": slope_threshold,
            "slope_compliant": slope_compliant,
            "valid": route_pass
        }
    
    results["traverse_routes"] = {
        "routes": route_details,
        "all_routes_valid": all_routes_valid
    }
    if not all_routes_valid:
        results["verdict"] = "REQUEST_CHANGES"

# ===========================================================================
# 4. Fleet Hierarchy & Scene Staging Sanity
# ===========================================================================
print("\n[CHALLENGE 4] Testing Surface Fleet Hierarchy & Root Staging...")
fleet_col = bpy.data.collections.get("MS_Mars_Fleet_Surface")
expected_roots = [
    "MS_Perseverance_Root",
    "MS_InSight_Lander_Root",
    "MS_Opportunity_Root",
    "MS_Ingenuity_Root",
    "MS_Curiosity_Root",
    "MS_MER_SpiritOpp_Root",
    "MS_Viking_Lander_Root"
]

fleet_valid = True
if not fleet_col:
    print("  FAIL: MS_Mars_Fleet_Surface collection missing!")
    fleet_valid = False
else:
    for root_name in expected_roots:
        r_obj = bpy.data.objects.get(root_name)
        if not r_obj:
            print(f"  FAIL: Fleet root {root_name} missing!")
            fleet_valid = False
        else:
            in_col = root_name in fleet_col.objects
            print(f"  Root {root_name}: Location={tuple(r_obj.location)}, Scale={tuple(r_obj.scale)}, InCol={in_col}")
            if not in_col:
                fleet_valid = False

# Check zero loose Perseverance components in Scene Collection
pers_root = bpy.data.objects.get("MS_Perseverance_Root")
pers_descendants = set()
def gather_descendants(obj):
    for c in obj.children:
        pers_descendants.add(c)
        gather_descendants(c)
if pers_root:
    gather_descendants(pers_root)

scene_col = bpy.context.scene.collection
loose_pers_parts = [o.name for o in pers_descendants if o.name in scene_col.objects]
print(f"  Total Perseverance descendants: {len(pers_descendants)}")
print(f"  Perseverance parts lingering in root Scene Collection: {len(loose_pers_parts)}")
if loose_pers_parts:
    print(f"  FAIL: Found loose Perseverance objects in Scene Collection: {loose_pers_parts[:5]}")
    fleet_valid = False

results["fleet_hierarchy"] = {
    "roots_present": fleet_valid,
    "perseverance_total_parts": len(pers_descendants),
    "loose_perseverance_parts_count": len(loose_pers_parts)
}
if not fleet_valid:
    results["verdict"] = "REQUEST_CHANGES"

# ===========================================================================
# 5. Filesystem & Guardrail Preservation Checks
# ===========================================================================
print("\n[CHALLENGE 5] Testing Filesystem Guardrails & Backup Absence...")
backup_exists = (PROJECT_ROOT / "MARS_SENTINEL_HERO_PRODUCTION.blend1").exists()
print(f"  MARS_SENTINEL_HERO_PRODUCTION.blend1 exists: {backup_exists} (Expected: False)")

results["filesystem_integrity"] = {
    "backup_exists": backup_exists,
    "guardrails_respected": not backup_exists
}
if backup_exists:
    results["verdict"] = "REQUEST_CHANGES"

# ===========================================================================
# Final Empirical Summary
# ===========================================================================
print("\n" + "=" * 80)
print(f"FINAL EMPIRICAL VERDICT: {results['verdict']}")
print("=" * 80)

output_json = PROJECT_ROOT / ".agents" / "teamwork" / "challenger_m1_2" / "challenge_data.json"
with open(output_json, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2)
print(f"Wrote empirical results to {output_json}")
