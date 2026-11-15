import bpy
import math
from mathutils import Vector, Quaternion, Euler

bl_info = {
    "name": "Mars Sentinel — Navigation, Day/Night Cycle & Mission Dashboard",
    "author": "Mars Sentinel Team",
    "version": (3, 0, 0),
    "blender": (4, 0, 0),
    "location": "3D Viewport > Keys 1, 2, 3, 4, 5 & N-Panel > Mars",
    "description": "1 = Surface Base Camp & Crew, 2 = Mars Planet & Starfield from Outside, 3 = Mars Agriculture & Hydroponics Zone, 4 = Habitat Pod & Rovers, 5 = Cycle Martian Time of Day",
    "category": "3D View",
}

CTL = "MARS_NEW_Controls"
LAYERS = [
    ("Layer_Elevation", "Elevation"),
    ("Layer_Slope", "Slope"),
    ("Layer_Geological", "Geological"),
    ("Layer_Temperature", "Temperature"),
    ("Layer_Dust", "Dust"),
    ("Layer_Sunlight_Shadow", "Sunlight & Shadow"),
]
CAMERAS = [
    ("Astronauts_Camera", "Surface Base (1)"),
    ("MARS_NEW_Globe_Camera", "Planet Orbit (2)"),
    ("Mars_Camera", "Overview"),
]
PRESETS = [
    ("Jezero", 18.4, 77.5),
    ("Gale", -5.4, 137.8),
    ("Olympus Mons", 18.65, -133.8),
    ("Valles Marineris", -13.9, -59.2),
    ("Hellas", -42.4, 70.5),
    ("Elysium", 25.0, 147.2),
]
LEGEND = {
    "Layer_Elevation": ("NEW_Layer_Elevation", "Color Ramp", "Low -17 m  ->  High 439 m"),
    "Layer_Slope": ("NEW_Layer_Slope", "Color Ramp", "Flat 0 deg  ->  Steep 60 deg"),
    "Layer_Geological": ("NEW_Layer_Geological", "Color Ramp", "Geological units: regolith, iron oxide, basalt, sulfate, clay"),
    "Layer_Temperature": ("NEW_Layer_Temperature", "Color Ramp", "Relative: cold (high / shaded)  ->  warm (low / sunlit)"),
    "Layer_Dust": (None, None, "Dust density: transparent (clear)  ->  dense (dusty)"),
    "Layer_Sunlight_Shadow": ("NEW_Layer_Sunlight_Shadow", "Color Ramp", "Shadow  ->  Sunlight"),
}

TIME_PRESETS = ["DAY", "SUNSET", "TWILIGHT", "NIGHT", "SUNRISE"]

SURFACE_MODELS = (
    "Sketchfab_model",
    "Sketchfab_model.015",
    "Sketchfab_model.007",
    "Joe_Astronaut",
    "Wheels_objs",
    "Body",
    "Sketchfab_model.012",
    "Sketchfab_model.014",
    "Sketchfab_model.002",
    "Sketchfab_model.005",
    "Sketchfab_model.004",
)

OUTSIDE_PLANET_OBJECTS = (
    "MARS_NEW_Globe",
    "MARS_NEW_Explore_Area_Ring",
    "MARS_NEW_Explore_Area_Pin",
    "Sketchfab_model.008",
)


def _redraw(context):
    if not context or not context.window_manager:
        return
    for w in context.window_manager.windows:
        for a in w.screen.areas:
            a.tag_redraw()


def _set_orbit_objects_visible(visible=False):
    for col_name in ("NEW_Mars_Globe", "MS_Mars_Orbital_Fleet"):
        col_globe = bpy.data.collections.get(col_name)
        if col_globe:
            col_globe.hide_viewport = not visible
            col_globe.hide_render = not visible
    for name in OUTSIDE_PLANET_OBJECTS:
        o = bpy.data.objects.get(name)
        if o:
            for d in [o] + list(o.children_recursive):
                d.hide_viewport = not visible
                d.hide_render = not visible


def set_star_visibility(val):
    w = bpy.data.worlds.get("Mars_Sky_NASA")
    if w and w.node_tree:
        sv = w.node_tree.nodes.get("Star_Visibility_Factor")
        if sv:
            sv.inputs[1].default_value = float(val)


