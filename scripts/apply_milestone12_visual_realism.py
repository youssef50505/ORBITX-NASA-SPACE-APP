import math
import os
import bpy
from mathutils import Vector


def ensure_node(nodes, name, node_type, location=(0, 0)):
    node = nodes.get(name)
    if node is None or node.bl_idname != node_type:
        if node is not None:
            nodes.remove(node)
        node = nodes.new(node_type)
        node.name = name
    node.location = location
    return node


def upgrade_orbit_curves_and_materials():
    curve_specs = {
        "MS_Orbit_Path_MRO": (3.8, (0.22, 0.78, 1.0), 4.0),
        "MS_Orbit_Path_Odyssey": (3.8, (1.0, 0.58, 0.18), 4.0),
        "MS_Orbit_Path_GlobalSurveyor": (3.8, (0.35, 0.95, 0.65), 4.0),
        "MS_Orbit_Path_MAVEN": (4.2, (0.95, 0.35, 0.85), 4.2),
        "MS_Orbit_Path_Phobos": (5.2, (0.48, 0.78, 1.0), 4.6),
        "MS_Orbit_Path_Deimos": (5.2, (1.0, 0.84, 0.36), 4.6),
    }

    for cname, (bev_depth, fallback_col, strength) in curve_specs.items():
        obj = bpy.data.objects.get(cname)
        if obj is None or obj.type != "CURVE":
            continue
        obj.data.resolution_u = 128
        obj.data.bevel_depth = bev_depth
        obj.data.bevel_resolution = 4
        if hasattr(obj, "visible_shadow"):
            obj.visible_shadow = False

        mat = obj.data.materials[0] if obj.data.materials else None
        if mat is None:
            continue

        # Preserve existing base telemetry color if present
        base_col = fallback_col
        if mat.node_tree:
            old_em = mat.node_tree.nodes.get("Emission")
            if old_em and "Color" in old_em.inputs:
                c = old_em.inputs["Color"].default_value
                if (c[0] + c[1] + c[2]) > 0.05:
                    base_col = (c[0], c[1], c[2])

        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED"
        elif hasattr(mat, "blend_method"):
            mat.blend_method = "BLEND"
        if hasattr(mat, "use_backface_culling"):
            mat.use_backface_culling = False

        nt = mat.node_tree
        nt.nodes.clear()

        out = nt.nodes.new("ShaderNodeOutputMaterial")
        out.name = "Material Output"
        out.location = (620, 0)

        lw = nt.nodes.new("ShaderNodeLayerWeight")
        lw.name = "Orbit_LayerWeight"
        lw.location = (-260, 120)
        lw.inputs["Blend"].default_value = 0.35

        inv = nt.nodes.new("ShaderNodeMath")
        inv.name = "Orbit_Core_Mask"
        inv.operation = "SUBTRACT"
        inv.use_clamp = True
        inv.location = (-60, 120)
        inv.inputs[0].default_value = 1.0

        pow_node = nt.nodes.new("ShaderNodeMath")
        pow_node.name = "Orbit_Falloff"
        pow_node.operation = "POWER"
        pow_node.use_clamp = True
        pow_node.location = (120, 120)
        pow_node.inputs[1].default_value = 1.8

        transp = nt.nodes.new("ShaderNodeBsdfTransparent")
        transp.name = "Orbit_Transparent"
        transp.location = (120, -140)

        em = nt.nodes.new("ShaderNodeEmission")
        em.name = "Emission"
        em.location = (120, -20)
        em.inputs["Color"].default_value = (base_col[0], base_col[1], base_col[2], 1.0)
        em.inputs["Strength"].default_value = strength

        mix_sh = nt.nodes.new("ShaderNodeMixShader")
        mix_sh.name = "Orbit_MixShader"
        mix_sh.location = (380, 0)

        nt.links.new(lw.outputs["Facing"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], pow_node.inputs[0])
        nt.links.new(pow_node.outputs["Value"], mix_sh.inputs["Fac"])
        nt.links.new(transp.outputs["BSDF"], mix_sh.inputs[1])
        nt.links.new(em.outputs["Emission"], mix_sh.inputs[2])
        nt.links.new(mix_sh.outputs["Shader"], out.inputs["Surface"])


