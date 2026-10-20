"""Build a Blender animation and preview for the carrier-deck crew member."""

import math
import os
import shutil
import subprocess
import bpy
from mathutils import Vector


PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODEL = os.path.join(PROJECT, "Assets", "Soldado", "models", "Soldier_demo.FBX")
OUTPUT_DIR = os.path.join(PROJECT, "Tools", "Blender", "output", "TrabalhadorConves")
BLEND_PATH = os.path.join(OUTPUT_DIR, "TrabalhadorConves_Animacao.blend")
FBX_PATH = os.path.join(PROJECT, "Assets", "Animations", "TrabalhadorConves", "TrabalhadorConves_Actions.fbx")
GIF_PATH = os.path.join(OUTPUT_DIR, "TrabalhadorConves_Preview.gif")
MP4_PATH = os.path.join(OUTPUT_DIR, "TrabalhadorConves_Preview.mp4")
FRAMES_DIR = os.path.join(OUTPUT_DIR, "animation_frames")
STILLS_DIR = os.path.join(OUTPUT_DIR, "stills")


def look_at(obj, point):
    direction = Vector(point) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def material(name, color, metallic=0.0, roughness=0.65):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    if shader is None:
        shader = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    output = mat.node_tree.nodes.get("Material Output")
    if output is None:
        output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
    if not output.inputs["Surface"].is_linked:
        mat.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