def apply_martian_time_of_day(mode="DAY", force_space_stars=False):
    mode = mode.upper()
    c = bpy.data.objects.get(CTL)
    if c is not None:
        c["Time_Of_Day"] = mode

    sun = bpy.data.objects.get("Mars_Sun")
    w = bpy.data.worlds.get("Mars_Sky_NASA")

    # Configure Sun & Atmosphere parameters per Martian solar phase
    if mode == "SUNRISE":
        sun_rot = (math.radians(76.0), 0.0, math.radians(-115.0))
        sun_energy = 1.65
        sun_col = (1.0, 0.72, 0.48)
        sky_stops = [(0.68, 0.38, 0.22, 1.0), (0.42, 0.25, 0.18, 1.0), (0.18, 0.14, 0.16, 1.0)]
        haze_col = (0.82, 0.48, 0.30, 1.0)
        bg_strength = 0.85
        star_factor = 0.18
        light_mult = 1.3
    elif mode == "DAY":
        sun_rot = (math.radians(58.0), 0.0, math.radians(-118.0))
        sun_energy = 2.85
        sun_col = (1.0, 0.93, 0.83)
        sky_stops = [(0.62, 0.43, 0.29, 1.0), (0.47, 0.315, 0.205, 1.0), (0.33, 0.215, 0.135, 1.0)]
        haze_col = (0.76, 0.54, 0.38, 1.0)
        bg_strength = 1.05
        star_factor = 0.0
        light_mult = 0.85
    elif mode == "SUNSET":
        sun_rot = (math.radians(82.0), 0.0, math.radians(65.0))
        sun_energy = 1.35
        sun_col = (0.95, 0.68, 0.52)
        # Authentic NASA Mars sunset: warm dusty amber horizon + bluish forward-scattered solar glow & deep slate zenith
        sky_stops = [(0.58, 0.33, 0.20, 1.0), (0.34, 0.22, 0.20, 1.0), (0.14, 0.13, 0.18, 1.0)]
        haze_col = (0.72, 0.42, 0.26, 1.0)
        bg_strength = 0.72
        star_factor = 0.25
        light_mult = 1.6
    elif mode == "TWILIGHT":
        sun_rot = (math.radians(94.0), 0.0, math.radians(72.0))
        sun_energy = 0.35
        sun_col = (0.65, 0.62, 0.78)
        sky_stops = [(0.24, 0.14, 0.12, 1.0), (0.10, 0.08, 0.12, 1.0), (0.03, 0.03, 0.06, 1.0)]
        haze_col = (0.28, 0.16, 0.12, 1.0)
        bg_strength = 0.32
        star_factor = 0.72
        light_mult = 2.2
    else:  # NIGHT
        sun_rot = (math.radians(125.0), 0.0, math.radians(90.0))
        sun_energy = 0.08
        sun_col = (0.45, 0.55, 0.75)
        sky_stops = [(0.05, 0.03, 0.03, 1.0), (0.02, 0.02, 0.03, 1.0), (0.008, 0.008, 0.015, 1.0)]
        haze_col = (0.06, 0.04, 0.03, 1.0)
        bg_strength = 0.14
        star_factor = 0.92
        light_mult = 2.8

    if sun and sun.type == 'LIGHT':
        sun.rotation_euler = sun_rot
        sun.data.energy = sun_energy
        sun.data.color = sun_col

    if w and w.node_tree:
        nt = w.node_tree
        sg = nt.nodes.get("Sky_Gradient")
        if sg and len(sg.color_ramp.elements) >= 3:
            for idx in range(3):
                sg.color_ramp.elements[idx].color = sky_stops[idx]
        hh = nt.nodes.get("Horizon_Haze")
        if hh and len(hh.inputs) > 7:
            hh.inputs[7].default_value = haze_col
        bg = nt.nodes.get("Background")
        if bg:
            bg.inputs["Strength"].default_value = bg_strength
        sd = nt.nodes.get("Sun_Direction")
        if sd and sun:
            # Compute Sun direction vector directly from Euler rotation
            s_dir = Euler(sun_rot, 'XYZ').to_matrix() @ Vector((0.0, 0.0, 1.0))
            sd.inputs[0].default_value = (s_dir.x, s_dir.y, max(-0.2, s_dir.z))
        sv = nt.nodes.get("Star_Visibility_Factor")
        if sv:
            sv.inputs[1].default_value = 1.0 if force_space_stars else star_factor

    # Adjust artificial Greenhouse & Habitat lights so they shine dramatically at Sunset/Twilight/Night
    base_energies = {
        "MS_Agri_Light_GH1_North": 350.0,
        "MS_Agri_Light_GH1_South": 350.0,
        "MS_Agri_Light_GH2_Dome": 420.0,
        "MS_Agri_Light_Airlock": 180.0,
        "MS_Habitat_Flood_Light": 450.0,
    }
    for lname, base_e in base_energies.items():
        lo = bpy.data.objects.get(lname)
        if lo and lo.type == 'LIGHT':
            lo.data.energy = base_e * light_mult