def upgrade_moons():
    moon_specs = {
        "MS_Moon_Phobos": (
            21000.0,
            (0.16, 0.14, 0.13, 1.0),
            (0.48, 0.43, 0.38, 1.0),
            0.10,
            14.0,
            0.55,
            (0.071, 0.064, 0.058, 1.0),
        ),
        "MS_Moon_Deimos": (
            18000.0,
            (0.22, 0.19, 0.16, 1.0),
            (0.56, 0.50, 0.43, 1.0),
            0.12,
            20.0,
            0.35,
            (0.068, 0.062, 0.055, 1.0),
        ),
    }

    for mname, (sc, dark_col, bright_col, em_str, vor_scale, bump_str, nom_albedo) in moon_specs.items():
        obj = bpy.data.objects.get(mname)
        if obj is None:
            continue
        obj.scale = (sc, sc, sc)
        obj.hide_viewport = False
        obj.hide_render = False

        mat = bpy.data.materials.get(mname)
        if mat is None and obj.data.materials:
            mat = obj.data.materials[0]
        if mat is None:
            continue

        nt = mat.node_tree
        nt.nodes.clear()

        out = nt.nodes.new("ShaderNodeOutputMaterial")
        out.name = "Material Output"
        out.location = (640, 0)

        tc = nt.nodes.new("ShaderNodeTexCoord")
        tc.location = (-720, 0)

        noise_macro = nt.nodes.new("ShaderNodeTexNoise")
        noise_macro.location = (-480, 180)
        noise_macro.inputs["Scale"].default_value = 3.2
        noise_macro.inputs["Detail"].default_value = 8.0
        noise_macro.inputs["Roughness"].default_value = 0.65

        vor = nt.nodes.new("ShaderNodeTexVoronoi")
        vor.name = "Voronoi Texture"
        vor.location = (-480, -80)
        vor.feature = "SMOOTH_F1"
        vor.inputs["Scale"].default_value = vor_scale

        mix_tex = nt.nodes.new("ShaderNodeMix")
        mix_tex.data_type = "FLOAT"
        mix_tex.location = (-240, 80)
        mix_tex.inputs[0].default_value = 0.55  # Blend craters into albedo noise

        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.location = (-20, 140)
        ramp.color_ramp.elements[0].position = 0.25
        ramp.color_ramp.elements[0].color = dark_col
        ramp.color_ramp.elements[1].position = 0.78
        ramp.color_ramp.elements[1].color = bright_col

        bump = nt.nodes.new("ShaderNodeBump")
        bump.name = "Bump"
        bump.location = (-20, -140)
        bump.inputs["Strength"].default_value = bump_str
        bump.inputs["Distance"].default_value = 0.012

        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.name = "Principled BSDF"
        bsdf.location = (300, 20)
        bsdf.inputs["Base Color"].default_value = nom_albedo
        bsdf.inputs["Roughness"].default_value = 0.88
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = em_str

        nt.links.new(tc.outputs["Object"], noise_macro.inputs["Vector"])
        nt.links.new(tc.outputs["Object"], vor.inputs["Vector"])
        nt.links.new(noise_macro.outputs["Fac"], mix_tex.inputs[2])
        nt.links.new(vor.outputs["Distance"], mix_tex.inputs[3])
        nt.links.new(mix_tex.outputs[0], ramp.inputs["Fac"])
        nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
        if "Emission Color" in bsdf.inputs:
            nt.links.new(ramp.outputs["Color"], bsdf.inputs["Emission Color"])
        nt.links.new(mix_tex.outputs[0], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])


