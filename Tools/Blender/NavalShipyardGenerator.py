"""Generate the modular, metre-scale naval shipyard game asset for Hegemonia Global.

Run from the Unity project root with:
  blender --background --factory-startup --python Tools/Blender/NavalShipyardGenerator.py

Outputs are written to the Unity project and this script's sibling folder. The
currently open Blender document is never loaded or overwritten.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from NavalShipyardTextureGenerator import generate_textures


PROJECT_ROOT = Path(__file__).resolve().parents[2]
MODELS_DIR = PROJECT_ROOT / "Assets/Game/Environment/NavalShipyard/Models"
PREVIEW_DIR = PROJECT_ROOT / "Assets/Game/Environment/NavalShipyard/Previews"
TEXTURE_DIR = PROJECT_ROOT / "Assets/Game/Environment/NavalShipyard/Textures"
BLEND_OUTPUT = Path(__file__).resolve().parent / "NavalShipyard_Blockout.blend"
FBX_OUTPUT = MODELS_DIR / "NavalShipyard_Blockout.fbx"


PALETTE = {
    "MAT_Concrete_Naval": ((0.62, 0.63, 0.60, 1.0), 0.0, 0.82),
    "MAT_Steel_Grey": ((0.27, 0.31, 0.33, 1.0), 0.62, 0.42),
    "MAT_GalvanizedMetal": ((0.55, 0.62, 0.64, 1.0), 0.68, 0.32),
    "MAT_RoofMetal": ((0.71, 0.73, 0.73, 1.0), 0.36, 0.52),
    "MAT_SafetyYellow": ((1.0, 0.74, 0.06, 1.0), 0.12, 0.48),
    "MAT_Asphalt": ((0.10, 0.11, 0.12, 1.0), 0.0, 0.94),
    "MAT_DarkSteel": ((0.055, 0.075, 0.088, 1.0), 0.55, 0.52),
    "MAT_Glass": ((0.12, 0.30, 0.38, 1.0), 0.22, 0.23),
    "MAT_Water": ((0.025, 0.16, 0.21, 1.0), 0.28, 0.19),
    "MAT_Concrete_Light": ((0.73, 0.74, 0.70, 1.0), 0.0, 0.86),
    "MAT_Green": ((0.25, 0.33, 0.18, 1.0), 0.0, 0.9),
    "MAT_Foliage_Olive": ((0.31, 0.40, 0.16, 1.0), 0.0, 0.94),
    "MAT_White_Marking": ((0.82, 0.84, 0.80, 1.0), 0.0, 0.7),
    "MAT_Red_Safety": ((0.58, 0.09, 0.06, 1.0), 0.08, 0.6),
}


COLLECTIONS = {
    "ROOT": "MilitaryNavalShipyard",
    "Ground": "Concrete_Base",
    "Dock": "DryDock",
    "MainBuilding": "Main_Hangar",
    "Maintenance": "Maintenance_Hangars",
    "Workshop": "Workshops",
    "Admin": "Administration",
    "Crane": "Cranes",
    "Pier": "Piers",
    "Storage": "Storage",
    "Utility": "Utilities",
    "Pipe": "Pipes",
    "Light": "Lighting",
    "Security": "Security",
    "Road": "Roads",
    "Detail": "Details",
    "Environment": "Environment",
}

MATERIALS = {}
GROUPS = {}
OBJECTS = []
TEXTURE_FILES = {}

UV_TILE_METERS = {
    "MAT_Concrete_Naval": 8.0,
    "MAT_Concrete_Light": 8.0,
    "MAT_Steel_Grey": 3.0,
    "MAT_GalvanizedMetal": 2.5,
    "MAT_RoofMetal": 8.0,
    "MAT_SafetyYellow": 2.0,
    "MAT_Asphalt": 6.0,
    "MAT_DarkSteel": 2.4,
    "MAT_Glass": 3.0,
    "MAT_Water": 22.0,
    "MAT_Green": 6.0,
    "MAT_Foliage_Olive": 3.0,
    "MAT_White_Marking": 2.0,
    "MAT_Red_Safety": 2.0,
}


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.collections):
        if block.users == 0:
            bpy.data.collections.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)


def make_material(name):
    if name in MATERIALS:
        return MATERIALS[name]
    rgba, metallic, roughness = PALETTE[name]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = rgba
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    albedo_path, normal_path = TEXTURE_FILES[name]
    albedo_node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    albedo_node.label = "Tileable albedo"
    albedo_node.image = bpy.data.images.load(str(albedo_path), check_existing=True)
    albedo_node.extension = "REPEAT"
    mat.node_tree.links.new(albedo_node.outputs["Color"], shader.inputs["Base Color"])
    if normal_path is not None:
        normal_texture = mat.node_tree.nodes.new("ShaderNodeTexImage")
        normal_texture.label = "Subtle surface normal"
        normal_texture.image = bpy.data.images.load(str(normal_path), check_existing=True)
        normal_texture.image.colorspace_settings.name = "Non-Color"
        normal_texture.extension = "REPEAT"
        normal_map = mat.node_tree.nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = 0.38
        mat.node_tree.links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
        mat.node_tree.links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    mat["uv_tile_meters"] = UV_TILE_METERS[name]
    MATERIALS[name] = mat
    return mat


def create_hierarchy():
    root_collection = bpy.data.collections.new(COLLECTIONS["ROOT"])
    bpy.context.scene.collection.children.link(root_collection)
    root = bpy.data.objects.new(COLLECTIONS["ROOT"], None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 4
    root_collection.objects.link(root)
    for key, label in COLLECTIONS.items():
        if key in ("ROOT",):
            continue
        collection = bpy.data.collections.new(label)
        root_collection.children.link(collection)
        group = bpy.data.objects.new(label, None)
        group.empty_display_type = "PLAIN_AXES"
        group.empty_display_size = 2
        collection.objects.link(group)
        group.parent = root
        GROUPS[key] = group
    return root


def mesh_object(name, verts, faces, material_name, category, module, center=(0, 0, 0), rotation=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(make_material(material_name))
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    COLLECTIONS_KEY = category
    collection = next(c for c in bpy.data.collections if c.name == COLLECTIONS[COLLECTIONS_KEY])
    collection.objects.link(obj)
    obj.parent = GROUPS[COLLECTIONS_KEY]
    obj.location = center
    if rotation is not None:
        obj.rotation_euler = rotation
    obj["module_id"] = module
    obj["export_category"] = COLLECTIONS[COLLECTIONS_KEY]
    OBJECTS.append(obj)
    return obj


def box(category, module, part, center, size, material="MAT_Concrete_Naval", rotation=None, bevel=0.08):
    sx, sy, sz = (float(v) for v in size)
    x, y, z = sx / 2, sy / 2, sz / 2
    verts = [
        (-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z),
        (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z),
    ]
    faces = [
        (0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
        (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),
    ]
    name = f"{category}__{module}__{part}"
    obj = mesh_object(name, verts, faces, material, category, module, center, rotation)
    if bevel > 0 and min(sx, sy, sz) > bevel * 1.7:
        modifier = obj.modifiers.new("Soft_Edges", "BEVEL")
        modifier.width = min(bevel, min(sx, sy, sz) * 0.12)
        modifier.segments = 1
        modifier.limit_method = "ANGLE"
    return obj


def cylinder(category, module, part, center, radius, depth, material="MAT_Steel_Grey", vertices=10, rotation=None):
    verts = []
    for z in (-depth / 2, depth / 2):
        for i in range(vertices):
            angle = 2 * math.pi * i / vertices
            verts.append((radius * math.cos(angle), radius * math.sin(angle), z))
    faces = [tuple(range(vertices - 1, -1, -1)), tuple(range(vertices, 2 * vertices))]
    for i in range(vertices):
        j = (i + 1) % vertices
        faces.append((i, j, vertices + j, vertices + i))
    return mesh_object(f"{category}__{module}__{part}", verts, faces, material, category, module, center, rotation)


def beam(category, module, part, start, end, width, depth, material="MAT_Steel_Grey"):
    a, b = Vector(start), Vector(end)
    vector = b - a
    obj = box(category, module, part, (a + b) * 0.5, (vector.length, width, depth), material, bevel=min(width, depth) * 0.08)
    obj.rotation_euler = vector.to_track_quat("X", "Z").to_euler()
    return obj


def make_shed(category, module, x, y, width, length, wall_h, ridge_h, facade_material="MAT_Concrete_Light", with_doors=True):
    # Four wall runs leave a wide open industrial bay at the south end.
    wall_t = 1.5
    door_w = width * 0.56
    front_y = y - length / 2 + wall_t / 2
    back_y = y + length / 2 - wall_t / 2
    side_x = x - width / 2 + wall_t / 2
    box(category, module, "SideWall_West", (side_x, y, wall_h / 2), (wall_t, length, wall_h), facade_material)
    box(category, module, "SideWall_East", (x + width / 2 - wall_t / 2, y, wall_h / 2), (wall_t, length, wall_h), facade_material)
    box(category, module, "BackWall", (x, back_y, wall_h / 2), (width, wall_t, wall_h), facade_material)
    flank = (width - door_w) / 2
    box(category, module, "FrontFlank_West", (x - door_w / 2 - flank / 2, front_y, wall_h / 2), (flank, wall_t, wall_h), facade_material)
    box(category, module, "FrontFlank_East", (x + door_w / 2 + flank / 2, front_y, wall_h / 2), (flank, wall_t, wall_h), facade_material)
    box(category, module, "FrontLintel", (x, front_y, wall_h - 1.3), (door_w, wall_t, 2.6), facade_material)
    box(category, module, "Interior_Slab", (x, y, 0.08), (width - 3, length - 3, 0.16), "MAT_Concrete_Naval", bevel=0.02)
    # Two roof planes and a central ridge cap keep a readable military-yard silhouette.
    roof_angle = math.atan2(ridge_h - wall_h, width / 2)
    roof_len = math.sqrt((width * 0.53) ** 2 + (ridge_h - wall_h) ** 2)
    box(category, module, "RoofSlope_West", (x - width / 4, y, wall_h + (ridge_h - wall_h) / 2), (roof_len, length + 2, 1.5), "MAT_RoofMetal", (0, -roof_angle, 0), 0.05)
    box(category, module, "RoofSlope_East", (x + width / 4, y, wall_h + (ridge_h - wall_h) / 2), (roof_len, length + 2, 1.5), "MAT_RoofMetal", (0, roof_angle, 0), 0.05)
    box(category, module, "RidgeVent", (x, y, ridge_h + 1), (2.2, length * 0.72, 1.8), "MAT_GalvanizedMetal")
    # Repeated roof ribs and structural portal frames are coarse enough for the game camera.
    for i, yy in enumerate([y - length * 0.39, y - length * 0.13, y + length * 0.13, y + length * 0.39]):
        beam(category, module, f"PortalFrame_{i:02d}_West", (x - width * 0.47, yy, wall_h - 0.5), (x, yy, ridge_h), 0.65, 0.65, "MAT_GalvanizedMetal")
        beam(category, module, f"PortalFrame_{i:02d}_East", (x, yy, ridge_h), (x + width * 0.47, yy, wall_h - 0.5), 0.65, 0.65, "MAT_GalvanizedMetal")
        for sign in (-1, 1):
            box(category, module, f"PortalColumn_{i:02d}_{sign}", (x + sign * width * 0.47, yy, wall_h / 2), (0.75, 0.75, wall_h), "MAT_GalvanizedMetal")
    for side in (-1, 1):
        for i in range(4):
            yy = y - length * 0.34 + i * length * 0.22
            box(category, module, f"SideLouver_{side}_{i:02d}", (x + side * (width / 2 + 0.82), yy, wall_h * 0.63), (0.12, length * 0.11, 2.4), "MAT_GalvanizedMetal", bevel=0.02)
            # Recessed technical window bands read clearly without glazing the whole wall.
            box(category, module, f"TechWindow_{side}_{i:02d}", (x + side * (width / 2 + 0.91), yy, wall_h * 0.80), (0.14, length * 0.10, 1.65), "MAT_Glass", bevel=0.01)
            box(category, module, f"WindowSill_{side}_{i:02d}", (x + side * (width / 2 + 1.02), yy, wall_h * 0.80 - 0.9), (0.32, length * 0.115, 0.13), "MAT_GalvanizedMetal", bevel=0)
    for i in range(6):
        yy = y - length * 0.41 + i * length * 0.164
        box(category, module, f"Skylight_{i:02d}", (x, yy, ridge_h + 1.96), (1.2, length * 0.12, 0.12), "MAT_Glass", bevel=0.02)
    if with_doors:
        for side in (-1, 1):
            box(category, module, f"DoorJamb_{side}", (x + side * door_w / 2, front_y - 0.15, wall_h / 2), (0.8, 0.8, wall_h - 1.8), "MAT_GalvanizedMetal")
        for i in range(8):
            xx = x - door_w * 0.44 + i * door_w * 0.126
            box(category, module, f"DoorSlat_{i:02d}", (xx, front_y + 1.0, wall_h * 0.43), (0.5, 0.25, wall_h * 0.80), "MAT_DarkSteel", bevel=0.015)
        for side in (-1, 1):
            box(category, module, f"DoorSafetyPost_{side}", (x + side * door_w * 0.55, front_y - 1.0, 1.6), (0.55, 0.6, 3.2), "MAT_SafetyYellow")
    # Low perimeter markings and broad sliding-door tracks.
    box(category, module, "DoorTrack", (x, front_y - 1.2, 0.16), (door_w, 0.45, 0.24), "MAT_Steel_Grey", bevel=0.02)


def build_ground():
    # The 500 x 340 m platform is assembled from large slabs. Two real openings
    # are left in the south half so the dry-dock basins remain visible and open.
    for ix in range(10):
        x = -225 + ix * 50
        box("Ground", "ConcreteBase", f"NorthSlab_{ix:02d}", (x, 110, -0.55), (49.7, 119.7, 1.1), "MAT_Concrete_Naval", bevel=0.02)
    box("Ground", "ConcreteBase", "WestApron", (-187.5, -58, -0.55), (124.7, 223.7, 1.1), "MAT_Concrete_Naval", bevel=0.02)
    box("Ground", "ConcreteBase", "CentralApron", (-5, -58, -0.55), (91.7, 223.7, 1.1), "MAT_Concrete_Naval", bevel=0.02)
    box("Ground", "ConcreteBase", "EastApron", (182.5, -58, -0.55), (134.7, 223.7, 1.1), "MAT_Concrete_Naval", bevel=0.02)
    # Segmented quay coping leaves both basin entrances open to the sea.
    for label, x, width in (("West", -187.5, 124.7), ("Center", -5, 91.7), ("East", 182.5, 134.7)):
        box("Ground", "ConcreteBase", f"SouthQuayLip_{label}", (x, -169, -0.55), (width, 2, 1.1), "MAT_Concrete_Naval", bevel=0.02)
        box("Security", f"SouthQuay_{label}", "Quay_Coping", (x, -169, 1.3), (width, 2.2, 2.6), "MAT_Concrete_Light")
        box("Security", f"SouthQuay_{label}", "QuaySafetyEdge", (x, -167.7, 2.64), (width, 0.22, 0.07), "MAT_SafetyYellow", bevel=0)
    # Keep a 48 m clear vehicle entrance aligned to the north access road.
    box("Security", "NorthPerimeter_West", "Wall_Run", (-137, 169, 3.0), (226, 2, 6), "MAT_Concrete_Light")
    box("Security", "NorthPerimeter_East", "Wall_Run", (137, 169, 3.0), (226, 2, 6), "MAT_Concrete_Light")
    box("Security", "WestPerimeter", "Wall_Run", (-249, 0, 3.0), (2, 338, 6), "MAT_Concrete_Light")
    box("Security", "EastPerimeter", "Wall_Run", (249, 0, 3.0), (2, 338, 6), "MAT_Concrete_Light")
    for x in range(-225, 226, 50):
        if abs(x) < 30:
            continue
        box("Security", f"WatchPost_{x:+04d}", "Body", (x, 168, 8), (8, 8, 10), "MAT_Concrete_Light")
        box("Security", f"WatchPost_{x:+04d}", "Roof", (x, 168, 13.5), (10, 10, 1), "MAT_RoofMetal")
    # Water remains a separate collider-free module in Unity and surrounds the quays.
    box("Environment", "HarborWater", "Sea_South", (0, -266, -2.1), (800, 190, 0.5), "MAT_Water", bevel=0)
    box("Environment", "HarborWater", "Sea_West", (-300, 0, -2.1), (100, 338, 0.5), "MAT_Water", bevel=0)
    box("Environment", "HarborWater", "Sea_East", (300, 0, -2.1), (100, 338, 0.5), "MAT_Water", bevel=0)
    # Landward shelters and sparse trees frame the gate like the reference image.
    box("Environment", "LandEdge", "Coastal_Terrace", (0, 205, -0.3), (560, 70, 0.6), "MAT_Green", bevel=0)
    for x in range(-240, 241, 30):
        if abs(x) < 35:
            continue
        box("Security", f"NorthButtress_{x:+04d}", "Body", (x, 167.5, 4.0), (3.4, 3, 8), "MAT_Concrete_Naval")
        rock_w = 12 + (abs(x) % 3) * 2
        box("Environment", f"NorthRock_{x:+04d}", "ShoreRock", (x, 215, 3.0), (rock_w, 12, 6), "MAT_Green", rotation=(0, math.radians((x % 9) * 2), math.radians(x % 7)))
        box("Environment", f"NorthRock_{x:+04d}", "ShelterRoof", (x, 215, 6.3), (rock_w + 1.0, 13.0, 0.55), "MAT_DarkSteel")
    for i, x in enumerate(range(-242, 243, 19)):
        if abs(x) < 38:
            continue
        y = 191 + (i % 3) * 11
        height = 7.0 + (i % 4) * 0.8
        module = f"CoastalTree_{i:02d}"
        cylinder("Environment", module, "Trunk", (x, y, height * 0.38), 0.42, height * 0.76, "MAT_DarkSteel", vertices=7)
        foliage_cluster("Environment", module, "CanopyLower", (x, y, height * 0.90), 3.5 + (i % 3) * 0.5, 5.8, "MAT_Foliage_Olive")
        foliage_cluster("Environment", module, "CanopyUpper", (x + 0.5, y + 0.3, height * 1.25), 2.7 + (i % 2) * 0.3, 5.0, "MAT_Green")


def build_dry_docks():
    for side, x in (("West", -88), ("East", 78)):
        module = f"DryDock_{side}"
        length, width = 158, 74
        center_y = -79
        # Recessed but open-ended basin; no entrance cross-wall blocks navigation.
        box("Dock", module, "BasinFloor", (x, center_y, -5.8), (width - 5, length - 7, 0.9), "MAT_Concrete_Light")
        for sign, label in ((-1, "WestWall"), (1, "EastWall")):
            wx = x + sign * (width / 2 - 1.5)
            box("Dock", module, label, (wx, center_y, 4.1), (3, length, 9.0), "MAT_Concrete_Naval")
            box("Dock", module, label + "Coping", (wx, center_y, 9.0), (4.8, length, 0.65), "MAT_Concrete_Light")
            for i in range(12):
                yy = center_y - length * 0.45 + i * length * 0.082
                box("Dock", module, f"Fender_{label}_{i:02d}", (wx - sign * 2.5, yy, 3.1), (0.65, 2.8, 3.2), "MAT_DarkSteel", bevel=0.06)
                box("Dock", module, f"SafetyStrip_{label}_{i:02d}", (wx - sign * 2.6, yy, 9.35), (0.75, 2.1, 0.14), "MAT_SafetyYellow", bevel=0)
            for i in range(6):
                yy = center_y - length * 0.40 + i * length * 0.16
                beam("Dock", module, f"Handrail_{label}_{i:02d}", (wx - sign * 2.6, yy, 9.8), (wx - sign * 2.6, yy + 17, 9.8), 0.14, 0.14, "MAT_SafetyYellow")
                for dy in (0, 8.5, 17):
                    cylinder("Dock", module, f"HandrailPost_{label}_{i:02d}_{dy}", (wx - sign * 2.6, yy + dy, 9.0), 0.10, 1.6, "MAT_SafetyYellow", vertices=6)
        box("Dock", module, "FarWall", (x, center_y + length / 2 - 1.5, 4.1), (width, 3, 9), "MAT_Concrete_Naval")
        for rail_sign in (-1, 1):
            rx = x + rail_sign * 13
            box("Dock", module, f"CradleRail_{rail_sign}", (rx, center_y, -4.9), (1.2, length - 6, 0.38), "MAT_GalvanizedMetal", bevel=0.02)
            for i in range(13):
                yy = center_y - 53 + i * 8.8
                box("Dock", module, f"KeelBlock_{rail_sign}_{i:02d}", (rx, yy, -3.0), (3.2, 2.3, 3.5), "MAT_DarkSteel", bevel=0.06)
        for i in range(8):
            xx = x - 28 + i * 8
            box("Dock", module, f"TransomSupport_{i:02d}", (xx, center_y + 45, -3.2), (2.8, 5.2, 4.2), "MAT_Concrete_Light")
        # Mooring bollards at the entrance and at the basin head.
        for i, xx in enumerate((x - 30, x, x + 30)):
            for yy in (center_y - length / 2 + 2, center_y + length / 2 - 3):
                cylinder("Dock", module, f"MooringBollard_{i}_{int(yy)}", (xx, yy, 2.2), 0.7, 2.7, "MAT_DarkSteel", vertices=8)
        # Coarse ladders at each wall keep the platforms believable without micro-detail.
        for sign in (-1, 1):
            lx = x + sign * (width / 2 - 5)
            for ladder_index, yy in enumerate((center_y - 33, center_y + 35)):
                for rung in range(7):
                    box("Dock", module, f"Ladder_{sign}_{ladder_index}_Rung_{rung}", (lx, yy, 1 + rung * 1.05), (1.4, 0.18, 0.16), "MAT_GalvanizedMetal", bevel=0.015)


def build_crane(module, x_center, y_center, span=88, rail_length=34, height=31):
    # Separated animated-ready parts: legs, beam, cab, trolley, cable, hook and wheels.
    leg_x = (x_center - span * 0.44, x_center + span * 0.44)
    for i, x in enumerate(leg_x):
        box("Crane", module, f"Leg_{i}_Lower", (x, y_center, height * 0.34), (3.2, 4.2, height * 0.68), "MAT_Steel_Grey")
        box("Crane", module, f"Leg_{i}_Foot", (x, y_center, 1.2), (7.2, 8, 2.4), "MAT_SafetyYellow")
        for end in (-1, 1):
            yy = y_center + end * rail_length * 0.42
            cylinder("Crane", module, f"RailWheel_{i}_{end}", (x, yy, 1.2), 1.45, 2.0, "MAT_DarkSteel", vertices=12, rotation=(math.pi / 2, 0, 0))
        beam("Crane", module, f"LegBrace_{i}_A", (x, y_center - rail_length * 0.42, height * 0.26), (x, y_center, height * 0.70), 1.1, 1.1, "MAT_GalvanizedMetal")
        beam("Crane", module, f"LegBrace_{i}_B", (x, y_center + rail_length * 0.42, height * 0.26), (x, y_center, height * 0.70), 1.1, 1.1, "MAT_GalvanizedMetal")
    box("Crane", module, "UpperBeam", (x_center, y_center, height), (span, 6.5, 5), "MAT_SafetyYellow")
    box("Crane", module, "BridgeGantryWeb", (x_center, y_center, height - 4.4), (span * 0.88, 1.1, 3.4), "MAT_Steel_Grey")
    for i in range(14):
        xx = x_center - span * 0.40 + i * span * 0.0615
        beam("Crane", module, f"Truss_{i:02d}", (xx, y_center, height - 5.9), (xx + 2.8, y_center, height - 2.2), 0.38, 0.38, "MAT_GalvanizedMetal")
    box("Crane", module, "OperatorCabin", (x_center + span * 0.30, y_center - rail_length * 0.40, height * 0.78), (5.4, 5, 5.2), "MAT_Glass")
    box("Crane", module, "CabinBase", (x_center + span * 0.30, y_center - rail_length * 0.40, height * 0.63), (6.2, 6, 1.1), "MAT_Steel_Grey")
    box("Crane", module, "Trolley", (x_center, y_center, height + 3.0), (12, 9, 2.5), "MAT_DarkSteel")
    box("Crane", module, "HoistMotor", (x_center, y_center, height + 4.8), (7, 6, 2.6), "MAT_GalvanizedMetal")
    box("Crane", module, "Cable", (x_center, y_center, height - 8), (0.35, 0.35, 16), "MAT_DarkSteel", bevel=0)
    box("Crane", module, "HookBlock", (x_center, y_center, height - 17), (2.2, 2.4, 3.2), "MAT_SafetyYellow")
    beam("Crane", module, "Hook", (x_center, y_center, height - 18.4), (x_center + 1.6, y_center, height - 19.2), 0.5, 0.5, "MAT_DarkSteel")
    for x in leg_x:
        for sign in (-1, 1):
            box("Crane", module, f"HazardStripe_{int(x)}_{sign}", (x + sign * 1.75, y_center - 2.15, height * 0.38), (1.1, 0.10, 4.8), "MAT_SafetyYellow", bevel=0)


def build_hangars():
    make_shed("MainBuilding", "MainHangar_West", -88, 69, 70, 98, 22, 30)
    make_shed("MainBuilding", "MainHangar_East", 78, 69, 70, 98, 22, 30)
    make_shed("Maintenance", "MaintenanceHangar_NorthWest", -186, 83, 38, 54, 14, 19, "MAT_Concrete_Light")
    make_shed("Maintenance", "MaintenanceHangar_NorthEast", 178, 83, 42, 58, 15, 20, "MAT_Concrete_Light")
    make_shed("Workshop", "Workshop_ShipSystems", 179, -20, 42, 42, 12, 16, "MAT_Concrete_Naval", with_doors=False)
    make_shed("Storage", "Warehouse_North", 18, 137, 58, 26, 9, 13, "MAT_Concrete_Light", with_doors=False)
    make_shed("Storage", "Warehouse_East", 199, 19, 36, 42, 9, 12, "MAT_Concrete_Light", with_doors=False)
    for idx, x in enumerate((-88, 78)):
        for y in (20, 118):
            for sign in (-1, 1):
                cylinder("MainBuilding", f"HangarFacade_{idx}", f"Apron_Bollard_{int(y)}_{sign}", (x + sign * 35, y, 1.1), 0.45, 2.2, "MAT_SafetyYellow", vertices=8)


def build_cranes_and_piers():
    for side, x in (("West", -88), ("East", 78)):
        # Two bridge gantries travel the same rail corridor over each dock.
        for index, y in enumerate((-42, -116), start=1):
            build_crane(f"ShipyardGantry_{side}_{index}", x, y, span=94, rail_length=52, height=31 if side == "West" else 34)
        # Each dock has one continuous rail corridor, separate from the moving cranes.
        rail_module = f"GantryRails_{side}"
        for sign in (-1, 1):
            rx = x + sign * 41.5
            box("Crane", rail_module, f"GantryRailBed_{sign}", (rx, -79, 1.2), (5.4, 158, 2.4), "MAT_Concrete_Light")
            box("Crane", rail_module, f"GantryRail_{sign}", (rx, -79, 2.55), (0.8, 158, 0.3), "MAT_GalvanizedMetal", bevel=0.02)
    for side, x in (("West", -186), ("East", 186)):
        module = f"Pier_{side}"
        box("Pier", module, "Deck", (x, -211, 1.4), (23, 82, 1.4), "MAT_Concrete_Naval")
        box("Pier", module, "BumperStrip", (x, -250.5, 2.18), (23, 0.55, 0.12), "MAT_SafetyYellow", bevel=0)
        for sign in (-1, 1):
            box("Pier", module, f"SafetyEdge_{sign}", (x + sign * 11.2, -211, 2.18), (0.45, 81, 0.12), "MAT_SafetyYellow", bevel=0)
        for i, y in enumerate((-243, -223, -203, -183)):
            for sign in (-1, 1):
                cylinder("Pier", module, f"Pile_{i}_{sign}", (x + sign * 8.5, y, -3.4), 1.2, 10, "MAT_DarkSteel", vertices=10)
        for i, y in enumerate((-241, -222, -203, -184)):
            cylinder("Pier", module, f"Bollard_{i}", (x, y, 2.7), 0.75, 1.8, "MAT_Steel_Grey", vertices=8)
            box("Light", module, f"PierLight_{i}", (x + 9.5, y, 7), (0.5, 0.5, 9), "MAT_Steel_Grey", bevel=0.02)
            box("Light", module, f"PierLightHead_{i}", (x + 9.5, y, 11.8), (2.8, 1.8, 0.45), "MAT_SafetyYellow")
    # Quay face fenders and bollards are restricted to solid concrete sections.
    for section, start, stop in (("West", -245, -125), ("Center", -45, 41), ("East", 115, 246)):
        for i, x in enumerate(range(start, stop, 20)):
            module = f"SouthQuay_{section}"
            box("Pier", module, f"QuayFender_{i:02d}", (x, -172.4, 0.4), (2.5, 1.8, 4.2), "MAT_DarkSteel")
            cylinder("Pier", module, f"TireFender_{i:02d}", (x, -173.55, 0.5), 1.8, 0.7, "MAT_DarkSteel", vertices=16, rotation=(math.pi / 2, 0, 0))
            cylinder("Pier", module, f"QuayBollard_{i:02d}", (x, -167.5, 2), 0.65, 1.7, "MAT_Steel_Grey", vertices=8)


def build_admin_and_parking():
    # Compact low-rise command offices with glass bands; deliberately secondary to the dock.
    for idx, (x, y) in enumerate(((-190, 91), (-190, 57), (-130, 91))):
        module = f"Administration_Block_{idx+1}"
        w, l, h = (46, 25, 12) if idx < 2 else (34, 25, 10)
        box("Admin", module, "GroundFloor", (x, y, h * 0.31), (w, l, h * 0.62), "MAT_Concrete_Light")
        box("Admin", module, "UpperFloor", (x, y, h * 0.81), (w * 0.88, l * 0.92, h * 0.37), "MAT_Concrete_Naval")
        box("Admin", module, "Roof", (x, y, h + 0.6), (w + 2, l + 2, 1.2), "MAT_RoofMetal")
        box("Admin", module, "WindowBand_South", (x, y - l / 2 - 0.15, h * 0.78), (w * 0.72, 0.25, 2.1), "MAT_Glass", bevel=0.02)
        box("Admin", module, "WindowBand_North", (x, y + l / 2 + 0.15, h * 0.78), (w * 0.72, 0.25, 2.1), "MAT_Glass", bevel=0.02)
        for sign in (-1, 1):
            box("Admin", module, f"EntryColumn_{sign}", (x + sign * w * 0.23, y - l / 2 - 1.2, 3.0), (1, 1, 6), "MAT_Steel_Grey")
    # A separate marked parking field and pedestrian strip match the north-west reference zone.
    box("Admin", "CommandParking", "ParkingAsphalt", (-185, 27, 0.06), (95, 35, 0.12), "MAT_Asphalt", bevel=0)
    for row in range(2):
        yy = 17 + row * 16
        for col in range(12):
            xx = -229 + col * 7.8
            box("Admin", "CommandParking", f"ParkingBay_{row}_{col:02d}", (xx, yy, 0.16), (0.16, 6.5, 0.08), "MAT_White_Marking", bevel=0)
    box("Admin", "CommandParking", "ParkingCrossAisle", (-185, 27, 0.18), (94, 5.5, 0.10), "MAT_White_Marking", bevel=0)
    for i, x in enumerate((-235, -220, -158, -143)):
        box("Admin", "PedestrianWalk", f"WalkTreeTrunk_{i}", (x, 56, 2.2), (0.8, 0.8, 4.4), "MAT_Steel_Grey", bevel=0.02)
        # Flat foliage massing is intentionally restrained for an RTS camera.
        cone("Admin", "PedestrianWalk", f"WalkTreeCanopy_{i}", (x, 56, 5.5), 2.7, 5.2, "MAT_Green", vertices=7)


def cone(category, module, part, center, radius, depth, material="MAT_Green", vertices=8):
    verts = []
    for i in range(vertices):
        angle = 2 * math.pi * i / vertices
        verts.append((radius * math.cos(angle), radius * math.sin(angle), -depth / 2))
    verts.append((0, 0, depth / 2))
    faces = [tuple(range(vertices - 1, -1, -1))]
    for i in range(vertices):
        faces.append((i, (i + 1) % vertices, vertices))
    return mesh_object(f"{category}__{module}__{part}", verts, faces, material, category, module, center)


def foliage_cluster(category, module, part, center, radius, height, material):
    """Faceted, broad tree crown with two offset rings for an RTS silhouette."""
    verts = [(0, 0, height * 0.55)]
    for z, ring_radius, phase in ((height * 0.20, radius * 0.84, 0.0), (-height * 0.22, radius, 0.19)):
        for i in range(9):
            angle = 2 * math.pi * i / 9 + phase
            verts.append((math.cos(angle) * ring_radius, math.sin(angle) * ring_radius, z))
    verts.append((0, 0, -height * 0.54))
    faces = []
    for i in range(9):
        j = (i + 1) % 9
        faces.append((0, 1 + i, 1 + j))
        faces.append((1 + i, 10 + i, 10 + j, 1 + j))
        faces.append((19, 10 + j, 10 + i))
    return mesh_object(f"{category}__{module}__{part}", verts, faces, material, category, module, center)


def assign_game_uvs():
    """World-projected metre UVs keep large slabs and narrow rails unstretched."""
    bpy.context.view_layer.update()
    for obj in OBJECTS:
        mesh = obj.data
        mesh.update()
        uv_layer = mesh.uv_layers.get("UV_Game_Tile") or mesh.uv_layers.new(name="UV_Game_Tile")
        tile = UV_TILE_METERS[mesh.materials[0].name]
        world = obj.matrix_world
        normal_matrix = world.to_3x3()
        for polygon in mesh.polygons:
            normal = normal_matrix @ polygon.normal
            axis = max(range(3), key=lambda item: abs(normal[item]))
            for loop_index in polygon.loop_indices:
                local = mesh.vertices[mesh.loops[loop_index].vertex_index].co
                point = world @ local
                if axis == 0:
                    uv = (point.y / tile, point.z / tile)
                elif axis == 1:
                    uv = (point.x / tile, point.z / tile)
                else:
                    uv = (point.x / tile, point.y / tile)
                uv_layer.data[loop_index].uv = uv
        obj["uv_mapping"] = "world projected, tile metres " + str(tile)


def build_roads_and_yard():
    # Wide service roads trace the site perimeter and connect hangars, workshops and the quay.
    road_specs = [
        ("NorthRoad", (0, 156, 0.08), (470, 10, 0.16)),
        ("SouthQuayRoad_West", (-187.5, -163, 0.08), (124.7, 9, 0.16)),
        ("SouthQuayRoad_Center", (-5, -163, 0.08), (91.7, 9, 0.16)),
        ("SouthQuayRoad_East", (182.5, -163, 0.08), (134.7, 9, 0.16)),
        ("WestServiceRoad", (-235, 0, 0.08), (9, 330, 0.16)),
        ("EastServiceRoad", (235, 0, 0.08), (9, 330, 0.16)),
        ("MainSpine", (0, -5, 0.08), (10, 310, 0.16)),
        ("DockApronWest", (-133, -79, 0.08), (9, 158, 0.16)),
        ("DockApronEast", (124, -79, 0.08), (9, 158, 0.16)),
        ("CrossRoadNorth", (0, 24, 0.08), (436, 8, 0.16)),
    ]
    for module, center, size in road_specs:
        box("Road", module, "Asphalt", center, size, "MAT_Asphalt", bevel=0)
    # Road edge and centerline markings are sparse so they read at an oblique camera angle.
    for x in range(-220, 221, 20):
        box("Road", "NorthRoad", f"CenterDash_{x:+04d}", (x, 156, 0.19), (9, 0.24, 0.07), "MAT_White_Marking", bevel=0)
    for road, lower, upper in (("West", -220, -130), ("Center", -45, 40), ("East", 125, 220)):
        for dash, x in enumerate(range(lower, upper + 1, 20)):
            box("Road", f"SouthQuayRoad_{road}", f"CenterDash_{dash:02d}", (x, -163, 0.19), (9, 0.24, 0.07), "MAT_White_Marking", bevel=0)
    for y in range(-145, 146, 18):
        box("Road", "MainSpine", f"CenterDash_{y:+04d}", (0, y, 0.19), (0.24, 8, 0.07), "MAT_White_Marking", bevel=0)
    # Simple apron grids around each hangar and conspicuous hazard chevrons at the doors.
    for side, x in (("West", -88), ("East", 78)):
        for mark in range(7):
            xx = x - 25 + mark * 8.3
            box("Detail", f"Apron_{side}", f"StandMark_{mark:02d}", (xx, 25, 0.16), (0.22, 12, 0.08), "MAT_SafetyYellow", bevel=0)
        for sign in (-1, 1):
            box("Detail", f"Apron_{side}", f"ClearanceLine_{sign}", (x + sign * 34, 25, 0.17), (0.35, 18, 0.10), "MAT_SafetyYellow", bevel=0)


def build_pipes_and_storage():
    # Pipe rack along the industrial eastern edge, with separate runs ready for future extension.
    for i, y in enumerate((-85, -79, -72, -64)):
        cylinder("Pipe", "EastPipeRack", f"ProcessPipe_{i}", (208, y, 7 + (i % 2) * 1.4), 0.72 if i < 2 else 0.48, 164, "MAT_GalvanizedMetal", vertices=10, rotation=(0, math.pi / 2, 0))
    for i, y in enumerate(range(-91, -41, 10)):
        box("Pipe", "EastPipeRack", f"PipeSupport_{i:02d}", (208, y, 3.4), (5, 1, 6.8), "MAT_Steel_Grey")
        box("Pipe", "EastPipeRack", f"PipeSaddle_{i:02d}", (208, y, 7.0), (8, 1.2, 0.75), "MAT_SafetyYellow")
    # Service tanks and electrical equipment are intentionally broad silhouettes.
    for i, (x, y, radius, height) in enumerate(((208, -16, 8, 15), (220, -2, 5.5, 11), (197, -34, 5.2, 9))):
        cylinder("Utility", f"FuelTank_{i}", "TankBody", (x, y, height / 2), radius, height, "MAT_GalvanizedMetal", vertices=14)
        cylinder("Utility", f"FuelTank_{i}", "TankRoof", (x, y, height + 0.5), radius * 1.02, 1, "MAT_Steel_Grey", vertices=14)
        box("Utility", f"FuelTank_{i}", "SafetyBerm", (x, y, 0.65), (radius * 2.6, radius * 2.6, 1.3), "MAT_Concrete_Light")
    for row, y in enumerate((34, 40, 46, 52)):
        for column in range(5):
            x = 139 + column * 13
            part = f"Container_{row:02d}_{column:02d}"
            box("Storage", "ContainerYard", part, (x, y, 2.3), (11, 2.5, 4.6), "MAT_Steel_Grey")
            for rib in range(7):
                box("Storage", "ContainerYard", f"{part}_Rib_{rib:02d}", (x - 4.5 + rib * 1.5, y - 1.3, 2.3), (0.18, 0.15, 4.0), "MAT_GalvanizedMetal", bevel=0)
    # A compact electrical yard supplies the industrial side without turning it into a city.
    for i, (x, y) in enumerate(((212, 103), (230, 103), (212, 128))):
        module = f"Substation_Transformer_{i+1}"
        box("Utility", module, "ConcretePad", (x, y, 0.18), (14, 18, 0.36), "MAT_Concrete_Light")
        box("Utility", module, "TransformerBody", (x, y, 3.3), (8, 7, 6), "MAT_GalvanizedMetal")
        for side in (-1, 1):
            for fin in range(5):
                yy = y - 2.8 + fin * 1.4
                box("Utility", module, f"CoolingFin_{side}_{fin}", (x + side * 4.3, yy, 3.2), (0.45, 0.55, 4.8), "MAT_DarkSteel", bevel=0)
        for leg in (-1, 1):
            box("Utility", module, f"Insulator_{leg}", (x + leg * 2.5, y + 4.2, 7.0), (0.65, 0.65, 2.0), "MAT_Concrete_Light")


def build_lighting_and_security():
    poles = []
    for x in (-228, -160, -95, -28, 42, 112, 178, 228):
        poles.extend(((x, -164), (x, 158)))
    for y in (-130, -65, 10, 80, 145):
        poles.extend(((-229, y), (229, y)))
    for index, (x, y) in enumerate(poles):
        module = f"LightPole_{index:02d}"
        cylinder("Light", module, "Pole", (x, y, 9), 0.32, 18, "MAT_Steel_Grey", vertices=8)
        beam("Light", module, "Arm", (x, y, 17), (x + (1.4 if x < 0 else -1.4), y, 18.4), 0.22, 0.22, "MAT_Steel_Grey")
        box("Light", module, "Luminaire", (x + (1.5 if x < 0 else -1.5), y, 18.3), (2.4, 1.5, 0.65), "MAT_Concrete_Light")
        for lamp in (-1, 0, 1):
            box("Light", module, f"Lamp_{lamp:+d}", (x + (1.5 if x < 0 else -1.5) + lamp * 0.65, y - 0.80, 18.2), (0.48, 0.2, 0.35), "MAT_White_Marking", bevel=0)
        box("Light", module, "BasePlate", (x, y, 0.8), (1.5, 1.5, 1.6), "MAT_Concrete_Light")
    # A gate opening in the north fence with a booth and clear vehicle lane.
    box("Security", "MainGate", "Guardhouse", (-36, 165, 5), (12, 10, 10), "MAT_Concrete_Light")
    box("Security", "MainGate", "GuardhouseGlass", (-36, 159.8, 5.5), (8, 0.25, 3.2), "MAT_Glass", bevel=0.02)
    box("Security", "MainGate", "GateArm", (8, 156, 3.5), (18, 0.6, 0.6), "MAT_SafetyYellow")
    cylinder("Security", "MainGate", "GatePost", (0, 156, 2), 0.45, 4, "MAT_DarkSteel", vertices=8)
    for i in range(5):
        box("Security", "MainGate", f"AntiVehicle_Bollard_{i}", (19 + i * 3.2, 155, 1.4), (0.9, 1.1, 2.8), "MAT_SafetyYellow")


def make_camera(name, location, target, ortho_scale):
    data = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = location
    direction = Vector(target) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    data.type = "ORTHO"
    data.ortho_scale = ortho_scale
    data.lens = 52
    data.clip_start = 0.1
    data.clip_end = 4000
    return cam


def setup_lighting():
    scene = bpy.context.scene
    world = bpy.data.worlds.new("NavalShipyard_SoftSky")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.38, 0.43, 0.45, 1)
    background.inputs["Strength"].default_value = 0.78
    sun_data = bpy.data.lights.new("LateMorning_Sun", "SUN")
    sun = bpy.data.objects.new("LateMorning_Sun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(25), math.radians(-18), math.radians(-30))
    sun_data.energy = 3.3
    fill_data = bpy.data.lights.new("SoftFill", "AREA")
    fill = bpy.data.objects.new("SoftFill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = (0, -20, 240)
    fill_data.energy = 85000
    fill_data.size = 220
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(PREVIEW_DIR / "NavalShipyard_Blockout_Isometric.png")
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0.25
    scene.camera = make_camera("Camera_Isometric", (640, -760, 760), (0, -25, 4), 650)
    # A named top-down camera remains available for a second render and future layout checks.
    top = make_camera("Camera_TopDown", (0, 0, 820), (0, 0, 0), 620)
    top["preview_only"] = True
    detail = make_camera("Camera_DockDetail", (220, -370, 240), (0, -75, 3), 270)
    detail["preview_only"] = True
    scene["unity_axis_note"] = "Blender XY ground plane exports to Unity XZ at 1 unit = 1 m"
    scene["shipyard_dimensions_m"] = "500 x 340 platform; 500 m primary length"


def export_and_render():
    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    bpy.ops.object.select_all(action="DESELECT")
    for obj in OBJECTS:
        obj.select_set(True)
    if OBJECTS:
        bpy.context.view_layer.objects.active = OBJECTS[0]
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_OUTPUT),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_NONE",
        global_scale=1.0,
        apply_unit_scale=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_custom_props=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        axis_forward="-Z",
        axis_up="Y",
    )
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.scene.camera = bpy.data.objects["Camera_Isometric"]
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "NavalShipyard_Blockout_Isometric.png")
    bpy.ops.render.render(write_still=True)
    bpy.context.scene.camera = bpy.data.objects["Camera_TopDown"]
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "NavalShipyard_Blockout_TopDown.png")
    bpy.context.scene.render.resolution_x = 1440
    bpy.context.scene.render.resolution_y = 1080
    bpy.ops.render.render(write_still=True)
    bpy.context.scene.camera = bpy.data.objects["Camera_DockDetail"]
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "NavalShipyard_ArtPass_DockDetail.png")
    bpy.context.scene.render.resolution_x = 1440
    bpy.context.scene.render.resolution_y = 1080
    bpy.ops.render.render(write_still=True)
    bpy.context.scene.camera = bpy.data.objects["Camera_Isometric"]
    bpy.context.scene.render.filepath = str(PREVIEW_DIR / "NavalShipyard_Blockout_Isometric.png")
    iso_rotation = bpy.data.objects["Camera_Isometric"].rotation_euler.to_quaternion()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.region_3d.view_location = (0, -10, 0)
            space.region_3d.view_rotation = iso_rotation
            space.region_3d.view_distance = 730
            space.region_3d.view_perspective = "ORTHO"
            space.shading.type = "MATERIAL"
            space.overlay.show_floor = False
            space.overlay.show_relationship_lines = False
            space.overlay.show_cursor = False
    for obj in bpy.data.objects:
        if obj.type in {"CAMERA", "LIGHT"}:
            obj.hide_set(True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUTPUT), check_existing=False)


def main():
    clear_scene()
    TEXTURE_FILES.update(generate_textures(TEXTURE_DIR, PALETTE))
    create_hierarchy()
    build_ground()
    build_dry_docks()
    build_hangars()
    build_cranes_and_piers()
    build_admin_and_parking()
    build_roads_and_yard()
    build_pipes_and_storage()
    build_lighting_and_security()
    assign_game_uvs()
    setup_lighting()
    export_and_render()
    print(f"NAVAL_SHIPYARD_OBJECTS={len(OBJECTS)}")
    print(f"NAVAL_SHIPYARD_BLEND={BLEND_OUTPUT}")
    print(f"NAVAL_SHIPYARD_FBX={FBX_OUTPUT}")
    print(f"NAVAL_SHIPYARD_PREVIEW={PREVIEW_DIR}")


if __name__ == "__main__":
    main()