def _set_viewport_camera_to_pose(context, eye, target, clip_start=0.1, clip_end=20000.0):
    direction = (target - eye).normalized()
    rot_quat = direction.to_track_quat('-Z', 'Y')
    dist = (eye - target).length

    # Also update active scene camera if it's a surface camera so renders match viewport
    cam = bpy.data.objects.get("Astronauts_Camera")
    if cam and cam.type == 'CAMERA':
        if cam.animation_data:
            cam.animation_data_clear()
        cam.location = eye
        cam.rotation_mode = 'QUATERNION'
        cam.rotation_quaternion = rot_quat
        cam.rotation_mode = 'XYZ'
        cam.data.lens = 32.0
        cam.data.clip_start = clip_start
        cam.data.clip_end = clip_end
        context.scene.camera = cam

    for window in context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != 'VIEW_3D':
                continue
            for space in area.spaces:
                if space.type != 'VIEW_3D':
                    continue
                space.clip_start = clip_start
                space.clip_end = clip_end
                space.shading.type = 'MATERIAL'
                space.shading.use_scene_lights = True
                space.shading.use_scene_world = True
                space.overlay.show_relationship_lines = False
                rv3d = space.region_3d
                if rv3d is not None:
                    rv3d.view_perspective = 'PERSP'
                    rv3d.view_location = target
                    rv3d.view_rotation = rot_quat
                    rv3d.view_distance = dist


class VIEW3D_OT_front_on_models(bpy.types.Operator):
    """Key 1: Frame Mars Surface Base Camp, Astronauts, Perseverance Rover, Habitat Pod & Agriculture Greenhouse"""
    bl_idname = "view3d.front_on_models"
    bl_label = "Surface Base & Crew (Key 1)"

    def execute(self, context):
        context.scene.frame_set(76)
        _set_orbit_objects_visible(False)
        c = bpy.data.objects.get(CTL)
        cur_time = c.get("Time_Of_Day", "DAY") if c else "DAY"
        apply_martian_time_of_day(cur_time, force_space_stars=False)

        # Frame the complete surface base camp (Astronauts, Perseverance Rover, Habitat Pod, Greenhouse, Heavy Rover)
        eye = Vector((-1.0, -16.5, 4.6))
        target = Vector((-1.0, 16.0, 1.8))
        _set_viewport_camera_to_pose(context, eye, target, clip_start=0.1, clip_end=20000.0)
        context.view_layer.update()
        _redraw(context)
        self.report({'INFO'}, f"Key 1: Mars Surface Base Camp & Crew ({cur_time})")
        return {'FINISHED'}


class VIEW3D_OT_mars_planet_outside(bpy.types.Operator):
    """Key 2: Frame Mars Planet, Jezero Crater Pin, Orbiting Satellite & Original Milky Way Starfield from Space"""
    bl_idname = "view3d.mars_planet_outside"
    bl_label = "Mars Planet from Outside (Key 2)"

    def execute(self, context):
        context.scene.frame_set(76)
        _set_orbit_objects_visible(True)
        # In space/orbit, always show 100% of the original 90-node Stars_Background!
        set_star_visibility(1.0)

        depsgraph = context.evaluated_depsgraph_get()
        bmin = [1e18, 1e18, 1e18]
        bmax = [-1e18, -1e18, -1e18]
        found = False
        for name in OUTSIDE_PLANET_OBJECTS:
            obj = bpy.data.objects.get(name)
            if obj is None:
                continue
            obj.hide_viewport = False
            obj.hide_render = False
            eval_obj = obj.evaluated_get(depsgraph)
            M = eval_obj.matrix_world
            for corner in eval_obj.bound_box:
                w = M @ Vector(corner)
                for i in range(3):
                    if w[i] < bmin[i]:
                        bmin[i] = w[i]
                    if w[i] > bmax[i]:
                        bmax[i] = w[i]
                found = True

        if not found:
            self.report({'WARNING'}, "Mars Globe not found")
            return {'CANCELLED'}

        center = Vector(((bmin[0] + bmax[0]) * 0.5, (bmin[1] + bmax[1]) * 0.5, (bmin[2] + bmax[2]) * 0.5))
        size = max(bmax[0] - bmin[0], bmax[1] - bmin[1], bmax[2] - bmin[2], 1000.0)

        gcam = bpy.data.objects.get("MARS_NEW_Globe_Camera")
        if gcam is not None:
            context.scene.camera = gcam
            forward = (gcam.matrix_world.to_quaternion() @ Vector((0.0, 0.0, -1.0))).normalized()
            rot_quat = forward.to_track_quat('-Z', 'Y')
        else:
            rot_quat = Quaternion((0.7071068, 0.7071068, 0.0, 0.0))

        for window in context.window_manager.windows:
            for area in window.screen.areas:
                if area.type != 'VIEW_3D':
                    continue
                for space in area.spaces:
                    if space.type != 'VIEW_3D':
                        continue
                    space.clip_start = 100.0
                    space.clip_end = 10000000.0
                    space.shading.type = 'MATERIAL'
                    space.shading.use_scene_lights = True
                    space.shading.use_scene_world = True
                    space.overlay.show_relationship_lines = False
                    rv3d = space.region_3d
                    if rv3d is not None:
                        rv3d.view_perspective = 'PERSP'
                        rv3d.view_location = center
                        rv3d.view_rotation = rot_quat
                        rv3d.view_distance = size * 1.45

        context.view_layer.update()
        _redraw(context)
        self.report({'INFO'}, "Key 2: Mars Planet & Starfield from Space")
        return {'FINISHED'}