def cube(name, location, scale, mat, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("Soft machined edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
    return obj


def mesh_object(name, vertices, faces, mat):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def create_deck():
    deck = material("Flight deck | nonskid charcoal", (0.105, 0.135, 0.155), 0.22, 0.86)
    edge = material("Deck edge steel", (0.23, 0.29, 0.32), 0.48, 0.5)
    yellow = material("Safety yellow", (0.98, 0.61, 0.055), 0.05, 0.5)
    white = material("Catapult lane white", (0.79, 0.83, 0.8), 0.0, 0.62)
    black = material("Warning stripe black", (0.025, 0.035, 0.04), 0.12, 0.6)
    tire = material("Aircraft tire", (0.018, 0.021, 0.023), 0.0, 0.9)
    canopy = material("Canopy glass", (0.045, 0.15, 0.21), 0.62, 0.22)
    aircraft = material("Carrier aircraft | slate", (0.24, 0.31, 0.35), 0.46, 0.56)

    cube("Flight deck", (0.0, 0.0, -0.12), (11.0, 12.0, 0.24), deck, 0.08)
    cube("Deck edge lip", (0.0, 5.95, -0.02), (11.0, 0.12, 0.22), edge, 0.03)
    for x in (-0.55, 0.55):
        cube("Catapult track", (x, 0.85, 0.008), (0.055, 8.5, 0.015), edge, 0.008)
    cube("Catapult center guide", (0.0, 0.8, 0.012), (0.018, 8.5, 0.012), white, 0.004)
    for y in (-3.4, -2.25, -1.1, 0.05, 1.2, 2.35, 3.5, 4.65):
        cube("Deck tie-down bar", (2.25, y, 0.012), (0.38, 0.035, 0.018), edge, 0.006)
        cube("Deck tie-down bar", (-2.25, y, 0.012), (0.38, 0.035, 0.018), edge, 0.006)
    for side in (-1, 1):
        for i in range(6):
            cube("Hazard chevron", (side * 1.15, -1.1 + i * 0.18, 0.015), (0.12, 0.08, 0.018), yellow if i % 2 == 0 else black, 0.01)
    cube("Safety boundary", (3.15, 0.5, 0.015), (0.045, 8.8, 0.02), yellow, 0.008)
    cube("Safety boundary", (-3.15, 0.5, 0.015), (0.045, 8.8, 0.02), yellow, 0.008)

    # A compact parked jet silhouette establishes the launch-deck context.
    cube("Jet fuselage", (0.0, 3.95, 0.43), (0.38, 2.55, 0.34), aircraft, 0.12)
    cube("Jet nose", (0.0, 2.55, 0.43), (0.27, 0.58, 0.25), aircraft, 0.11)
    cube("Jet canopy", (0.0, 3.13, 0.63), (0.23, 0.58, 0.2), canopy, 0.08)
    mesh_object("Jet wings", [
        (-2.05, 4.18, 0.43), (2.05, 4.18, 0.43),
        (0.52, 4.72, 0.43), (-0.52, 4.72, 0.43),
        (-2.05, 4.18, 0.31), (2.05, 4.18, 0.31),
        (0.52, 4.72, 0.31), (-0.52, 4.72, 0.31),
    ], [(0, 1, 2, 3), (7, 6, 5, 4)], aircraft)
    mesh_object("Jet tailplane", [
        (-0.83, 5.0, 0.53), (0.83, 5.0, 0.53),
        (0.3, 5.28, 0.53), (-0.3, 5.28, 0.53),
        (-0.83, 5.0, 0.44), (0.83, 5.0, 0.44),
        (0.3, 5.28, 0.44), (-0.3, 5.28, 0.44),
    ], [(0, 1, 2, 3), (7, 6, 5, 4)], aircraft)
    cube("Jet vertical tail", (0.0, 4.95, 0.78), (0.09, 0.55, 0.75), aircraft, 0.035)
    for x in (-0.82, 0.82):
        cube("Jet landing gear", (x, 4.12, 0.15), (0.055, 0.12, 0.32), edge, 0.018)
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.11, depth=0.07, location=(x, 4.12, 0.075), rotation=(0, math.pi / 2, 0))
        wheel = bpy.context.object
        wheel.name = "Jet tire"
        wheel.data.materials.append(tire)


def create_lighting_and_camera():
    world = bpy.context.scene.world
    world.color = (0.11, 0.14, 0.18)
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.095, 0.13, 0.18, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45

    bpy.ops.object.light_add(type="AREA", location=(1.5, -3.5, 8.0))
    key = bpy.context.object
    key.name = "Soft daylight"
    key.data.energy = 1500
    key.data.shape = "DISK"
    key.data.size = 7.0
    look_at(key, (0.0, 0.5, 0.8))
    bpy.ops.object.light_add(type="AREA", location=(-4.0, 2.5, 4.0))
    fill = bpy.context.object
    fill.name = "Cool deck fill"
    fill.data.energy = 850
    fill.data.color = (0.66, 0.8, 1.0)
    fill.data.size = 5.0
    look_at(fill, (0.0, 0.8, 1.0))

    bpy.ops.object.camera_add(location=(4.6, -7.8, 4.0))
    camera = bpy.context.object
    camera.name = "Deck crew camera"
    camera.data.lens = 52
    look_at(camera, (0.0, 0.9, 1.15))
    bpy.context.scene.camera = camera


def make_character_opaque(meshes):
    for mesh in meshes:
        for mat in mesh.data.materials:
            if mat is None or not mat.use_nodes:
                continue
            shader = next((node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
            if shader is None:
                continue
            alpha = shader.inputs.get("Alpha")
            if alpha is not None:
                for link in list(alpha.links):
                    mat.node_tree.links.remove(link)
                alpha.default_value = 1.0
            mat.surface_render_method = "DITHERED"


def insert_track(armature, action, bone_name, axis, points):
    pose_bone = armature.pose.bones.get(bone_name)
    if pose_bone is None:
        raise RuntimeError("Required rig bone is missing: " + bone_name)
    pose_bone.rotation_mode = "XYZ"
    channel = "XYZ".index(axis)
    for frame, degrees in points:
        pose_bone.rotation_euler[channel] = math.radians(degrees)
        pose_bone.keyframe_insert(data_path="rotation_euler", index=channel, frame=frame, group=bone_name)


def create_action(armature, name, duration, tracks, root_y=None, root_z=None, heading=None, bone_locations=None):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    armature.animation_data.action = action
    for bone_name, axis, points in tracks:
        insert_track(armature, action, bone_name, axis, points)

    if bone_locations:
        for bone_name, axis, points in bone_locations:
            pose_bone = armature.pose.bones.get(bone_name)
            if pose_bone is None:
                raise RuntimeError("Required rig bone is missing: " + bone_name)
            channel = "XYZ".index(axis)
            for frame, value in points:
                pose_bone.location[channel] = value
                pose_bone.keyframe_insert(data_path="location", index=channel, frame=frame, group=bone_name)

    if root_y is not None:
        for frame, value in root_y:
            armature.location.y = value
            armature.keyframe_insert(data_path="location", index=1, frame=frame, group="Root motion")
    if root_z is not None:
        for frame, value in root_z:
            armature.location.z = value
            armature.keyframe_insert(data_path="location", index=2, frame=frame, group="Root motion")
    if heading is not None:
        for frame, degrees in heading:
            armature.rotation_euler.z = math.radians(-90.0 + degrees)
            armature.keyframe_insert(data_path="rotation_euler", index=2, frame=frame, group="Root heading")

    action.frame_end = duration
    return action


def build_animations(armature):
    armature.animation_data_create()
    actions = {}
    actions["Walk_Deck"] = create_action(armature, "Walk_Deck", 43, [
        ("Bip001 L Thigh", "Y", [(1, 0), (11, 20), (22, 0), (32, -20), (43, 0)]),
        ("Bip001 R Thigh", "Y", [(1, 0), (11, -20), (22, 0), (32, 20), (43, 0)]),
        ("Bip001 L Calf", "Y", [(1, 0), (11, -20), (22, 0), (32, 5), (43, 0)]),
        ("Bip001 R Calf", "Y", [(1, 0), (11, 5), (22, 0), (32, -20), (43, 0)]),
        ("Bip001 L UpperArm", "Y", [(1, 0), (11, -18), (22, 0), (32, 18), (43, 0)]),
        ("Bip001 R UpperArm", "Y", [(1, 0), (11, 18), (22, 0), (32, -18), (43, 0)]),
        ("Bip001 L Forearm", "Z", [(1, 12), (11, 18), (22, 12), (32, 5), (43, 12)]),
        ("Bip001 R Forearm", "Z", [(1, 12), (11, 5), (22, 12), (32, 18), (43, 12)]),
    ], root_y=[(1, -0.72), (43, 0.48)], root_z=[(1, 1.286), (43, 1.286)])

    kneel = [
        ("Bip001 L Thigh", "Z", [(1, 0), (13, -60), (25, -80), (36, -80)]),
        ("Bip001 L Calf", "Z", [(1, 0), (13, 60), (25, 80), (36, 80)]),
        ("Bip001 R Thigh", "Y", [(1, 0), (13, 0), (25, 0), (36, 0)]),
        ("Bip001 R Calf", "Y", [(1, 0), (13, 0), (25, 0), (36, 0)]),
        ("Bip001 Spine1", "Y", [(1, 0), (13, -12), (25, -18), (36, -18)]),
    ]
    actions["Kneel_Ready"] = create_action(armature, "Kneel_Ready", 36, kneel,
        root_y=[(1, 0.48), (36, 0.48)], root_z=[(1, 1.286), (13, 0.6), (36, 0.6)],
        bone_locations=[("Bip001 L Foot", "Z", [(1, 0), (13, 27.4), (36, 27.4)]),
                        ("Bip001 R Foot", "Z", [(1, 0), (13, 27.4), (36, 27.4)])])

    actions["Check_Catapult"] = create_action(armature, "Check_Catapult", 32, [
        ("Bip001 Spine1", "Y", [(1, -10), (10, -23), (22, -17), (32, -10)]),
        ("Bip001 Head", "Y", [(1, 0), (10, 14), (22, 8), (32, 0)]),
        ("Bip001 R UpperArm", "Y", [(1, 10), (10, 34), (22, 42), (32, 10)]),
        ("Bip001 R Forearm", "Z", [(1, 18), (10, 55), (22, 62), (32, 18)]),
        ("Bip001 L UpperArm", "Y", [(1, -8), (10, -20), (22, -17), (32, -8)]),
    ], root_y=[(1, 0.48), (32, 0.48)], root_z=[(1, 1.286), (32, 1.286)])

    actions["Kneel_LaunchSignal"] = create_action(armature, "Kneel_LaunchSignal", 42, [
        ("Bip001 L Thigh", "Z", [(1, -60), (10, -80), (42, -80)]),
        ("Bip001 L Calf", "Z", [(1, 60), (10, 80), (42, 80)]),
        ("Bip001 R Thigh", "Y", [(1, 0), (10, 0), (42, 0)]),
        ("Bip001 R Calf", "Y", [(1, 0), (10, 0), (42, 0)]),
        ("Bip001 Spine1", "Y", [(1, -10), (10, -7), (42, -7)]),
        ("Bip001 Head", "Y", [(1, 0), (10, 8), (42, 8)]),
        ("Bip001 R UpperArm", "Y", [(1, 10), (10, 74), (22, 86), (31, 74), (42, 74)]),
        ("Bip001 R Forearm", "Z", [(1, 18), (10, 40), (22, -18), (31, 40), (42, 40)]),
        ("Bip001 R Hand", "Z", [(1, 0), (10, -22), (22, 25), (31, -22), (42, -22)]),
    ], root_y=[(1, 0.48), (42, 0.48)], root_z=[(1, 1.286), (10, 0.6), (42, 0.6)],
        bone_locations=[("Bip001 L Foot", "Z", [(1, 0), (10, 27.4), (42, 27.4)]),
                        ("Bip001 R Foot", "Z", [(1, 0), (10, 27.4), (42, 27.4)])])

    actions["Marshal_Stop"] = create_action(armature, "Marshal_Stop", 30, [
        ("Bip001 L UpperArm", "Y", [(1, 0), (9, -62), (30, -62)]),
        ("Bip001 R UpperArm", "Y", [(1, 0), (9, 62), (30, 62)]),
        ("Bip001 L Forearm", "Z", [(1, 12), (9, 24), (30, 24)]),
        ("Bip001 R Forearm", "Z", [(1, 12), (9, 24), (30, 24)]),
        ("Bip001 Head", "Y", [(1, 0), (9, 5), (30, 5)]),
    ], root_y=[(1, 0.48), (30, 0.48)], root_z=[(1, 1.286), (30, 1.286)])

    actions["Marshal_TurnLeft"] = create_action(armature, "Marshal_TurnLeft", 30, [
        ("Bip001 Head", "Y", [(1, 0), (9, -12), (16, -12), (30, 0)]),
        ("Bip001 Spine1", "Y", [(1, 0), (9, -8), (16, -8), (30, 0)]),
    ], root_y=[(1, 0.48), (30, 0.48)], root_z=[(1, 1.286), (30, 1.286)], heading=[(1, 0), (11, -55), (19, -55), (30, 0)])

    actions["Marshal_TurnRight"] = create_action(armature, "Marshal_TurnRight", 30, [
        ("Bip001 Head", "Y", [(1, 0), (9, 12), (16, 12), (30, 0)]),
        ("Bip001 Spine1", "Y", [(1, 0), (9, 8), (16, 8), (30, 0)]),
    ], root_y=[(1, 0.48), (30, 0.48)], root_z=[(1, 1.286), (30, 1.286)], heading=[(1, 0), (11, 55), (19, 55), (30, 0)])

    actions["Wave_Clear"] = create_action(armature, "Wave_Clear", 42, [
        ("Bip001 R UpperArm", "Y", [(1, 0), (10, 74), (42, 74)]),
        ("Bip001 R Forearm", "Z", [(1, 12), (10, 48), (42, 48)]),
        ("Bip001 R Hand", "Z", [(1, 0), (10, -20), (19, 20), (28, -20), (37, 20), (42, 0)]),
        ("Bip001 Head", "Y", [(1, 0), (10, 7), (42, 7)]),
    ], root_y=[(1, 0.48), (42, 0.48)], root_z=[(1, 1.286), (42, 1.286)])

    armature.animation_data.action = None
    track = armature.animation_data.nla_tracks.new()
    track.name = "Flight deck launch sequence"
    order = ["Walk_Deck", "Kneel_Ready", "Check_Catapult", "Kneel_LaunchSignal",
             "Marshal_Stop", "Marshal_TurnLeft", "Marshal_TurnRight", "Wave_Clear"]
    cursor = 1
    starts = {}
    for name in order:
        action = actions[name]
        starts[name] = cursor
        strip = track.strips.new(name, cursor, action)
        strip.action_frame_start = action.frame_range[0]
        strip.action_frame_end = action.frame_range[1]
        strip.blend_type = "REPLACE"
        strip.extrapolation = "NOTHING"
        strip.blend_in = 3
        strip.blend_out = 3
        cursor += int(action.frame_range[1] - action.frame_range[0]) + 1
    return actions, starts, cursor - 1


def configure_scene(frame_end):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 16
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 75
    scene.render.fps = 24
    scene.frame_start = 1
    scene.frame_end = frame_end
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.render.filepath = os.path.join(FRAMES_DIR, "deck_")
    scene.render.image_settings.file_format = "PNG"
    scene.render.use_file_extension = True
    scene.render.image_settings.color_depth = "8"

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.region_3d.view_perspective = "CAMERA"
                area.spaces.active.shading.type = "MATERIAL"


def export_fbx(armature, mesh_objects):
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for obj in mesh_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        path_mode="AUTO",
    )


def render_stills(starts):
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "PNG"
    scene.render.resolution_percentage = 75
    os.makedirs(STILLS_DIR, exist_ok=True)
    for name, start in starts.items():
        # Mid-pose reads clearly and keeps one still for every authored action.
        frame = start + max(1, int((bpy.data.actions[name].frame_range[1] - 1) * 0.58))
        scene.frame_set(frame)
        scene.render.filepath = os.path.join(STILLS_DIR, name + ".png")
        bpy.ops.render.render(write_still=True)


def encode_preview():
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("FFMPEG_NOT_FOUND; PNG frames are available in", FRAMES_DIR)
        return

    frame_paths = [
        os.path.join(FRAMES_DIR, name)
        for name in sorted(os.listdir(FRAMES_DIR))
        if name.lower().endswith(".png")
    ]
    if not frame_paths:
        raise RuntimeError("No rendered animation frames found in " + FRAMES_DIR)

    manifest_lines = []
    for path in frame_paths:
        normalized = os.path.abspath(path).replace("\\", "/")
        manifest_lines.extend(("file '{}'".format(normalized), "duration 0.083333333"))
    last_path = os.path.abspath(frame_paths[-1]).replace("\\", "/")
    manifest_lines.append("file '{}'".format(last_path))
    manifest = ("\n".join(manifest_lines) + "\n").encode("utf-8")

    subprocess.run([
        ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
        "-protocol_whitelist", "file,pipe,crypto,data",
        "-f", "concat", "-safe", "0", "-i", "pipe:0",
        "-c:v", "libx264", "-crf", "20", "-preset", "medium",
        "-pix_fmt", "yuv420p", "-fps_mode", "vfr", "-movflags", "+faststart",
        MP4_PATH,
    ], input=manifest, check=True)

    subprocess.run([
        ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", MP4_PATH,
        "-vf", "fps=12,scale=704:-1:flags=lanczos,split[s0][s1];"
              "[s0]palettegen=stats_mode=diff[p];[s1][p]paletteuse=dither=bayer",
        "-loop", "0", GIF_PATH,
    ], check=True)


def main():
    if not os.path.isfile(MODEL):
        raise FileNotFoundError("Character source FBX not found: " + MODEL)
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    bpy.ops.import_scene.fbx(filepath=MODEL)
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE" and obj.name == "Bip001"), None)
    if armature is None:
        armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError("The imported character did not contain an armature")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.parent == armature]
    if not meshes:
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    make_character_opaque(meshes)
    required = ["Bip001 L Thigh", "Bip001 R Thigh", "Bip001 L Calf", "Bip001 R Calf",
                "Bip001 L UpperArm", "Bip001 R UpperArm", "Bip001 R Hand"]
    missing = [name for name in required if name not in armature.pose.bones]
    if missing:
        raise RuntimeError("The character rig is missing bones: " + ", ".join(missing))

    create_deck()
    create_lighting_and_camera()
    actions, starts, end_frame = build_animations(armature)
    configure_scene(end_frame)

    # Save the Blender project with the kneeling launch signal selected.
    signal_start = starts["Kneel_LaunchSignal"]
    bpy.context.scene.frame_set(signal_start + 22)
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    export_fbx(armature, meshes)
    render_stills(starts)

    scene = bpy.context.scene
    scene.render.image_settings.file_format = "PNG"
    scene.render.resolution_percentage = 55
    scene.frame_step = 2
    scene.render.filepath = os.path.join(FRAMES_DIR, "deck_")
    os.makedirs(FRAMES_DIR, exist_ok=True)
    scene.frame_set(1)
    bpy.ops.render.render(animation=True)
    encode_preview()
    print("DECK_CREW_DONE", BLEND_PATH, FBX_PATH, MP4_PATH, GIF_PATH, end_frame)


if __name__ == "__main__":
    main()