def refine_explore_beacon_and_atmosphere():
    pin = bpy.data.objects.get("MARS_NEW_Explore_Area_Pin")
    if pin:
        pin.scale = (0.18, 0.18, 0.14)
    ring = bpy.data.objects.get("MARS_NEW_Explore_Area_Ring")
    if ring:
        ring.scale = (0.45, 0.45, 0.45)

    beacon_mat = bpy.data.materials.get("NEW_Explore_Zone_Marker")
    if beacon_mat and beacon_mat.node_tree:
        em = beacon_mat.node_tree.nodes.get("Emission")
        if em and "Strength" in em.inputs:
            em.inputs["Strength"].default_value = 3.2

    rotator = bpy.data.objects.get("MARS_NEW_Globe_Rotator")
    if rotator is None:
        return

    # Remove old shell if regenerating cleanly
    old = bpy.data.objects.get("MS_Mars_Atmosphere_Shell")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=308.5, location=(0, 0, 0))
    atmo = bpy.context.active_object
    atmo.name = "MS_Mars_Atmosphere_Shell"
    atmo.parent = rotator
    atmo.location = (0.0, 0.0, 0.0)
    for poly in atmo.data.polygons:
        poly.use_smooth = True
    if hasattr(atmo, "visible_shadow"):
        atmo.visible_shadow = False

    # Link into same collections as MARS_NEW_Globe
    globe = bpy.data.objects.get("MARS_NEW_Globe")
    if globe:
        for col in globe.users_collection:
            if atmo.name not in col.objects:
                col.objects.link(atmo)
        for col in list(atmo.users_collection):
            if col not in globe.users_collection:
                col.objects.unlink(atmo)

    amat = bpy.data.materials.get("MS_Mars_Atmosphere_Mat")
    if amat is None:
        amat = bpy.data.materials.new("MS_Mars_Atmosphere_Mat")
    amat.use_nodes = True
    if hasattr(amat, "surface_render_method"):
        amat.surface_render_method = "BLENDED"
    elif hasattr(amat, "blend_method"):
        amat.blend_method = "BLEND"

    nt = amat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (760, 0)

    geom = nt.nodes.new("ShaderNodeNewGeometry")
    geom.location = (-560, -180)

    sun_vec = nt.nodes.new("ShaderNodeCombineXYZ")
    sun_vec.location = (-560, -340)
    sun_vec.inputs["X"].default_value = -0.7536
    sun_vec.inputs["Y"].default_value = 0.3977
    sun_vec.inputs["Z"].default_value = 0.5233

    vdot = nt.nodes.new("ShaderNodeVectorMath")
    vdot.operation = "DOT_PRODUCT"
    vdot.location = (-340, -240)

    day_map = nt.nodes.new("ShaderNodeMapRange")
    day_map.clamp = True
    day_map.location = (-120, -220)
    day_map.inputs["From Min"].default_value = -0.25
    day_map.inputs["From Max"].default_value = 0.55
    day_map.inputs["To Min"].default_value = 0.05
    day_map.inputs["To Max"].default_value = 1.0

    lw = nt.nodes.new("ShaderNodeLayerWeight")
    lw.location = (-560, 140)
    lw.inputs["Blend"].default_value = 0.25

    rim_pow = nt.nodes.new("ShaderNodeMath")
    rim_pow.operation = "POWER"
    rim_pow.use_clamp = True
    rim_pow.location = (-340, 140)
    rim_pow.inputs[1].default_value = 3.2

    rim_fade = nt.nodes.new("ShaderNodeMath")
    rim_fade.operation = "MULTIPLY"
    rim_fade.use_clamp = True
    rim_fade.location = (-120, 140)

    # Edge falloff: suppress very outer grazing silhouette harsh band via (1 - facing)^2 * facing^3
    edge_soft = nt.nodes.new("ShaderNodeMath")
    edge_soft.operation = "SUBTRACT"
    edge_soft.use_clamp = True
    edge_soft.location = (-340, -20)
    edge_soft.inputs[0].default_value = 1.0

    alpha_mul = nt.nodes.new("ShaderNodeMath")
    alpha_mul.operation = "MULTIPLY"
    alpha_mul.use_clamp = True
    alpha_mul.location = (100, 60)

    alpha_gain = nt.nodes.new("ShaderNodeMath")
    alpha_gain.operation = "MULTIPLY"
    alpha_gain.use_clamp = True
    alpha_gain.location = (280, 60)
    alpha_gain.inputs[1].default_value = 0.85

    # Day butterscotch -> twilight blue rim color ramp
    col_ramp = nt.nodes.new("ShaderNodeValToRGB")
    col_ramp.location = (-120, -40)
    col_ramp.color_ramp.elements[0].position = 0.12
    col_ramp.color_ramp.elements[0].color = (0.36, 0.62, 0.96, 1.0)
    col_ramp.color_ramp.elements[1].position = 0.82
    col_ramp.color_ramp.elements[1].color = (0.94, 0.56, 0.30, 1.0)

    transp = nt.nodes.new("ShaderNodeBsdfTransparent")
    transp.location = (280, -160)

    em = nt.nodes.new("ShaderNodeEmission")
    em.location = (280, -40)
    em.inputs["Strength"].default_value = 2.4

    mix_sh = nt.nodes.new("ShaderNodeMixShader")
    mix_sh.location = (540, 0)

    nt.links.new(geom.outputs["Normal"], vdot.inputs[0])
    nt.links.new(sun_vec.outputs["Vector"], vdot.inputs[1])
    nt.links.new(vdot.outputs["Value"], day_map.inputs["Value"])

    nt.links.new(lw.outputs["Facing"], rim_pow.inputs[0])
    nt.links.new(lw.outputs["Facing"], edge_soft.inputs[1])
    nt.links.new(rim_pow.outputs["Value"], rim_fade.inputs[0])
    nt.links.new(edge_soft.outputs["Value"], rim_fade.inputs[1])

    nt.links.new(rim_fade.outputs["Value"], alpha_mul.inputs[0])
    nt.links.new(day_map.outputs["Result"], alpha_mul.inputs[1])
    nt.links.new(alpha_mul.outputs["Value"], alpha_gain.inputs[0])

    nt.links.new(day_map.outputs["Result"], col_ramp.inputs["Fac"])
    nt.links.new(col_ramp.outputs["Color"], em.inputs["Color"])

    nt.links.new(alpha_gain.outputs["Value"], mix_sh.inputs["Fac"])
    nt.links.new(transp.outputs["BSDF"], mix_sh.inputs[1])
    nt.links.new(em.outputs["Emission"], mix_sh.inputs[2])
    nt.links.new(mix_sh.outputs["Shader"], out.inputs["Surface"])

    atmo.data.materials.append(amat)