class VIEW3D_OT_mars_agriculture_zone(bpy.types.Operator):
    """Key 3: Frame the Mars Agriculture, Hydroponics & Aeroponics Sustainability Complex"""
    bl_idname = "view3d.mars_agriculture_zone"
    bl_label = "Mars Agriculture & Greenhouse Zone (Key 3)"

    def execute(self, context):
        context.scene.frame_set(76)
        _set_orbit_objects_visible(False)
        c = bpy.data.objects.get(CTL)
        cur_time = c.get("Time_Of_Day", "DAY") if c else "DAY"
        apply_martian_time_of_day(cur_time, force_space_stars=False)

        eye = Vector((-13.2, 6.5, 3.6))
        target = Vector((-22.5, 20.5, 1.85))
        _set_viewport_camera_to_pose(context, eye, target, clip_start=0.1, clip_end=20000.0)
        context.view_layer.update()
        _redraw(context)
        self.report({'INFO'}, "Key 3: Mars Agriculture & Hydroponics Complex")
        return {'FINISHED'}


class VIEW3D_OT_mars_habitat_zone(bpy.types.Operator):
    """Key 4: Frame the Astronaut Habitat Pods, Heavy Exploration Vehicle & Perseverance Rover"""
    bl_idname = "view3d.mars_habitat_zone"
    bl_label = "Mars Habitat & Rover Fleet (Key 4)"

    def execute(self, context):
        context.scene.frame_set(76)
        _set_orbit_objects_visible(False)
        c = bpy.data.objects.get(CTL)
        cur_time = c.get("Time_Of_Day", "DAY") if c else "DAY"
        apply_martian_time_of_day(cur_time, force_space_stars=False)

        eye = Vector((11.5, -2.5, 3.8))
        target = Vector((8.5, 18.5, 2.1))
        _set_viewport_camera_to_pose(context, eye, target, clip_start=0.1, clip_end=20000.0)
        context.view_layer.update()
        _redraw(context)
        self.report({'INFO'}, "Key 4: Mars Habitat Base & Rover Fleet")
        return {'FINISHED'}


class VIEW3D_OT_mars_fleet_zone(bpy.types.Operator):
    """Key 6: Frame Historical Mars Exploration Fleet (Ingenuity, Perseverance, Curiosity, Spirit/Opp, Viking, InSight)"""
    bl_idname = "view3d.mars_fleet_zone"
    bl_label = "Historical Mars Fleet Showcase (Key 6)"

    def execute(self, context):
        context.scene.frame_set(76)
        _set_orbit_objects_visible(False)
        c = bpy.data.objects.get(CTL)
        cur_time = c.get("Time_Of_Day", "DAY") if c else "DAY"
        apply_martian_time_of_day(cur_time, force_space_stars=False)

        eye = Vector((16.5, -9.5, 5.2))
        target = Vector((23.5, 6.5, 0.8))
        _set_viewport_camera_to_pose(context, eye, target, clip_start=0.1, clip_end=20000.0)
        context.view_layer.update()
        _redraw(context)
        self.report({'INFO'}, "Key 6: Historical Mars Exploration Fleet")
        return {'FINISHED'}


class VIEW3D_OT_mars_cycle_time(bpy.types.Operator):
    """Key 5: Cycle Martian Time of Day (Day -> Sunset -> Twilight -> Night -> Sunrise)"""
    bl_idname = "view3d.mars_cycle_time"
    bl_label = "Cycle Martian Day/Night (Key 5)"
    mode: bpy.props.StringProperty(default="")

    def execute(self, context):
        c = bpy.data.objects.get(CTL)
        cur = c.get("Time_Of_Day", "DAY") if c else "DAY"
        if self.mode and self.mode in TIME_PRESETS:
            nxt = self.mode
        else:
            idx = TIME_PRESETS.index(cur) if cur in TIME_PRESETS else 0
            nxt = TIME_PRESETS[(idx + 1) % len(TIME_PRESETS)]
        apply_martian_time_of_day(nxt, force_space_stars=False)
        context.view_layer.update()
        _redraw(context)
        self.report({'INFO'}, f"Martian Time of Day: {nxt}")
        return {'FINISHED'}


class MARS_OT_toggle_layer(bpy.types.Operator):
    bl_idname = "mars.toggle_layer"
    bl_label = "Toggle layer"
    bl_options = {'INTERNAL'}
    prop: bpy.props.StringProperty()

    def execute(self, context):
        c = bpy.data.objects.get(CTL)
        if c is None:
            return {'CANCELLED'}
        new_val = 0 if c.get(self.prop, 0) else 1
        c[self.prop] = new_val
        col_layers = bpy.data.collections.get("NEW_Terrain_Layers")
        if col_layers:
            any_on = any(c.get(p, 0) for p, _ in LAYERS)
            col_layers.hide_viewport = not any_on
            col_layers.hide_render = not any_on
            obj_name = "NEW_" + self.prop
            lo = bpy.data.objects.get(obj_name)
            if lo:
                lo.hide_viewport = not bool(new_val)
                lo.hide_render = not bool(new_val)
        c.update_tag()
        context.view_layer.update()
        _redraw(context)
        return {'FINISHED'}


class MARS_OT_layers_off(bpy.types.Operator):
    bl_idname = "mars.layers_off"
    bl_label = "All layers off"
    bl_options = {'INTERNAL'}

    def execute(self, context):
        c = bpy.data.objects.get(CTL)
        if c is None:
            return {'CANCELLED'}
        for p, _ in LAYERS:
            c[p] = 0
            lo = bpy.data.objects.get("NEW_" + p)
            if lo:
                lo.hide_viewport = True
                lo.hide_render = True
        col_layers = bpy.data.collections.get("NEW_Terrain_Layers")
        if col_layers:
            col_layers.hide_viewport = True
            col_layers.hide_render = True
        c.update_tag()
        context.view_layer.update()
        _redraw(context)
        return {'FINISHED'}


class MARS_OT_set_camera(bpy.types.Operator):
    bl_idname = "mars.set_camera"
    bl_label = "Set scene camera"
    bl_options = {'INTERNAL'}
    cam: bpy.props.StringProperty()

    def execute(self, context):
        o = bpy.data.objects.get(self.cam)
        if o is None:
            return {'CANCELLED'}
        context.scene.camera = o
        _redraw(context)
        return {'FINISHED'}


class MARS_OT_explore_preset(bpy.types.Operator):
    bl_idname = "mars.explore_preset"
    bl_label = "Set exploration area"
    bl_options = {'INTERNAL'}
    lat: bpy.props.FloatProperty()
    lon: bpy.props.FloatProperty()

    def execute(self, context):
        c = bpy.data.objects.get(CTL)
        if c is None:
            return {'CANCELLED'}
        c["Explore_Lat"] = self.lat
        c["Explore_Lon"] = self.lon
        c.update_tag()
        context.view_layer.update()
        _redraw(context)
        return {'FINISHED'}