def create_space_sun_in_planet_anchor():
    anchor = bpy.data.objects.get("Planet_Anchor")
    globe = bpy.data.objects.get("MARS_NEW_Globe")
    if anchor is None:
        return

    for old_name in ("MS_Visible_Sun_Corona", "MS_Visible_Sun_Space"):
        old = bpy.data.objects.get(old_name)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)

    # Positioned cleanly inside Planet_Anchor space (upper-left cosmic backdrop for Key 2)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=120.0, location=(0, 0, 0))
    sun_obj = bpy.context.active_object
    sun_obj.name = "MS_Visible_Sun_Space"
    sun_obj.parent = anchor
    sun_obj.location = (-1750.0, 1150.0, 3000.0 + 1280.0)
    for poly in sun_obj.data.polygons:
        poly.use_smooth = True
    if hasattr(sun_obj, "visible_shadow"):
        sun_obj.visible_shadow = False

    smat = bpy.data.materials.get("MS_Visible_Sun_Mat")
    if smat is None:
        smat = bpy.data.materials.new("MS_Visible_Sun_Mat")
    smat.use_nodes = True
    if hasattr(smat, "surface_render_method"):
        smat.surface_render_method = "BLENDED"
    elif hasattr(smat, "blend_method"):
        smat.blend_method = "BLEND"
    nt = smat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (860, 0)
    sgeo = nt.nodes.new("ShaderNodeNewGeometry")
    sgeo.location = (-560, 140)
    sdot = nt.nodes.new("ShaderNodeVectorMath")
    sdot.operation = "DOT_PRODUCT"
    sdot.location = (-360, 140)
    sabs = nt.nodes.new("ShaderNodeMath")
    sabs.operation = "ABSOLUTE"
    sabs.location = (-180, 140)
    sback = nt.nodes.new("ShaderNodeMath")
    sback.operation = "SUBTRACT"
    sback.use_clamp = True
    sback.location = (-180, -20)
    sback.inputs[0].default_value = 1.0
    sfront = nt.nodes.new("ShaderNodeMath")
    sfront.operation = "MULTIPLY"
    sfront.use_clamp = True
    sfront.location = (0, 140)
    sedge = nt.nodes.new("ShaderNodeMapRange")
    sedge.interpolation_type = "SMOOTHSTEP"
    sedge.clamp = True
    sedge.location = (180, 140)
    sedge.inputs["From Min"].default_value = 0.08
    sedge.inputs["From Max"].default_value = 1.0
    spow = nt.nodes.new("ShaderNodeMath")
    spow.operation = "POWER"
    spow.use_clamp = True
    spow.location = (360, 140)
    spow.inputs[1].default_value = 6.0
    smul = nt.nodes.new("ShaderNodeMath")
    smul.operation = "MULTIPLY"
    smul.use_clamp = False
    smul.location = (520, 140)
    smul.inputs[1].default_value = 12.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (180, -80)
    ramp.color_ramp.elements[0].position = 0.25
    ramp.color_ramp.elements[0].color = (1.0, 0.50, 0.14, 1.0)
    ramp.color_ramp.elements[1].position = 0.88
    ramp.color_ramp.elements[1].color = (1.0, 0.96, 0.88, 1.0)
    stransp = nt.nodes.new("ShaderNodeBsdfTransparent")
    stransp.location = (520, -180)
    em = nt.nodes.new("ShaderNodeEmission")
    em.location = (520, -40)
    sadd = nt.nodes.new("ShaderNodeAddShader")
    sadd.location = (690, 0)
    nt.links.new(sgeo.outputs["Normal"], sdot.inputs[0])
    nt.links.new(sgeo.outputs["Incoming"], sdot.inputs[1])
    nt.links.new(sdot.outputs["Value"], sabs.inputs[0])
    nt.links.new(sgeo.outputs["Backfacing"], sback.inputs[1])
    nt.links.new(sabs.outputs["Value"], sfront.inputs[0])
    nt.links.new(sback.outputs["Value"], sfront.inputs[1])
    nt.links.new(sfront.outputs["Value"], sedge.inputs["Value"])
    nt.links.new(sedge.outputs["Result"], spow.inputs[0])
    nt.links.new(spow.outputs["Value"], smul.inputs[0])
    nt.links.new(smul.outputs["Value"], em.inputs["Strength"])
    nt.links.new(sfront.outputs["Value"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], em.inputs["Color"])
    nt.links.new(stransp.outputs["BSDF"], sadd.inputs[0])
    nt.links.new(em.outputs["Emission"], sadd.inputs[1])
    nt.links.new(sadd.outputs["Shader"], out.inputs["Surface"])
    sun_obj.data.materials.append(smat)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=580.0, location=(0, 0, 0))
    corona = bpy.context.active_object
    corona.name = "MS_Visible_Sun_Corona"
    corona.parent = sun_obj
    corona.location = (0.0, 0.0, 0.0)
    for poly in corona.data.polygons:
        poly.use_smooth = True
    if hasattr(corona, "visible_shadow"):
        corona.visible_shadow = False

    cmat = bpy.data.materials.get("MS_Visible_Sun_Corona_Mat")
    if cmat is None:
        cmat = bpy.data.materials.new("MS_Visible_Sun_Corona_Mat")
    cmat.use_nodes = True
    if hasattr(cmat, "surface_render_method"):
        cmat.surface_render_method = "BLENDED"
    elif hasattr(cmat, "blend_method"):
        cmat.blend_method = "BLEND"

    cnt = cmat.node_tree
    cnt.nodes.clear()
    cout = cnt.nodes.new("ShaderNodeOutputMaterial")
    cout.location = (760, 0)
    cgeo = cnt.nodes.new("ShaderNodeNewGeometry")
    cgeo.location = (-560, 120)
    cdot = cnt.nodes.new("ShaderNodeVectorMath")
    cdot.operation = "DOT_PRODUCT"
    cdot.location = (-340, 160)
    cabs = cnt.nodes.new("ShaderNodeMath")
    cabs.operation = "ABSOLUTE"
    cabs.location = (-160, 160)
    cback = cnt.nodes.new("ShaderNodeMath")
    cback.operation = "SUBTRACT"
    cback.use_clamp = True
    cback.location = (-160, -20)
    cback.inputs[0].default_value = 1.0
    cfront = cnt.nodes.new("ShaderNodeMath")
    cfront.operation = "MULTIPLY"
    cfront.use_clamp = True
    cfront.location = (20, 120)
    crange = cnt.nodes.new("ShaderNodeMapRange")
    crange.interpolation_type = "LINEAR"
    crange.clamp = True
    crange.location = (200, 120)
    crange.inputs["From Min"].default_value = 0.05
    crange.inputs["From Max"].default_value = 1.0
    crange.inputs["To Min"].default_value = 0.0
    crange.inputs["To Max"].default_value = 1.0
    cpow = cnt.nodes.new("ShaderNodeMath")
    cpow.operation = "POWER"
    cpow.use_clamp = True
    cpow.location = (380, 120)
    cpow.inputs[1].default_value = 11.0
    cmul = cnt.nodes.new("ShaderNodeMath")
    cmul.operation = "MULTIPLY"
    cmul.use_clamp = False
    cmul.location = (440, -20)
    cmul.inputs[1].default_value = 14.0
    ctransp = cnt.nodes.new("ShaderNodeBsdfTransparent")
    ctransp.location = (440, -180)
    cem = cnt.nodes.new("ShaderNodeEmission")
    cem.location = (440, -80)
    cem.inputs["Color"].default_value = (1.0, 0.56, 0.20, 1.0)
    cadd = cnt.nodes.new("ShaderNodeAddShader")
    cadd.location = (600, 0)

    cnt.links.new(cgeo.outputs["Normal"], cdot.inputs[0])
    cnt.links.new(cgeo.outputs["Incoming"], cdot.inputs[1])
    cnt.links.new(cdot.outputs["Value"], cabs.inputs[0])
    cnt.links.new(cgeo.outputs["Backfacing"], cback.inputs[1])
    cnt.links.new(cabs.outputs["Value"], cfront.inputs[0])
    cnt.links.new(cback.outputs["Value"], cfront.inputs[1])
    cnt.links.new(cfront.outputs["Value"], crange.inputs["Value"])
    cnt.links.new(crange.outputs["Result"], cpow.inputs[0])
    cnt.links.new(cpow.outputs["Value"], cmul.inputs[0])
    cnt.links.new(cmul.outputs["Value"], cem.inputs["Strength"])
    cnt.links.new(ctransp.outputs["BSDF"], cadd.inputs[0])
    cnt.links.new(cem.outputs["Emission"], cadd.inputs[1])
    cnt.links.new(cadd.outputs["Shader"], cout.inputs["Surface"])
    corona.data.materials.append(cmat)

    if globe:
        for obj in (sun_obj, corona):
            for col in globe.users_collection:
                if obj.name not in col.objects:
                    col.objects.link(obj)
            for col in list(obj.users_collection):
                if col not in globe.users_collection:
                    col.objects.unlink(obj)