class MARS_PT_dashboard(bpy.types.Panel):
    bl_label = "MARS SENTINEL — Mission Control"
    bl_idname = "MARS_PT_dashboard"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Mars"

    def draw(self, context):
        lay = self.layout
        c = bpy.data.objects.get(CTL)

        # Quick Camera & Zone Teleport Bar (Keys 1-4, 6)
        nav = lay.box()
        nav.label(text="Mission Zones & Hotkeys", icon='VIEW_CAMERA')
        col = nav.column(align=True)
        col.operator("view3d.front_on_models", text="1: Surface Base & Crew", icon='USER')
        col.operator("view3d.mars_planet_outside", text="2: Mars Planet & Starfield", icon='WORLD')
        col.operator("view3d.mars_agriculture_zone", text="3: Agriculture & Hydroponics", icon='SHADING_RENDERED')
        col.operator("view3d.mars_habitat_zone", text="4: Habitat & Rover Fleet", icon='HOME')
        col.operator("view3d.mars_fleet_zone", text="6: Historical Fleet Showcase", icon='SORTSIZE')

        # Day & Night Cycle Control (Key 5)
        tod_box = lay.box()
        cur_tod = c.get("Time_Of_Day", "DAY") if c else "DAY"
        tod_box.label(text=f"Martian Solar Cycle: {cur_tod}", icon='LIGHT_SUN')
        row = tod_box.row(align=True)
        for m_name, m_label in (("SUNRISE", "Sunrise"), ("DAY", "Day"), ("SUNSET", "Sunset"), ("TWILIGHT", "Twilight"), ("NIGHT", "Night")):
            op = row.operator("view3d.mars_cycle_time", text=m_label, depress=(cur_tod == m_name))
            op.mode = m_name

        if c is None:
            return

        # Phase 1 - Mars Globe Controls
        box = lay.box()
        box.label(text="Orbit & Globe Controls", icon='WORLD')
        box.prop(c, '["Globe_Zoom"]', text="Zoom", slider=True)
        box.prop(c, '["Globe_Spin_Speed"]', text="Spin Speed")
        box.prop(c, '["Globe_Rotation_Offset"]', text="Rotation")
        grid = box.grid_flow(columns=2, align=True)
        for pname, plat, plon in PRESETS:
            pop = grid.operator("mars.explore_preset", text=pname)
            pop.lat = plat
            pop.lon = plon

        # Phase 3 - Scientific Terrain Layers
        box = lay.box()
        box.label(text="NASA Scientific Overlays", icon='RENDERLAYERS')
        for p, label in LAYERS:
            on = bool(c.get(p, 0))
            op = box.operator(
                "mars.toggle_layer",
                text=label,
                depress=on,
                icon='CHECKBOX_HLT' if on else 'CHECKBOX_DEHLT',
            )
            op.prop = p
        box.operator("mars.layers_off", text="All Overlays Off", icon='X')


CLASSES = (
    VIEW3D_OT_front_on_models,
    VIEW3D_OT_mars_planet_outside,
    VIEW3D_OT_mars_agriculture_zone,
    VIEW3D_OT_mars_habitat_zone,
    VIEW3D_OT_mars_cycle_time,
    VIEW3D_OT_mars_fleet_zone,
    MARS_OT_toggle_layer,
    MARS_OT_layers_off,
    MARS_OT_set_camera,
    MARS_OT_explore_preset,
    MARS_PT_dashboard,
)

addon_keymaps = []


def register():
    for cls in CLASSES:
        try:
            bpy.utils.unregister_class(cls)
        except Exception:
            pass
        bpy.utils.register_class(cls)

    wm = bpy.context.window_manager
    if wm and wm.keyconfigs.addon:
        km = wm.keyconfigs.addon.keymaps.new(name='3D View', space_type='VIEW_3D')
        for key_id, op_id in (
            ('ONE', "view3d.front_on_models"),
            ('NUMPAD_1', "view3d.front_on_models"),
            ('TWO', "view3d.mars_planet_outside"),
            ('NUMPAD_2', "view3d.mars_planet_outside"),
            ('THREE', "view3d.mars_agriculture_zone"),
            ('NUMPAD_3', "view3d.mars_agriculture_zone"),
            ('FOUR', "view3d.mars_habitat_zone"),
            ('NUMPAD_4', "view3d.mars_habitat_zone"),
            ('FIVE', "view3d.mars_cycle_time"),
            ('NUMPAD_5', "view3d.mars_cycle_time"),
            ('SIX', "view3d.mars_fleet_zone"),
            ('NUMPAD_6', "view3d.mars_fleet_zone"),
        ):
            kmi = km.keymap_items.new(op_id, key_id, 'PRESS')
            addon_keymaps.append((km, kmi))


def unregister():
    for km, kmi in addon_keymaps:
        try:
            km.keymap_items.remove(kmi)
        except Exception:
            pass
    addon_keymaps.clear()
    for cls in reversed(CLASSES):
        try:
            bpy.utils.unregister_class(cls)
        except Exception:
            pass


if __name__ == "__main__":
    register()