def upgrade_world_sun_disc_and_auto_space_stars():
    scene = bpy.context.scene
    world = scene.world
    if world is None or not world.node_tree:
        return
    nt = world.node_tree
    nodes = nt.nodes

    # 1. Calibrate Sun direction & add crisp Solar Photosphere Disc to Martian sky gradient
    sun_dir_node = nodes.get("Sun_Direction")
    if sun_dir_node and "Vector" in sun_dir_node.inputs:
        sun_dir_node.inputs["Vector"].default_value = (-0.36, 0.924, 0.13)
    sun_dot_clamp = nodes.get("Sun_Dot_Clamp")
    sun_glow_core = nodes.get("Sun_Glow_Core")
    ground_fill = nodes.get("Ground_Fill")
    dir_norm = nodes.get("Dir_Normalize")
    if sun_dot_clamp and sun_glow_core and ground_fill:
        disc_pow = ensure_node(nodes, "MS_Solar_Disc_Power", "ShaderNodeMath", (160, 420))
        disc_pow.operation = "POWER"
        disc_pow.use_clamp = True
        disc_pow.inputs[1].default_value = 2600.0

        disc_gain = ensure_node(nodes, "MS_Solar_Disc_Gain", "ShaderNodeMath", (340, 420))
        disc_gain.operation = "MULTIPLY"
        disc_gain.use_clamp = True
        disc_gain.inputs[1].default_value = 1.0

        disc_mix = ensure_node(nodes, "MS_Solar_Disc_Mix", "ShaderNodeMix", (520, 320))
        disc_mix.data_type = "RGBA"
        disc_mix.blend_type = "MIX"
        disc_mix.clamp_result = False
        # High-intensity golden-white photosphere disc (inputs[7] = B RGBA)
        disc_mix.inputs[7].default_value = (4.8, 4.3, 3.3, 1.0)

        nt.links.new(sun_dot_clamp.outputs["Value"], disc_pow.inputs[0])
        nt.links.new(disc_pow.outputs["Value"], disc_gain.inputs[0])
        nt.links.new(disc_gain.outputs["Value"], disc_mix.inputs[0])
        nt.links.new(sun_glow_core.outputs["Result"], disc_mix.inputs[6])

        # Procedural Phobos & Deimos moons inside surface sky (zero image datablocks, crescent-lit)
        prev_sky_socket = disc_mix.outputs[2]
        if dir_norm is not None:
            sun_vec = Vector((-0.36, 0.924, 0.13)).normalized()
            moon_defs = [
                ("Phobos", Vector((-0.02, 0.985, 0.175)).normalized(), 0.021, (0.68, 0.57, 0.48, 1.0)),
                ("Deimos", Vector((0.18, 0.969, 0.172)).normalized(), 0.014, (0.78, 0.67, 0.56, 1.0)),
            ]
            for m_idx, (m_name, m_dir, ang_rad, base_col) in enumerate(moon_defs):
                y_off = 560 + m_idx * 240
                mdot = ensure_node(nodes, f"MS_Sky_{m_name}_Dot", "ShaderNodeVectorMath", (160, y_off))
                mdot.operation = "DOT_PRODUCT"
                mdot.inputs[1].default_value = (m_dir.x, m_dir.y, m_dir.z)

                # Slight rocky crater albedo variation
                mnoise = ensure_node(nodes, f"MS_Sky_{m_name}_Noise", "ShaderNodeTexNoise", (160, y_off - 120))
                mnoise.inputs["Scale"].default_value = 140.0
                mnoise.inputs["Detail"].default_value = 5.0
                mnoise.inputs["Roughness"].default_value = 0.65

                m_disc = ensure_node(nodes, f"MS_Sky_{m_name}_Disc", "ShaderNodeMapRange", (340, y_off))
                m_disc.interpolation_type = "SMOOTHSTEP"
                m_disc.clamp = True
                m_disc.inputs["From Min"].default_value = math.cos(ang_rad * 1.12)
                m_disc.inputs["From Max"].default_value = math.cos(ang_rad * 0.88)

                # Side-lit crescent terminator facing Sun
                mscale = ensure_node(nodes, f"MS_Sky_{m_name}_Proj", "ShaderNodeVectorMath", (160, y_off + 90))
                mscale.operation = "SCALE"
                mscale.inputs[0].default_value = (m_dir.x, m_dir.y, m_dir.z)
                mtang = ensure_node(nodes, f"MS_Sky_{m_name}_Tang", "ShaderNodeVectorMath", (340, y_off + 90))
                mtang.operation = "SUBTRACT"
                sun_side = (sun_vec - m_dir * sun_vec.dot(m_dir)).normalized()
                mphasedot = ensure_node(nodes, f"MS_Sky_{m_name}_PhaseDot", "ShaderNodeVectorMath", (520, y_off + 90))
                mphasedot.operation = "DOT_PRODUCT"
                mphasedot.inputs[1].default_value = (sun_side.x, sun_side.y, sun_side.z)
                mphase = ensure_node(nodes, f"MS_Sky_{m_name}_Phase", "ShaderNodeMapRange", (700, y_off + 90))
                mphase.interpolation_type = "SMOOTHSTEP"
                mphase.clamp = True
                mphase.inputs["From Min"].default_value = -ang_rad * 0.65
                mphase.inputs["From Max"].default_value = ang_rad * 0.85
                mphase.inputs["To Min"].default_value = 0.24
                mphase.inputs["To Max"].default_value = 1.0

                mshade = ensure_node(nodes, f"MS_Sky_{m_name}_Shade", "ShaderNodeMapRange", (340, y_off - 120))
                mshade.clamp = True
                mshade.inputs["From Min"].default_value = 0.30
                mshade.inputs["From Max"].default_value = 0.72
                mshade.inputs["To Min"].default_value = 0.62
                mshade.inputs["To Max"].default_value = 1.15

                mbright = ensure_node(nodes, f"MS_Sky_{m_name}_Bright", "ShaderNodeMath", (860, y_off + 20))
                mbright.operation = "MULTIPLY"
                mtint = ensure_node(nodes, f"MS_Sky_{m_name}_Tint", "ShaderNodeVectorMath", (1020, y_off + 20))
                mtint.operation = "SCALE"
                mtint.inputs[0].default_value = (base_col[0], base_col[1], base_col[2])

                mmix = ensure_node(nodes, f"MS_Sky_{m_name}_Mix", "ShaderNodeMix", (1200, y_off))
                mmix.data_type = "RGBA"
                mmix.blend_type = "MIX"
                mmix.clamp_result = False

                nt.links.new(dir_norm.outputs["Vector"], mdot.inputs[0])
                nt.links.new(dir_norm.outputs["Vector"], mnoise.inputs["Vector"])
                nt.links.new(mdot.outputs["Value"], m_disc.inputs["Value"])
                nt.links.new(mdot.outputs["Value"], mscale.inputs["Scale"])
                nt.links.new(dir_norm.outputs["Vector"], mtang.inputs[0])
                nt.links.new(mscale.outputs["Vector"], mtang.inputs[1])
                nt.links.new(mtang.outputs["Vector"], mphasedot.inputs[0])
                nt.links.new(mphasedot.outputs["Value"], mphase.inputs["Value"])
                nt.links.new(mnoise.outputs["Fac"], mshade.inputs["Value"])
                nt.links.new(mphase.outputs["Result"], mbright.inputs[0])
                nt.links.new(mshade.outputs["Result"], mbright.inputs[1])
                nt.links.new(mbright.outputs["Value"], mtint.inputs["Scale"])
                nt.links.new(m_disc.outputs["Result"], mmix.inputs[0])
                nt.links.new(prev_sky_socket, mmix.inputs[6])
                nt.links.new(mtint.outputs["Vector"], mmix.inputs[7])
                prev_sky_socket = mmix.outputs[2]

        nt.links.new(prev_sky_socket, ground_fill.inputs[6])

    # 2. Automatic non-script camera-clip_end gating for Deep Space Stars in Orbit View
    star_vis = nodes.get("Star_Visibility_Factor")
    cam_mix = nodes.get("Camera_Stars_vs_Lighting")
    lp = nodes.get("Light Path")
    if star_vis and cam_mix and lp:
        clip_proxy = ensure_node(nodes, "MS_Orbit_Cam_Clip_Proxy", "ShaderNodeValue", (220, -420))
        # Remove old driver on default_value if present
        if world.animation_data:
            for d in list(world.animation_data.drivers):
                if "MS_Orbit_Cam_Clip_Proxy" in d.data_path:
                    world.animation_data.drivers.remove(d)
        fcurve = clip_proxy.outputs[0].driver_add("default_value")
        drv = fcurve.driver
        drv.type = "AVERAGE"
        for v in list(drv.variables):
            drv.variables.remove(v)
        var = drv.variables.new()
        var.name = "cam_clip"
        var.type = "SINGLE_PROP"
        var.targets[0].id_type = "SCENE"
        var.targets[0].id = scene
        var.targets[0].data_path = "camera.data.clip_end"

        clip_map = ensure_node(nodes, "MS_Orbit_Cam_Clip_Map", "ShaderNodeMapRange", (420, -420))
        clip_map.clamp = True
        clip_map.inputs["From Min"].default_value = 500000.0
        clip_map.inputs["From Max"].default_value = 5000000.0
        clip_map.inputs["To Min"].default_value = 0.0
        clip_map.inputs["To Max"].default_value = 1.0

        orbit_cam_ray = ensure_node(nodes, "MS_Orbit_Star_CamRay", "ShaderNodeMath", (620, -340))
        orbit_cam_ray.operation = "MULTIPLY"
        orbit_cam_ray.use_clamp = True

        star_gate = ensure_node(nodes, "MS_Star_Camera_Gate", "ShaderNodeMath", (820, -240))
        star_gate.operation = "MAXIMUM"
        star_gate.use_clamp = True

        nt.links.new(clip_proxy.outputs["Value"], clip_map.inputs["Value"])
        nt.links.new(lp.outputs["Is Camera Ray"], orbit_cam_ray.inputs[0])
        nt.links.new(clip_map.outputs["Result"], orbit_cam_ray.inputs[1])
        nt.links.new(star_vis.outputs["Value"], star_gate.inputs[0])
        nt.links.new(orbit_cam_ray.outputs["Value"], star_gate.inputs[1])
        nt.links.new(star_gate.outputs["Value"], cam_mix.inputs["Factor"])


def upgrade_cameras_and_framing():
    # 1. Replace cramped MARS_NEW_Globe_Camera drivers with full-system non-Python Generator drivers
    gcam = bpy.data.objects.get("MARS_NEW_Globe_Camera")
    ctrl = bpy.data.objects.get("MARS_NEW_Controls")
    rotator = bpy.data.objects.get("MARS_NEW_Globe_Rotator")
    if gcam:
        if gcam.animation_data:
            for d in list(gcam.animation_data.drivers):
                gcam.animation_data.drivers.remove(d)
        # Base position inside Planet_Anchor frames Mars, Atmosphere, ALL 6 orbits, Phobos, Deimos & Sun
        base_pos = (3050.0, -6150.0, 3000.0 + 2450.0)
        gcam.location = base_pos
        gcam.data.lens = 26.0
        gcam.data.clip_end = 100000000.0
        if rotator:
            has_track = any(c.type == "TRACK_TO" for c in gcam.constraints)
            if not has_track:
                tc = gcam.constraints.new("TRACK_TO")
                tc.target = rotator
                tc.track_axis = "TRACK_NEGATIVE_Z"
                tc.up_axis = "UP_Y"

        if ctrl is not None:
            # Non-script C-evaluated linear zoom drivers: pos(z) = base * (1 - 0.55 * z) around (0,0,3000)
            coeffs = [
                (0, [3050.0, -1677.5]),
                (1, [-6150.0, 3382.5]),
                (2, [5450.0, -1347.5]),
            ]
            for idx, (c0, c1) in coeffs:
                fc = gcam.driver_add("location", idx)
                drv = fc.driver
                drv.type = "AVERAGE"
                for v in list(drv.variables):
                    drv.variables.remove(v)
                var = drv.variables.new()
                var.name = "z"
                var.type = "SINGLE_PROP"
                var.targets[0].id_type = "OBJECT"
                var.targets[0].id = ctrl
                var.targets[0].data_path = '["Globe_Zoom"]'
                for m in list(fc.modifiers):
                    fc.modifiers.remove(m)
                mod = fc.modifiers.new("GENERATOR")
                mod.mode = "POLYNOMIAL"
                mod.poly_order = 1
                mod.coefficients[0] = c0
                mod.coefficients[1] = c1

    # 2. Hide orphaned upright ground-disc ribbon Scheibe_Boden_0, detached bracket bottom.001,
    #    far-horizon rim White_Ring, and stray hovering BusterDrone Sketchfab_model.004 hierarchy.
    def _hide_hierarchy(root_obj):
        if root_obj is None:
            return
        root_obj.hide_viewport = True
        root_obj.hide_render = True
        for ch in list(root_obj.children):
            _hide_hierarchy(ch)

    for stray_name in ("Scheibe_Boden_0", "bottom.001", "White_Ring"):
        stray_obj = bpy.data.objects.get(stray_name)
        if stray_obj:
            stray_obj.hide_viewport = True
            stray_obj.hide_render = True

    _hide_hierarchy(bpy.data.objects.get("Sketchfab_model.004"))

    # 2b. Hide FBX rig pivot/shadow/camera helper boxes on Curiosity & SpiritOpp without deleting
    #     datablocks (preserves 96-object hierarchy invariant), then ground Curiosity flush on HiRISE.
    def _hide_rover_rig_helpers(root_name):
        root_obj = bpy.data.objects.get(root_name)
        if root_obj is None:
            return
        stack = list(root_obj.children)
        while stack:
            obj = stack.pop()
            stack.extend(obj.children)
            if obj.type != "MESH" or not obj.data:
                continue
            mats = [m.name for m in obj.data.materials if m]
            nverts = len(obj.data.vertices)
            is_helper_mat = any(mn.startswith(("pivot", "shadow")) for mn in mats)
            is_empty_box = (len(mats) == 0 and nverts <= 12)
            is_pivot_name = obj.name in ("_root", "_root_p", "shadow_arm", "MARDI") or obj.name.endswith("_p")
            if is_helper_mat or is_empty_box or is_pivot_name:
                obj.hide_viewport = True
                obj.hide_render = True

    _hide_rover_rig_helpers("MS_Curiosity_Root")
    _hide_rover_rig_helpers("MS_MER_SpiritOpp_Root")

    bpy.context.view_layer.update()
    hirise_obj = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
    if hirise_obj is not None:
        inv_terr = hirise_obj.matrix_world.inverted()
        mw_terr = hirise_obj.matrix_world
        for rname in ("MS_Curiosity_Root", "MS_MER_SpiritOpp_Root"):
            r_obj = bpy.data.objects.get(rname)
            if r_obj is None:
                continue
            # Collect lowest visible non-helper real meshes (prefer wheels if present)
            desc = []
            stk = list(r_obj.children)
            while stk:
                o = stk.pop()
                stk.extend(o.children)
                if o.type == "MESH" and not o.hide_render and o.data and len(o.data.vertices) > 0:
                    desc.append(o)
            wheels = [o for o in desc if "wheel" in o.name.lower()]
            probe_objs = wheels if wheels else desc
            if not probe_objs:
                continue
            xs, ys, zmins = [], [], []
            for o in probe_objs:
                pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
                xs.extend(p.x for p in pts)
                ys.extend(p.y for p in pts)
                zmins.append(min(p.z for p in pts))
            cx = sum(xs) / len(xs)
            cy = sum(ys) / len(ys)
            wheel_zmin = min(zmins)
            ok, loc_hit, _, _ = hirise_obj.ray_cast(inv_terr @ Vector((cx, cy, 50.0)), Vector((0.0, 0.0, -1.0)))
            if ok:
                terr_z = (mw_terr @ loc_hit).z
                gap = wheel_zmin - terr_z
                if abs(gap) > 0.015:
                    r_obj.location.z -= gap

    mcam = bpy.data.objects.get("Mars_Camera")
    if mcam:
        mcam.location = Vector((15.0, -24.0, 5.2))
        aim = Vector((-0.26, 0.959, 0.11)).normalized()
        mcam.rotation_euler = aim.to_track_quat("-Z", "Y").to_euler()
        mcam.data.lens = 24.0
        mcam.data.clip_end = 25000.0

    # 3. Expand MS_Surface_Opening_Camera clip_end so distant Jezero ridges never chop off at 80m
    scam = bpy.data.objects.get("MS_Surface_Opening_Camera")
    if scam:
        scam.data.clip_end = 25000.0


def main():
    init_img_count = len(bpy.data.images)
    hirise = bpy.data.objects.get("NASA_HiRISE_DTM_Jezero_FieldSite_4m")
    init_hirise_verts = len(hirise.data.vertices) if hirise else 0

    upgrade_orbit_curves_and_materials()
    upgrade_moons()
    refine_explore_beacon_and_atmosphere()
    create_space_sun_in_planet_anchor()
    upgrade_world_sun_disc_and_auto_space_stars()
    upgrade_cameras_and_framing()

    bpy.context.view_layer.update()

    assert len(bpy.data.images) == init_img_count, f"Image count changed: {init_img_count} -> {len(bpy.data.images)}"
    assert hirise is None or len(hirise.data.vertices) == init_hirise_verts, "HiRISE vertex count changed!"

    # Raycast verification on Mars_Camera
    dg = bpy.context.evaluated_depsgraph_get()
    mcam = bpy.data.objects.get("Mars_Camera")
    if mcam:
        orig = mcam.matrix_world.translation
        fwd = mcam.matrix_world.to_3x3() @ Vector((0, 0, -1))
        hit, loc, _, _, obj, _ = bpy.context.scene.ray_cast(dg, orig, fwd, distance=120.0)
        dist = (loc - orig).length if hit else 999.0
        print(f"MARS_CAM_CHECK hit={hit} obj={obj.name if obj else None} dist={dist:.2f}")

    # Save cleanly in-place with zero .blend1 creation
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_mainfile(compress=True)
    blend1 = bpy.data.filepath + "1"
    if os.path.exists(blend1):
        os.remove(blend1)
    print(f"MILESTONE12_BLENDER_SAVED imgs={len(bpy.data.images)} hirise_verts={init_hirise_verts}")


if __name__ == "__main__":
    main()
