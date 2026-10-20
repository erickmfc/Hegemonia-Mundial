"""Rig and animate the yellow carrier-deck crew member on the USS Enterprise.

The visual mesh is sourced from the deck-worker prefab and weighted to the exact
41-bone Avatar referenced by that prefab, not the similarly named rifle soldier.
The Unity project is only written to a new FBX path; existing scenes, prefabs,
and controllers are left untouched.
"""

from __future__ import annotations

import math
import os
import shutil
import subprocess
import sys
import time
import traceback

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector


PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORKER_PREFAB = os.path.join(
    PROJECT, "Assets", "Prefabs", "Navios_Guerra", "Porta avioes",
    "Trabalhador_Conves 1.prefab",
)
WORKER_MESH = os.path.join(
    PROJECT, "Assets", "tripo_convert_2dd7b898-ac47-4257-9870-7fa18e999adf.fbx",
)
AVATAR_FBX = os.path.join(PROJECT, "Assets", "Prefabs", "Soldado_Rifle", "Ani conves.fbx")
UNIFORM_TEXTURE = os.path.join(
    PROJECT, "Assets", "Prefabs", "Soldado_Rifle", "militarypilot3dmodel_basecolor.JPEG",
)
ENTERPRISE_GLB = os.path.join(
    PROJECT, "Assets", "Prefabs", "Navios_Guerra", "Porta avioes",
    "uss_enterprise_cvn-65_aircraft_carrier.glb",
)
FBX_OUTPUT_DIR = os.path.join(PROJECT, "Assets", "Animations", "TrabalhadorConvesEnterprise")
FBX_OUTPUT = os.path.join(FBX_OUTPUT_DIR, "TrabalhadorConves_Acoes.fbx")
OUTPUT_DIR = os.path.join(
    PROJECT, "Tools", "Blender", "output", "TrabalhadorConvesEnterprise", "Final"
)
BLEND_OUTPUT = os.path.join(OUTPUT_DIR, "TrabalhadorConves_Enterprise.blend")
STILLS_DIR = os.path.join(OUTPUT_DIR, "stills")
VIDEO_OUTPUT = os.path.join(OUTPUT_DIR, "Preview_8_Acoes_No_Enterprise.mp4")

HUMAN_HEIGHT = 1.80
ENTERPRISE_PREFAB_SCALE = (0.7127881, 0.78216, 0.781284)
CREW_BONES = (
    "Root", "Hip", "Waist", "Spine01", "Spine02", "NeckTwist01",
    "NeckTwist02", "Head", "L_Clavicle", "L_Upperarm", "L_Forearm",
    "L_ForearmTwist01", "L_ForearmTwist02", "L_Hand", "L_UpperarmTwist01",
    "L_UpperarmTwist02", "R_Clavicle", "R_Upperarm", "R_UpperarmTwist01",
    "R_UpperarmTwist02", "R_Forearm", "R_ForearmTwist01", "R_ForearmTwist02",
    "R_Hand", "Pelvis", "R_Thigh", "R_ThighTwist01", "R_ThighTwist02",
    "R_Calf", "R_Foot", "R_ToeBase", "R_CalfTwist01", "R_CalfTwist02",
    "L_Thigh", "L_Calf", "L_Foot", "L_ToeBase", "L_CalfTwist01",
    "L_CalfTwist02", "L_ThighTwist01", "L_ThighTwist02",
)


def log(message: str) -> None:
    print(f"[DeckCrew] {message}", flush=True)


def require_file(path: str) -> None:
    if not os.path.isfile(path):
        raise FileNotFoundError(f"Required source asset is missing: {path}")


def object_snapshot():
    return {obj.as_pointer() for obj in bpy.data.objects}


def objects_added_since(previous):
    return [obj for obj in bpy.data.objects if obj.as_pointer() not in previous]


def import_fbx_without_source_animation(path: str):
    before = object_snapshot()
    bpy.ops.import_scene.fbx(
        filepath=path,
        use_anim=False,
        use_custom_normals=False,
        use_custom_props=False,
        use_image_search=False,
    )
    return objects_added_since(before)


def import_enterprise(path: str):
    before = object_snapshot()
    bpy.ops.import_scene.gltf(filepath=path)
    return objects_added_since(before)


def mesh_bounds(mesh_objects):
    total = sum(len(obj.data.vertices) for obj in mesh_objects)
    if not total:
        raise RuntimeError("Could not measure an imported mesh: no vertices were found")
    coords = np.empty(total * 3, dtype=np.float32)
    offset = 0
    for obj in mesh_objects:
        count = len(obj.data.vertices) * 3
        obj.data.vertices.foreach_get("co", coords[offset:offset + count])
        offset += count
    points = coords.reshape((-1, 3))
    low = Vector(tuple(float(v) for v in points.min(axis=0)))
    high = Vector(tuple(float(v) for v in points.max(axis=0)))
    return low, high


def bake_world_transform_into_data(obj, world_matrix):
    obj.parent = None
    obj.matrix_world = world_matrix
    if obj.type == "MESH":
        obj.data.transform(world_matrix)
    elif obj.type == "ARMATURE":
        obj.data.transform(world_matrix)
    obj.matrix_world = Matrix.Identity(4)


def join_meshes(mesh_objects, name):
    if not mesh_objects:
        raise RuntimeError(f"No mesh objects were imported for {name}")
    if len(mesh_objects) == 1:
        result = mesh_objects[0]
    else:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in mesh_objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = mesh_objects[0]
        bpy.ops.object.join()
        result = bpy.context.object
    result.name = name
    return result


def create_procedural_armature():
    """Build a clean 41-bone human rig calibrated to the worker's 1.8 m bounds."""
    data = bpy.data.armatures.new("DeckCrew_41Bone_Rig")
    armature = bpy.data.objects.new("DeckCrew_41Bone_Rig", data)
    bpy.context.collection.objects.link(armature)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")

    # The character faces +Y; the mesh and rig are authored in metres with feet at Z=0.
    bone_specs = {
        "Root": ((0.0, 0.0, 0.00), (0.0, 0.0, 0.10), None, False),
        "Hip": ((0.0, 0.0, 0.83), (0.0, 0.0, 0.98), "Root", True),
        "Waist": ((0.0, 0.0, 0.91), (0.0, 0.0, 1.16), "Hip", True),
        "Spine01": ((0.0, 0.0, 1.12), (0.0, 0.0, 1.34), "Waist", True),
        "Spine02": ((0.0, 0.0, 1.31), (0.0, 0.0, 1.49), "Spine01", True),
        "NeckTwist01": ((0.0, 0.0, 1.46), (0.0, 0.0, 1.54), "Spine02", True),
        "NeckTwist02": ((0.0, 0.0, 1.52), (0.0, 0.0, 1.61), "NeckTwist01", True),
        "Head": ((0.0, 0.0, 1.58), (0.0, 0.0, 1.79), "NeckTwist02", True),
        "L_Clavicle": ((0.0, 0.0, 1.46), (-0.15, 0.0, 1.45), "Spine02", True),
        "L_Upperarm": ((-0.15, 0.0, 1.45), (-0.18, 0.0, 1.17), "L_Clavicle", True),
        "L_Forearm": ((-0.18, 0.0, 1.17), (-0.19, 0.0, 0.94), "L_Upperarm", True),
        "L_ForearmTwist01": ((-0.18, 0.0, 1.12), (-0.19, 0.0, 1.04), "L_Forearm", False),
        "L_ForearmTwist02": ((-0.19, 0.0, 1.04), (-0.19, 0.0, 0.96), "L_Forearm", False),
        "L_Hand": ((-0.19, 0.0, 0.94), (-0.19, 0.0, 0.80), "L_Forearm", True),
        "L_UpperarmTwist01": ((-0.16, 0.0, 1.39), (-0.17, 0.0, 1.31), "L_Upperarm", False),
        "L_UpperarmTwist02": ((-0.17, 0.0, 1.31), (-0.18, 0.0, 1.23), "L_Upperarm", False),
        "R_Clavicle": ((0.0, 0.0, 1.46), (0.15, 0.0, 1.45), "Spine02", True),
        "R_Upperarm": ((0.15, 0.0, 1.45), (0.18, 0.0, 1.17), "R_Clavicle", True),
        "R_UpperarmTwist01": ((0.16, 0.0, 1.39), (0.17, 0.0, 1.31), "R_Upperarm", False),
        "R_UpperarmTwist02": ((0.17, 0.0, 1.31), (0.18, 0.0, 1.23), "R_Upperarm", False),
        "R_Forearm": ((0.18, 0.0, 1.17), (0.19, 0.0, 0.94), "R_Upperarm", True),
        "R_ForearmTwist01": ((0.18, 0.0, 1.12), (0.19, 0.0, 1.04), "R_Forearm", False),
        "R_ForearmTwist02": ((0.19, 0.0, 1.04), (0.19, 0.0, 0.96), "R_Forearm", False),
        "R_Hand": ((0.19, 0.0, 0.94), (0.19, 0.0, 0.80), "R_Forearm", True),
        "Pelvis": ((0.0, 0.0, 0.78), (0.0, 0.0, 0.99), "Hip", True),
        "R_Thigh": ((0.10, 0.0, 0.91), (0.11, 0.0, 0.50), "Pelvis", True),
        "R_ThighTwist01": ((0.10, 0.0, 0.83), (0.11, 0.0, 0.75), "R_Thigh", False),
        "R_ThighTwist02": ((0.11, 0.0, 0.72), (0.11, 0.0, 0.64), "R_Thigh", False),
        "R_Calf": ((0.11, 0.0, 0.50), (0.11, 0.0, 0.12), "R_Thigh", True),
        "R_Foot": ((0.11, 0.0, 0.12), (0.11, 0.14, 0.06), "R_Calf", True),
        "R_ToeBase": ((0.11, 0.13, 0.06), (0.11, 0.22, 0.04), "R_Foot", True),
        "R_CalfTwist01": ((0.11, 0.0, 0.42), (0.11, 0.0, 0.34), "R_Calf", False),
        "R_CalfTwist02": ((0.11, 0.0, 0.30), (0.11, 0.0, 0.22), "R_Calf", False),
        "L_Thigh": ((-0.10, 0.0, 0.91), (-0.11, 0.0, 0.50), "Pelvis", True),
        "L_Calf": ((-0.11, 0.0, 0.50), (-0.11, 0.0, 0.12), "L_Thigh", True),
        "L_Foot": ((-0.11, 0.0, 0.12), (-0.11, 0.14, 0.06), "L_Calf", True),
        "L_ToeBase": ((-0.11, 0.13, 0.06), (-0.11, 0.22, 0.04), "L_Foot", True),
        "L_CalfTwist01": ((-0.11, 0.0, 0.42), (-0.11, 0.0, 0.34), "L_Calf", False),
        "L_CalfTwist02": ((-0.11, 0.0, 0.30), (-0.11, 0.0, 0.22), "L_Calf", False),
        "L_ThighTwist01": ((-0.10, 0.0, 0.83), (-0.11, 0.0, 0.75), "L_Thigh", False),
        "L_ThighTwist02": ((-0.11, 0.0, 0.72), (-0.11, 0.0, 0.64), "L_Thigh", False),
    }

    edit_bones = {}
    for name, (head, tail, _parent, deform) in bone_specs.items():
        bone = data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = deform
        edit_bones[name] = bone
    for name, (_head, _tail, parent_name, _deform) in bone_specs.items():
        if parent_name:
            edit_bones[name].parent = edit_bones[parent_name]
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    if len(data.bones) != len(CREW_BONES):
        raise RuntimeError(f"Procedural rig count mismatch: made {len(data.bones)} bones")
    return armature


def assign_procedural_weights(target, armature):
    """Softly blend vertices between nearby anatomical bone segments."""
    for group in list(target.vertex_groups):
        target.vertex_groups.remove(group)
    segment_specs = {
        "Root": ((0, 0, 0.03), (0, 0, 0.22), 0.15),
        "Pelvis": ((0, 0, 0.78), (0, 0, 1.00), 0.16),
        "Hip": ((0, 0, 0.82), (0, 0, 0.99), 0.15),
        "Waist": ((0, 0, 0.93), (0, 0, 1.17), 0.16),
        "Spine01": ((0, 0, 1.10), (0, 0, 1.34), 0.16),
        "Spine02": ((0, 0, 1.30), (0, 0, 1.49), 0.15),
        "NeckTwist02": ((0, 0, 1.49), (0, 0, 1.62), 0.11),
        "Head": ((0, 0, 1.58), (0, 0, 1.79), 0.12),
        "L_Clavicle": ((0, 0, 1.46), (-0.15, 0, 1.45), 0.11),
        "L_Upperarm": ((-0.15, 0, 1.45), (-0.18, 0, 1.17), 0.085),
        "L_Forearm": ((-0.18, 0, 1.17), (-0.19, 0, 0.94), 0.075),
        "L_Hand": ((-0.19, 0, 0.94), (-0.19, 0, 0.80), 0.075),
        "R_Clavicle": ((0, 0, 1.46), (0.15, 0, 1.45), 0.11),
        "R_Upperarm": ((0.15, 0, 1.45), (0.18, 0, 1.17), 0.085),
        "R_Forearm": ((0.18, 0, 1.17), (0.19, 0, 0.94), 0.075),
        "R_Hand": ((0.19, 0, 0.94), (0.19, 0, 0.80), 0.075),
        "L_Thigh": ((-0.10, 0, 0.91), (-0.11, 0, 0.50), 0.095),
        "L_Calf": ((-0.11, 0, 0.50), (-0.11, 0, 0.12), 0.080),
        "L_Foot": ((-0.11, 0, 0.12), (-0.11, 0.14, 0.06), 0.080),
        "L_ToeBase": ((-0.11, 0.13, 0.06), (-0.11, 0.22, 0.04), 0.070),
        "R_Thigh": ((0.10, 0, 0.91), (0.11, 0, 0.50), 0.095),
        "R_Calf": ((0.11, 0, 0.50), (0.11, 0, 0.12), 0.080),
        "R_Foot": ((0.11, 0, 0.12), (0.11, 0.14, 0.06), 0.080),
        "R_ToeBase": ((0.11, 0.13, 0.06), (0.11, 0.22, 0.04), 0.070),
    }
    vertex_count = len(target.data.vertices)
    coords = np.empty(vertex_count * 3, dtype=np.float32)
    target.data.vertices.foreach_get("co", coords)
    points = coords.reshape((-1, 3))
    names = list(segment_specs)
    weights = np.empty((vertex_count, len(names)), dtype=np.float32)
    for column, name in enumerate(names):
        start, end, sigma = segment_specs[name]
        start = np.asarray(start, dtype=np.float32)
        vector = np.asarray(end, dtype=np.float32) - start
        length_sq = max(float(np.dot(vector, vector)), 1.0e-8)
        t = np.clip(((points - start) @ vector) / length_sq, 0.0, 1.0)
        nearest = start + t[:, None] * vector
        distance_sq = np.sum((points - nearest) ** 2, axis=1)
        weights[:, column] = np.exp(-distance_sq / (2.0 * sigma * sigma))

    x = points[:, 0]
    z = points[:, 2]
    central = np.abs(x) < 0.17
    region_masks = {
        "Root": z < 0.24,
        "Pelvis": (np.abs(x) < 0.24) & (z > 0.63) & (z < 1.10),
        "Hip": central & (z > 0.72) & (z < 1.13),
        "Waist": central & (z > 0.86) & (z < 1.31),
        "Spine01": central & (z > 1.00) & (z < 1.44),
        "Spine02": central & (z > 1.20) & (z < 1.56),
        "NeckTwist02": (np.abs(x) < 0.20) & (z > 1.39) & (z < 1.70),
        "Head": (np.abs(x) < 0.20) & (z > 1.49),
        # The chest and belt extend beyond the pelvis centerline. Keep arm
        # groups outside that torso silhouette so a raised arm cannot drag
        # jacket or trouser vertices with it.
        "L_Clavicle": (x < -0.105) & (z > 1.32) & (z < 1.57),
        "L_Upperarm": (((x < -0.26) & (z > 1.14)) | ((x < -0.15) & (z > 1.42))) & (z < 1.55),
        "L_Forearm": (x < -0.25) & (z > 0.90) & (z < 1.27),
        "L_Hand": (x < -0.23) & (z > 0.72) & (z < 1.06),
        "R_Clavicle": (x > 0.105) & (z > 1.32) & (z < 1.57),
        "R_Upperarm": (((x > 0.26) & (z > 1.14)) | ((x > 0.15) & (z > 1.42))) & (z < 1.55),
        "R_Forearm": (x > 0.25) & (z > 0.90) & (z < 1.27),
        "R_Hand": (x > 0.23) & (z > 0.72) & (z < 1.06),
        # Split the legs at the center seam; overlapping hemispheres made
        # both thigh bones compete for the same cargo-trouser vertices.
        "L_Thigh": (x < -0.025) & (z > 0.32) & (z < 0.84),
        "L_Calf": (x < -0.025) & (z < 0.63),
        "L_Foot": (x < -0.04) & (z < 0.29),
        "L_ToeBase": (x < -0.04) & (z < 0.17),
        "R_Thigh": (x > 0.025) & (z > 0.32) & (z < 0.84),
        "R_Calf": (x > 0.025) & (z < 0.63),
        "R_Foot": (x > 0.04) & (z < 0.29),
        "R_ToeBase": (x > 0.04) & (z < 0.17),
    }
    for column, name in enumerate(names):
        weights[~region_masks[name], column] = 0.0

    # Four influences are sufficient for this human mesh and keep Blender/Unity
    # skinning efficient even though the source has nearly half a million vertices.
    top_count = 4
    top_indices = np.argpartition(weights, -top_count, axis=1)[:, -top_count:]
    top_mask = np.zeros(weights.shape, dtype=np.bool_)
    np.put_along_axis(top_mask, top_indices, True, axis=1)
    weights[~top_mask] = 0.0

    totals = weights.sum(axis=1)
    empty = totals < 1.0e-8
    remaining = empty.copy()
    fallback_bands = (
        ((z >= 1.52), "Head"),
        ((z >= 1.26), "Spine02"),
        ((z >= 1.00), "Spine01"),
        ((z >= 0.68), "Pelvis"),
        ((z >= 0.32) & (x < 0.0), "L_Thigh"),
        ((z >= 0.32) & (x >= 0.0), "R_Thigh"),
        ((z >= 0.12) & (x < 0.0), "L_Calf"),
        ((z >= 0.12) & (x >= 0.0), "R_Calf"),
        ((x < 0.0), "L_Foot"),
        ((x >= 0.0), "R_Foot"),
    )
    for condition, fallback_name in fallback_bands:
        selected = remaining & condition
        if np.any(selected):
            weights[selected, names.index(fallback_name)] = 1.0
            remaining[selected] = False
    totals = weights.sum(axis=1)
    if np.any(totals < 1.0e-8):
        raise RuntimeError("Some worker vertices could not be assigned to a deform bone")
    weights /= totals[:, None]

    groups = {bone.name: target.vertex_groups.new(name=bone.name) for bone in armature.data.bones}
    covered = np.zeros(vertex_count, dtype=np.bool_)
    for column, name in enumerate(names):
        values = weights[:, column]
        # VertexGroup.add accepts a scalar weight per call, so quantize to 2%
        # buckets and submit each bucket as one native batch.
        quantized = np.rint(values * 50.0).astype(np.uint8)
        for bucket in np.unique(quantized):
            if bucket == 0:
                continue
            indices = np.flatnonzero(quantized == bucket)
            if indices.size:
                groups[name].add(indices.tolist(), float(bucket) / 50.0, "REPLACE")
                covered[indices] = True
        if (column + 1) % 6 == 0:
            log(f"Weight groups assigned: {column + 1}/{len(names)}")

    modifier = target.modifiers.new("Armature | rig anatômico 41 ossos", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = False
    coverage = float(covered.mean()) if vertex_count else 0.0
    log(f"Procedural skin: {vertex_count:,} vertices; four influences max; coverage {coverage:.1%}")
    if coverage < 0.98:
        raise RuntimeError(f"Procedural skin coverage is unexpectedly low: {coverage:.1%}")


def set_world_axis_rotation(armature, pose_bone, axis, degrees):
    """Apply a rotation around a chosen armature-space axis, irrespective of bone roll."""
    axis = Vector(axis).normalized()
    bone = pose_bone.bone
    if bone.parent:
        parent_rest = bone.parent.matrix_local.to_3x3().to_quaternion()
        local_rest = (
            bone.parent.matrix_local.inverted() @ bone.matrix_local
        ).to_3x3().to_quaternion()
    else:
        parent_rest = Quaternion()
        local_rest = bone.matrix_local.to_3x3().to_quaternion()
    axis_in_parent = parent_rest.inverted() @ axis
    delta_in_parent = Quaternion(axis_in_parent, math.radians(degrees))
    delta_in_bone = local_rest.inverted() @ delta_in_parent @ local_rest
    pose_bone.rotation_mode = "QUATERNION"
    pose_bone.rotation_quaternion = delta_in_bone @ pose_bone.rotation_quaternion


def translation_in_bone_space(pose_bone, armature_vector):
    return pose_bone.bone.matrix_local.to_3x3().inverted() @ Vector(armature_vector)


def make_material_from_texture(texture_path):
    image = bpy.data.images.load(texture_path, check_existing=True)
    image.colorspace_settings.name = "sRGB"
    material = bpy.data.materials.new("Conves | uniforme amarelo e equipamento de convés")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
    output = next((node for node in nodes if node.type == "OUTPUT_MATERIAL"), None)
    if shader is None:
        shader = nodes.new("ShaderNodeBsdfPrincipled")
    if output is None:
        output = nodes.new("ShaderNodeOutputMaterial")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.label = "Textura original do uniforme do trabalhador de convés"
    coordinates = nodes.new("ShaderNodeTexCoord")
    links.new(coordinates.outputs["UV"], texture.inputs["Vector"])
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    if not output.inputs["Surface"].is_linked:
        links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Roughness"].default_value = 0.78
    if shader.inputs.get("Alpha"):
        shader.inputs["Alpha"].default_value = 1.0
    material.diffuse_color = (0.82, 0.48, 0.08, 1.0)
    return material


def transfer_weights(source, target, armature):
    for group in source.vertex_groups:
        if target.vertex_groups.get(group.name) is None:
            target.vertex_groups.new(name=group.name)
    for bone in armature.data.bones:
        if target.vertex_groups.get(bone.name) is None:
            target.vertex_groups.new(name=bone.name)

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = target
    target.select_set(True)
    transfer = target.modifiers.new("Rig weights | avatar do trabalhador", "DATA_TRANSFER")
    transfer.object = source
    transfer.use_vert_data = True
    transfer.data_types_verts = {"VGROUP_WEIGHTS"}
    transfer.vert_mapping = "POLYINTERP_NEAREST"
    transfer.layers_vgroup_select_src = "ALL"
    transfer.layers_vgroup_select_dst = "NAME"
    transfer.mix_mode = "REPLACE"
    transfer.mix_factor = 1.0
    bpy.ops.object.modifier_apply(modifier=transfer.name)

    bpy.ops.object.mode_set(mode="OBJECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    armature_modifier = target.modifiers.new("Armature | Avatar Ani conves", "ARMATURE")
    armature_modifier.object = armature
    armature_modifier.use_deform_preserve_volume = False

    membership_count = 0
    for vertex in target.data.vertices:
        if any(assignment.weight > 0.0001 for assignment in vertex.groups):
            membership_count += 1
    coverage = membership_count / max(1, len(target.data.vertices))
    log(f"Weight transfer: {membership_count:,}/{len(target.data.vertices):,} vertices ({coverage:.1%})")
    if coverage < 0.95:
        raise RuntimeError(f"Rig transfer coverage is unexpectedly low: {coverage:.1%}")


def sanitize_transferred_arm_weights(target):
    """Keep transferred arm influences on the corresponding arm silhouette."""
    count = len(target.data.vertices)
    coords = np.empty(count * 3, dtype=np.float32)
    target.data.vertices.foreach_get("co", coords)
    points = coords.reshape((-1, 3))
    x = points[:, 0]
    z = points[:, 2]
    masks = {}
    for side_name, sign in (("L", 1.0), ("R", -1.0)):
        lateral = sign * x
        masks[f"{side_name}_Clavicle"] = (lateral > 0.09) & (z > 1.30) & (z < 1.60)
        upper = (((lateral > 0.22) & (z > 1.10)) | ((lateral > 0.13) & (z > 1.41))) & (z < 1.57)
        forearm = (lateral > 0.21) & (z > 0.88) & (z < 1.31)
        hand = (lateral > 0.18) & (z > 0.70) & (z < 1.10)
        masks[f"{side_name}_Upperarm"] = upper
        masks[f"{side_name}_UpperarmTwist01"] = upper
        masks[f"{side_name}_UpperarmTwist02"] = upper
        masks[f"{side_name}_Forearm"] = forearm
        masks[f"{side_name}_ForearmTwist01"] = forearm
        masks[f"{side_name}_ForearmTwist02"] = forearm
        masks[f"{side_name}_Hand"] = hand

    group_names = {group.index: group.name for group in target.vertex_groups}
    invalid = {name: [] for name in masks}
    retained = np.zeros(count, dtype=np.float32)
    for vertex in target.data.vertices:
        for assignment in vertex.groups:
            name = group_names[assignment.group]
            if name in masks and not masks[name][vertex.index]:
                invalid[name].append(vertex.index)
            else:
                retained[vertex.index] += assignment.weight

    removed_count = 0
    for name, indices in invalid.items():
        if indices:
            target.vertex_groups[name].remove(indices)
            removed_count += len(indices)

    fallback = {}
    for index in np.flatnonzero(retained < 1.0e-5):
        point_z = float(z[index])
        side = "L" if x[index] >= 0.0 else "R"
        if point_z >= 1.53:
            name = "Head"
        elif point_z >= 1.26:
            name = "Spine02"
        elif point_z >= 1.00:
            name = "Spine01"
        elif point_z >= 0.73:
            name = "Pelvis"
        elif point_z >= 0.32:
            name = f"{side}_Thigh"
        elif point_z >= 0.12:
            name = f"{side}_Calf"
        else:
            name = f"{side}_Foot"
        fallback.setdefault(name, []).append(int(index))
    for name, indices in fallback.items():
        target.vertex_groups[name].add(indices, 1.0, "REPLACE")

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    log(
        f"Arm influence bounds: removed {removed_count:,} out-of-region assignments; "
        f"restored {sum(map(len, fallback.values())):,} fallback vertices"
    )


def import_prefab_avatar_and_weight_source():
    """Use the exact 41-bone Avatar already assigned on the worker prefab."""
    imported = import_fbx_without_source_animation(AVATAR_FBX)
    armatures = [obj for obj in imported if obj.type == "ARMATURE"]
    meshes = [obj for obj in imported if obj.type == "MESH" and obj.name != "Cube"]
    if len(armatures) != 1 or not meshes:
        raise RuntimeError(
            f"Expected one prefab Avatar rig and its weighted meshes; "
            f"found {len(armatures)} armatures and {len(meshes)} meshes"
        )
    armature = armatures[0]
    missing_bones = sorted(set(CREW_BONES) - {bone.name for bone in armature.data.bones})
    if missing_bones:
        raise RuntimeError(f"The prefab Avatar is missing expected bones: {missing_bones}")

    bpy.context.view_layer.update()
    avatar_points = [
        obj.matrix_world @ vertex.co
        for obj in meshes
        for vertex in obj.data.vertices
    ]
    low = min(point.z for point in avatar_points)
    high = max(point.z for point in avatar_points)
    avatar_height = high - low
    if not 0.5 <= avatar_height <= 2.5:
        raise RuntimeError(f"The prefab Avatar height is implausible: {avatar_height:.3f}m")
    normalization = Matrix.Scale(HUMAN_HEIGHT / avatar_height, 4)

    # Bake source meshes into the target's centered, Z-up, 1.8m space before
    # nearest-surface transfer. The imported FBX has a centimetre-scale object
    # transform, while its geometry and the target FBX use different containers.
    for obj in meshes:
        for modifier in list(obj.modifiers):
            obj.modifiers.remove(modifier)
        bake_world_transform_into_data(obj, normalization @ obj.matrix_world.copy())
    source = join_meshes(meshes, "Avatar do prefab | fonte de pesos")

    # Apply the Avatar object's original FBX axes and scale to the rest bones so
    # the rig has identity transforms and matches the normalized target mesh.
    armature_world = normalization @ armature.matrix_world.copy()
    armature.parent = None
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in armature.data.edit_bones:
        bone.transform(armature_world, scale=True, roll=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.matrix_world = Matrix.Identity(4)
    armature.name = "DeckCrew | Avatar original do prefab (41 ossos)"
    armature.data.name = "DeckCrew | Avatar original do prefab"
    bpy.context.view_layer.update()

    log(
        f"Loaded exact prefab Avatar: {len(armature.data.bones)} bones, "
        f"{len(source.data.vertices):,} weighted source vertices, "
        f"source height {avatar_height:.3f}m -> {HUMAN_HEIGHT:.2f}m"
    )
    return armature, source


def create_action(armature, name, keyframes, cyclic=False):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action["crew_action"] = name
    action["description"] = ACTION_DESCRIPTIONS[name]
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action

    for frame, pose in keyframes:
        for pose_bone in armature.pose.bones:
            pose_bone.rotation_mode = "QUATERNION"
            pose_bone.rotation_quaternion = Quaternion()
            pose_bone.location = (0.0, 0.0, 0.0)

        for bone_name, axis_angles in pose.get("rotations", {}).items():
            pose_bone = armature.pose.bones.get(bone_name)
            if pose_bone is None:
                raise RuntimeError(f"Action {name} requires missing bone {bone_name}")
            for axis, degrees in axis_angles:
                set_world_axis_rotation(armature, pose_bone, axis, degrees)

        for bone_name, translation in pose.get("translations", {}).items():
            pose_bone = armature.pose.bones.get(bone_name)
            if pose_bone is None:
                raise RuntimeError(f"Action {name} requires missing bone {bone_name}")
            pose_bone.location = translation_in_bone_space(pose_bone, translation)

        for pose_bone in armature.pose.bones:
            pose_bone.keyframe_insert(
                data_path="rotation_quaternion", frame=frame, group=pose_bone.name
            )
            pose_bone.keyframe_insert(
                data_path="location", frame=frame, group=pose_bone.name
            )

    action.use_frame_range = True
    action.frame_start = keyframes[0][0]
    action.frame_end = keyframes[-1][0]
    action.use_cyclic = cyclic
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                channelbag = strip.channelbag(slot)
                if channelbag is None:
                    continue
                for fcurve in channelbag.fcurves:
                    for key in fcurve.keyframe_points:
                        key.interpolation = "BEZIER"
                        key.handle_left_type = "AUTO_CLAMPED"
                        key.handle_right_type = "AUTO_CLAMPED"
    return action


ACTION_DESCRIPTIONS = {
    "Walk_Deck": "Passo natural, equilibrado e atento no convés; ciclo contínuo.",
    "Kneel_Ready": "Ajoelha em posição estável para preparar uma operação de lançamento.",
    "Kneel_LaunchSignal": "Ajoelhado, faz um sinal amplo e decidido de liberação para voo.",
    "Marshal_Stop": "Sinaliza parada com ambos os braços abertos e as mãos erguidas.",
    "Marshal_TurnLeft": "Indica mudança de direção para a esquerda com gesto de braço.",
    "Marshal_TurnRight": "Indica mudança de direção para a direita com gesto de braço.",
    "Check_Catapult": "Inspeciona o trilho da catapulta e aponta para o equipamento.",
    "Clear_Deck_Wave": "Faz sinal de convés livre com varredura ampla dos braços.",
}


def build_actions(armature):
    R = lambda **items: {"rotations": items}
    T = lambda bone="Root", xyz=(0.0, 0.0, 0.0): {"translations": {bone: xyz}}
    M = lambda rotations=None, translation=(0.0, 0.0, 0.0): {
        "rotations": rotations or {}, "translations": {"Root": translation}
    }
    front = (0.0, -1.0, 0.0)
    side = (1.0, 0.0, 0.0)
    up = (0.0, 0.0, 1.0)
    arm_lift = (0.0, 1.0, 0.0)

    poses = {
        "Walk_Deck": (
            [
                (1, M()),
                (7, M({"L_Thigh": [(side, 20)], "R_Thigh": [(side, -20)],
                       "L_Calf": [(side, -12)], "R_Calf": [(side, 20)],
                       "L_Upperarm": [(side, -14)], "R_Upperarm": [(side, 14)],
                       "L_Forearm": [(side, -5)], "R_Forearm": [(side, 5)],
                       "Waist": [(side, 2)], "Head": [(side, -1)]}, (0, 0, 0.035))),
                (13, M()),
                (19, M({"L_Thigh": [(side, -20)], "R_Thigh": [(side, 20)],
                        "L_Calf": [(side, 20)], "R_Calf": [(side, -12)],
                        "L_Upperarm": [(side, 14)], "R_Upperarm": [(side, -14)],
                        "L_Forearm": [(side, 5)], "R_Forearm": [(side, -5)],
                        "Waist": [(side, -2)], "Head": [(side, 1)]}, (0, 0, 0.0))),
                (25, M()),
            ], True
        ),
        "Kneel_Ready": (
            [
                (1, M()),
                (10, M({"L_Thigh": [(side, 4)], "R_Thigh": [(side, 30)],
                        "L_Calf": [(side, 30)], "R_Calf": [(side, -5)],
                        "Waist": [(side, 5)], "Spine01": [(side, 3)],
                        "Head": [(side, -4)]}, (0.0, -0.02, -0.16))),
                (22, M({"L_Thigh": [(side, 0)], "R_Thigh": [(side, 58)],
                        "L_Calf": [(side, 90)], "R_Calf": [(side, -10)],
                        "Waist": [(side, 8)], "Spine01": [(side, 5)],
                        "Head": [(side, -7)]}, (0.0, -0.04, -0.50))),
                (30, M({"L_Thigh": [(side, 0)], "R_Thigh": [(side, 58)],
                        "L_Calf": [(side, 90)], "R_Calf": [(side, -10)],
                        "L_Foot": [(side, 4)], "R_Foot": [(side, 4)],
                        "Waist": [(side, 8)], "Spine01": [(side, 5)],
                        "Head": [(side, -6)]}, (0.0, -0.04, -0.50))),
            ], False
        ),
        "Kneel_LaunchSignal": (
            [
                (1, M()),
                (9, M({"L_Thigh": [(side, 4)], "R_Thigh": [(side, 30)],
                       "L_Calf": [(side, 30)], "R_Calf": [(side, -5)],
                       "Waist": [(side, 5)]}, (0.0, -0.02, -0.16))),
                (17, M({"L_Thigh": [(side, 0)], "R_Thigh": [(side, 58)],
                        "L_Calf": [(side, 90)], "R_Calf": [(side, -10)],
                        "L_Foot": [(side, 4)], "R_Foot": [(side, 4)],
                        "Waist": [(side, 8)], "Spine01": [(side, 5)],
                        "Head": [(side, -7)]}, (0.0, -0.04, -0.50))),
                (21, M({"L_Thigh": [(side, 0)], "R_Thigh": [(side, 58)],
                        "L_Calf": [(side, 90)], "R_Calf": [(side, -10)],
                        "L_Foot": [(side, 4)], "R_Foot": [(side, 4)],
                        "R_Upperarm": [(arm_lift, 65)],
                        "R_Forearm": [(side, -32)], "R_Hand": [(side, -12)],
                        "L_Upperarm": [(side, -12)], "L_Forearm": [(side, 18)],
                        "Waist": [(side, 7)], "Head": [(side, -3)]}, (0.0, -0.04, -0.50))),
                (26, M({"L_Thigh": [(side, 0)], "R_Thigh": [(side, 58)],
                        "L_Calf": [(side, 90)], "R_Calf": [(side, -10)],
                        "L_Foot": [(side, 4)], "R_Foot": [(side, 4)],
                        "R_Upperarm": [(arm_lift, 125)],
                        "R_Forearm": [(side, -32)], "R_Hand": [(side, 8)],
                        "L_Upperarm": [(side, -8)], "L_Forearm": [(side, 15)],
                        "Waist": [(side, 7)], "Head": [(side, -2)]}, (0.0, -0.04, -0.50))),
                (34, M({"L_Thigh": [(side, 0)], "R_Thigh": [(side, 58)],
                        "L_Calf": [(side, 90)], "R_Calf": [(side, -10)],
                        "L_Foot": [(side, 4)], "R_Foot": [(side, 4)],
                        "R_Upperarm": [(arm_lift, 125)],
                        "R_Forearm": [(side, -32)], "R_Hand": [(side, 8)],
                        "L_Upperarm": [(side, -8)], "L_Forearm": [(side, 15)],
                        "Waist": [(side, 7)], "Head": [(side, -2)]}, (0.0, -0.04, -0.50))),
            ], False
        ),
        "Marshal_Stop": (
            [
                (1, M()),
                (8, R(R_Upperarm=[(arm_lift, 28)], L_Upperarm=[(arm_lift, -28)],
                      R_Forearm=[(front, -22)], L_Forearm=[(front, 22)],
                      R_Hand=[(front, -8)], L_Hand=[(front, 8)], Spine01=[(side, 4)])),
                (16, R(R_Upperarm=[(arm_lift, 55)], L_Upperarm=[(arm_lift, -55)],
                       R_Forearm=[(front, -24)], L_Forearm=[(front, 24)],
                       R_Hand=[(front, -12)], L_Hand=[(front, 12)], Head=[(side, -2)])),
                (24, R(R_Upperarm=[(arm_lift, 55)], L_Upperarm=[(arm_lift, -55)],
                       R_Forearm=[(front, -24)], L_Forearm=[(front, 24)],
                       R_Hand=[(front, -12)], L_Hand=[(front, 12)], Head=[(side, -2)])),
            ], False
        ),
        "Marshal_TurnLeft": (
            [
                (1, M()),
                (8, R(L_Upperarm=[(arm_lift, -22)], L_Forearm=[(front, -14)],
                      R_Upperarm=[(arm_lift, 16)], R_Forearm=[(front, -35)],
                      Waist=[(up, 8)], Head=[(up, 6)])),
                (15, R(L_Upperarm=[(arm_lift, -40)], L_Forearm=[(front, -12)],
                       R_Upperarm=[(arm_lift, 26)], R_Forearm=[(front, -42)],
                       Waist=[(up, 14)], Spine01=[(up, 5)], Head=[(up, 10)])),
                (23, R(L_Upperarm=[(arm_lift, -40)], L_Forearm=[(front, -12)],
                       R_Upperarm=[(arm_lift, 26)], R_Forearm=[(front, -42)],
                       Waist=[(up, 14)], Head=[(up, 10)])),
            ], False
        ),
        "Marshal_TurnRight": (
            [
                (1, M()),
                (8, R(R_Upperarm=[(arm_lift, 22)], R_Forearm=[(front, 14)],
                      L_Upperarm=[(arm_lift, -16)], L_Forearm=[(front, 35)],
                      Waist=[(up, -8)], Head=[(up, -6)])),
                (15, R(R_Upperarm=[(arm_lift, 40)], R_Forearm=[(front, 12)],
                       L_Upperarm=[(arm_lift, -26)], L_Forearm=[(front, 42)],
                       Waist=[(up, -14)], Spine01=[(up, -5)], Head=[(up, -10)])),
                (23, R(R_Upperarm=[(arm_lift, 40)], R_Forearm=[(front, 12)],
                       L_Upperarm=[(arm_lift, -26)], L_Forearm=[(front, 42)],
                       Waist=[(up, -14)], Head=[(up, -10)])),
            ], False
        ),
        "Check_Catapult": (
            [
                (1, M()),
                (9, M({"Hip": [(side, 5)], "Waist": [(side, 11)],
                       "Spine01": [(side, 9)], "Head": [(side, -20)],
                       "R_Upperarm": [(side, 22)], "R_Forearm": [(side, -34)],
                       "R_Hand": [(side, 10)], "L_Upperarm": [(side, -10)],
                       "L_Forearm": [(side, 18)]}, (0.0, -0.03, -0.04))),
                (17, M({"Hip": [(side, 7)], "Waist": [(side, 14)],
                        "Spine01": [(side, 11)], "Head": [(side, -23)],
                        "R_Upperarm": [(side, 25)], "R_Forearm": [(side, -46)],
                        "R_Hand": [(side, 12)], "L_Upperarm": [(side, -9)],
                        "L_Forearm": [(side, 20)]}, (0.0, -0.04, -0.05))),
                (26, M({"Hip": [(side, 3)], "Waist": [(side, 7)],
                        "Head": [(side, -12)], "R_Upperarm": [(side, 14)],
                        "R_Forearm": [(side, -22)], "L_Upperarm": [(side, -6)]},
                       (0.0, -0.02, -0.02))),
                (32, M()),
            ], False
        ),
        "Clear_Deck_Wave": (
            [
                (1, M()),
                (8, R(R_Upperarm=[(arm_lift, 58)], R_Forearm=[(side, -48)],
                      R_Hand=[(front, 24)], L_Upperarm=[(arm_lift, -25)],
                      L_Forearm=[(side, 18)], Waist=[(up, -4)])),
                (15, R(R_Upperarm=[(arm_lift, 72)], R_Forearm=[(side, -42)],
                       R_Hand=[(front, -24)], L_Upperarm=[(arm_lift, -45)],
                       L_Forearm=[(side, -18)], Waist=[(up, 4)], Head=[(side, -3)])),
                (22, R(R_Upperarm=[(arm_lift, 58)], R_Forearm=[(side, -48)],
                       R_Hand=[(front, 24)], L_Upperarm=[(arm_lift, -25)],
                       L_Forearm=[(side, 18)], Waist=[(up, -4)])),
                (30, R(R_Upperarm=[(arm_lift, 72)], R_Forearm=[(side, -42)],
                       R_Hand=[(front, -24)], L_Upperarm=[(arm_lift, -45)],
                       L_Forearm=[(side, -18)], Waist=[(up, 4)], Head=[(side, -3)])),
                (38, M()),
            ], False
        ),
    }

    actions = {}
    for name, (keyframes, loop) in poses.items():
        actions[name] = create_action(armature, name, keyframes, cyclic=loop)
        log(f"Created action {name}: {len(keyframes)} poses; {ACTION_DESCRIPTIONS[name]}")
    armature.animation_data.action = None
    return actions


def create_worker_root(armature, target):
    root = bpy.data.objects.new("Trabalhador_Conves | root em metros", None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.35
    root.location = (0.0, 0.0, 0.0)
    root.rotation_euler = (0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)

    armature.parent = root
    armature.matrix_parent_inverse = Matrix.Identity(4)
    armature.matrix_world = Matrix.Identity(4)
    target.parent = armature
    target.matrix_parent_inverse = Matrix.Identity(4)
    target.matrix_world = Matrix.Identity(4)
    root["source_prefab"] = WORKER_PREFAB
    root["human_height_m"] = HUMAN_HEIGHT
    root["skeleton_source"] = "Avatar original de 41 ossos referenciado pelo prefab do trabalhador"
    return root


def create_ship_root(imported_objects):
    root = bpy.data.objects.new("USS Enterprise CVN-65 | escala do prefab Unity", None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 15.0
    root.scale = ENTERPRISE_PREFAB_SCALE
    root["source_prefab"] = os.path.join(
        PROJECT, "Assets", "Prefabs", "Navios_Guerra", "Porta avioes", "Uss Enterprise.prefab"
    )
    root["source_model"] = ENTERPRISE_GLB
    for obj in imported_objects:
        if obj.parent is not None:
            continue
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    return root


def cast_to_enterprise_deck(objects, x, y):
    origin = Vector((x, y, 500.0))
    direction = Vector((0.0, 0.0, -1.0))
    hits = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        inverse = evaluated.matrix_world.inverted()
        local_origin = inverse @ origin
        local_direction = (inverse.to_3x3() @ direction).normalized()
        hit, location, _normal, _face = evaluated.ray_cast(local_origin, local_direction)
        if hit:
            world_location = evaluated.matrix_world @ location
            if world_location.z > 0.0:
                hits.append(world_location.z)
    if not hits:
        raise RuntimeError(f"Could not raycast the Enterprise deck at x={x:.1f}, y={y:.1f}")
    return max(hits)


def look_at(obj, point):
    direction = Vector(point) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_camera(name, location, target, lens=48.0):
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(camera)
    camera.location = location
    data.lens = lens
    data.clip_end = 3000.0
    look_at(camera, target)
    return camera


def add_area_light(name, location, target, power, color, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.color = color
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    look_at(light, target)
    return light


def configure_render(scene, camera, width=1280, height=720):
    engine_ids = {
        item.identifier
        for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items
    }
    if "BLENDER_EEVEE" in engine_ids:
        scene.render.engine = "BLENDER_EEVEE"
    elif "BLENDER_EEVEE_NEXT" in engine_ids:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    else:
        raise RuntimeError(f"No supported realtime Blender render engine found: {engine_ids}")
    scene.camera = camera
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.render.engine = scene.render.engine
    scene.render.image_settings.compression = 18
    scene.render.resolution_percentage = 100
    scene.render.filepath = os.path.join(STILLS_DIR, "render.png")
    scene.render.fps = 24
    scene.render.image_settings.color_mode = "RGB"
    if hasattr(scene, "eevee"):
        eevee = scene.eevee
        if hasattr(eevee, "taa_render_samples"):
            eevee.taa_render_samples = 16
        if hasattr(eevee, "use_gtao"):
            eevee.use_gtao = True
        if hasattr(eevee, "gtao_distance"):
            eevee.gtao_distance = 3.0
        if hasattr(eevee, "gtao_factor"):
            eevee.gtao_factor = 1.18
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 1.0
    scene.view_settings.gamma = 1.0
    scene.world = bpy.data.worlds.new("Daylight | oceano claro") if scene.world is None else scene.world
    scene.world.use_nodes = True
    background = next(
        (node for node in scene.world.node_tree.nodes if node.type == "BACKGROUND"), None
    )
    if background is None:
        background = scene.world.node_tree.nodes.new("ShaderNodeBackground")
    background.inputs["Color"].default_value = (0.16, 0.23, 0.31, 1.0)
    background.inputs["Strength"].default_value = 0.55


def render_still(scene, filepath, frame):
    scene.frame_set(frame)
    scene.render.filepath = filepath
    bpy.ops.render.render(write_still=True)
    log(f"Rendered still {os.path.basename(filepath)} at frame {frame}")


def export_fbx(worker_root, armature, target):
    os.makedirs(FBX_OUTPUT_DIR, exist_ok=True)
    worker_root.location = (0.0, 0.0, 0.0)
    worker_root.rotation_euler = (0.0, 0.0, 0.0)
    worker_root.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (worker_root, armature, target):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    props = bpy.ops.export_scene.fbx.get_rna_type().properties
    supported = {prop.identifier for prop in props}
    requested = {
        "filepath": FBX_OUTPUT,
        "use_selection": True,
        "object_types": {"EMPTY", "ARMATURE", "MESH"},
        "apply_scale_options": "FBX_SCALE_ALL",
        "axis_forward": "-Z",
        "axis_up": "Y",
        "use_mesh_modifiers": True,
        "mesh_smooth_type": "FACE",
        "use_armature_deform_only": False,
        "add_leaf_bones": False,
        "bake_anim": True,
        "bake_anim_use_all_actions": True,
        "bake_anim_use_nla_strips": False,
        "bake_anim_force_startend_keying": True,
        "bake_anim_step": 1.0,
        "bake_anim_simplify_factor": 0.0,
    }
    options = {key: value for key, value in requested.items() if key in supported}
    log(f"Exporting FBX with supported options: {sorted(options.keys())}")
    bpy.ops.export_scene.fbx(**options)
    if not os.path.isfile(FBX_OUTPUT) or os.path.getsize(FBX_OUTPUT) < 100_000:
        raise RuntimeError("FBX export is missing or unexpectedly small")
    log(f"Exported animation FBX: {FBX_OUTPUT} ({os.path.getsize(FBX_OUTPUT):,} bytes)")


def build_nla_sequence(armature, actions, scene):
    anim = armature.animation_data
    anim.action = None
    for track in list(anim.nla_tracks):
        anim.nla_tracks.remove(track)
    track = anim.nla_tracks.new()
    track.name = "Convés Enterprise | sequência de demonstração"
    cursor = 1
    launch_frame = 1
    action_order = (
        "Walk_Deck", "Kneel_Ready", "Kneel_LaunchSignal", "Marshal_Stop",
        "Marshal_TurnLeft", "Marshal_TurnRight", "Check_Catapult", "Clear_Deck_Wave",
    )
    for name in action_order:
        action = actions[name]
        strip = track.strips.new(name, cursor, action)
        strip.blend_type = "REPLACE"
        strip.extrapolation = "NOTHING"
        strip.use_animated_influence = False
        strip.action_frame_start = action.frame_range[0]
        strip.action_frame_end = action.frame_range[1]
        scene.timeline_markers.new(name, frame=cursor)
        if name == "Kneel_LaunchSignal":
            launch_frame = cursor + int((action.frame_range[1] - action.frame_range[0]) * 0.76)
        cursor = int(round(strip.frame_end)) + 2
    anim.use_nla = True
    scene.frame_start = 1
    scene.frame_end = cursor
    return launch_frame


def make_animation_video(scene):
    frames_dir = os.path.join(OUTPUT_DIR, "video_frames")
    os.makedirs(frames_dir, exist_ok=True)
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.render.fps = 24
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = os.path.join(frames_dir, "frame_")
    log(f"Rendering animation frames {scene.frame_start}-{scene.frame_end} to PNG")
    bpy.ops.render.render(animation=True)
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        log("FFmpeg is unavailable; the complete numbered PNG sequence is retained for Blender preview")
        return
    pattern = os.path.join(frames_dir, "frame_%04d.png")
    command = [
        ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
        "-framerate", "24", "-start_number", "1", "-i", pattern,
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "22",
        "-pix_fmt", "yuv420p", VIDEO_OUTPUT,
    ]
    subprocess.run(command, check=True)
    if not os.path.isfile(VIDEO_OUTPUT) or os.path.getsize(VIDEO_OUTPUT) < 100_000:
        raise RuntimeError("MP4 preview was not produced or is unexpectedly small")
    log(f"Encoded MP4 preview: {VIDEO_OUTPUT} ({os.path.getsize(VIDEO_OUTPUT):,} bytes)")


def main():
    started = time.time()
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    os.makedirs(STILLS_DIR, exist_ok=True)
    for path in (WORKER_PREFAB, WORKER_MESH, UNIFORM_TEXTURE, ENTERPRISE_GLB):
        require_file(path)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 24

    log("Importing the exact high-poly yellow-jersey worker mesh from the carrier prefab")
    worker_import = import_fbx_without_source_animation(WORKER_MESH)
    log(f"Worker FBX import returned {len(worker_import)} scene objects")
    worker_meshes = [obj for obj in worker_import if obj.type == "MESH"]
    if not worker_meshes:
        raise RuntimeError("The target worker FBX did not produce a visible mesh")
    target_world_matrices = {obj: obj.matrix_world.copy() for obj in worker_meshes}
    for obj in worker_meshes:
        for modifier in list(obj.modifiers):
            obj.modifiers.remove(modifier)
        bake_world_transform_into_data(obj, target_world_matrices[obj])
    target = join_meshes(worker_meshes, "Trabalhador_Conves | malha original amarela")
    target_low, target_high = mesh_bounds([target])
    target_height = target_high.z - target_low.z
    if target_height <= 0.1:
        raise RuntimeError(f"Target worker mesh height is invalid: {target_height:.4f}m")
    target_mid = (target_low + target_high) * 0.5
    world_scale = HUMAN_HEIGHT / target_height
    normalize = (
        Matrix.Translation(Vector((0.0, 0.0, -target_low.z * world_scale)))
        @ Matrix.Scale(world_scale, 4)
        @ Matrix.Translation(Vector((-target_mid.x, -target_mid.y, 0.0)))
    )
    target.data.transform(normalize)
    target.matrix_world = Matrix.Identity(4)
    for polygon in target.data.polygons:
        polygon.use_smooth = True
    log(f"Normalized target mesh to {HUMAN_HEIGHT:.2f}m; loading the prefab's exact Avatar")
    armature, weight_source = import_prefab_avatar_and_weight_source()
    if "--avatar-mesh-preview" in sys.argv:
        high_resolution_mesh, high_resolution_data = target, target.data
        target = weight_source
        target.name = "Trabalhador_Conves | malha original do Avatar do prefab"
        original_part_names = [material.name if material else "" for material in target.data.materials]
        polygon_part_names = [
            original_part_names[poly.material_index]
            if poly.material_index < len(original_part_names) else ""
            for poly in target.data.polygons
        ]
        bpy.data.objects.remove(high_resolution_mesh, do_unlink=True)
        if high_resolution_data.users == 0:
            bpy.data.meshes.remove(high_resolution_data)
        target.data.materials.clear()
        palette = {
            "DeckCrew | camisa amarela": (0.68, 0.36, 0.065, 1.0),
            "DeckCrew | calça azul-marinho": (0.025, 0.032, 0.040, 1.0),
            "DeckCrew | equipamento escuro": (0.060, 0.065, 0.070, 1.0),
        }
        palette_materials = {}
        for name, color in palette.items():
            material = bpy.data.materials.new(name)
            material.diffuse_color = color
            material.use_nodes = True
            shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
            shader.inputs["Base Color"].default_value = color
            shader.inputs["Roughness"].default_value = 0.82
            palette_materials[name] = material
            target.data.materials.append(material)
        part_palette = {
            0: "DeckCrew | camisa amarela", 13: "DeckCrew | camisa amarela",
            8: "DeckCrew | camisa amarela", 9: "DeckCrew | camisa amarela",
            7: "DeckCrew | camisa amarela", 6: "DeckCrew | equipamento escuro",
            5: "DeckCrew | calça azul-marinho",
            4: "DeckCrew | equipamento escuro", 10: "DeckCrew | equipamento escuro",
            1: "DeckCrew | equipamento escuro", 2: "DeckCrew | calça azul-marinho",
            3: "DeckCrew | calça azul-marinho", 11: "DeckCrew | calça azul-marinho",
            12: "DeckCrew | calça azul-marinho",
        }
        part_to_palette = {}
        for part_name in original_part_names:
            try:
                part_index = int(part_name.split("_")[2])
            except (IndexError, ValueError):
                part_index = -1
            part_to_palette[part_name] = part_palette.get(part_index, "DeckCrew | equipamento escuro")
        for polygon, part_name in zip(target.data.polygons, polygon_part_names):
            palette_name = part_to_palette.get(part_name, "DeckCrew | equipamento escuro")
            polygon.material_index = list(palette).index(palette_name)
            polygon.use_smooth = True
        modifier = target.modifiers.new("Armature | Avatar Ani conves", "ARMATURE")
        modifier.object = armature
        modifier.use_deform_preserve_volume = False
        log("Previewing the prefab Avatar's own weighted mesh with deck-crew uniform materials")
    else:
        target.data.materials.clear()
        target.data.materials.append(make_material_from_texture(UNIFORM_TEXTURE))
        log("Transferring the original Avatar skin weights to the high-resolution yellow worker mesh")
        transfer_weights(weight_source, target, armature)
        sanitize_transferred_arm_weights(target)
        source_mesh_data = weight_source.data
        bpy.data.objects.remove(weight_source, do_unlink=True)
        if source_mesh_data.users == 0:
            bpy.data.meshes.remove(source_mesh_data)
    target["source_mesh"] = WORKER_MESH
    target["source_material_texture"] = UNIFORM_TEXTURE
    target["skin_weights"] = "Transferred from the exact 41-bone Avatar referenced by the worker prefab"

    worker_root = create_worker_root(armature, target)
    log(
        f"Worker rig prepared from the prefab Avatar: {len(armature.data.bones)} bones, "
        f"{len(target.data.vertices):,} vertices, height target {HUMAN_HEIGHT:.2f}m"
    )

    actions = build_actions(armature)
    export_fbx(worker_root, armature, target)

    log("Importing the actual USS Enterprise GLB for the carrier-deck test scene")
    enterprise_objects = import_enterprise(ENTERPRISE_GLB)
    ship_root = create_ship_root(enterprise_objects)
    bpy.context.view_layer.update()
    crew_x, crew_y = 54.0, -6.0
    deck_z = cast_to_enterprise_deck(enterprise_objects, crew_x, crew_y)
    worker_root.location = (crew_x, crew_y, deck_z)
    worker_root.rotation_euler.z = math.radians(-90.0)
    bpy.context.view_layer.update()
    log(f"Enterprise deck raycast passed: worker origin placed at z={deck_z:.3f}m")

    focus = (crew_x, crew_y, deck_z + 1.0)
    deck_camera = add_camera(
        "Camera | trabalhador no convés", (crew_x - 3.0, crew_y - 4.7, deck_z + 2.5), focus, 48.0
    )
    overview_camera = add_camera(
        "Camera | USS Enterprise por inteiro", (225.0, -390.0, 360.0), (0.0, 0.0, 25.0), 45.0
    )
    overview_camera.data.type = "ORTHO"
    overview_camera.data.ortho_scale = 590.0
    add_area_light("Sun | luz de convés", (crew_x - 12, crew_y - 18, deck_z + 24), focus, 6500, (1.0, 0.86, 0.68), 14)
    add_area_light("Fill | céu", (crew_x + 12, crew_y + 7, deck_z + 11), focus, 2800, (0.60, 0.76, 1.0), 10)
    add_area_light("Rim | aço", (crew_x + 2, crew_y + 13, deck_z + 14), focus, 4000, (1.0, 0.96, 0.84), 8)
    configure_render(scene, deck_camera)

    # Still renders are pinned to the actual carrier deck and use the same target mesh.
    armature.animation_data.use_nla = False
    armature.animation_data.action = actions["Walk_Deck"]
    render_still(scene, os.path.join(STILLS_DIR, "01_Trabalhador_Em_Pe_Enterprise.png"), 1)
    armature.animation_data.action = actions["Kneel_Ready"]
    render_still(scene, os.path.join(STILLS_DIR, "02_Ajoelhado_Preparacao.png"), 30)
    armature.animation_data.action = actions["Kneel_LaunchSignal"]
    render_still(scene, os.path.join(STILLS_DIR, "02_Ajoelhado_Liberando_Voo.png"), 29)
    armature.animation_data.action = actions["Check_Catapult"]
    render_still(scene, os.path.join(STILLS_DIR, "03_Inspecao_Catapulta.png"), 17)
    for action_name, frame, filename in (
        ("Marshal_Stop", 16, "05_Sinal_Parar.png"),
        ("Marshal_TurnLeft", 15, "06_Sinal_Virar_Esquerda.png"),
        ("Marshal_TurnRight", 15, "07_Sinal_Virar_Direita.png"),
        ("Clear_Deck_Wave", 15, "08_Liberar_Conves.png"),
    ):
        armature.animation_data.action = actions[action_name]
        render_still(scene, os.path.join(STILLS_DIR, filename), frame)

    scene.camera = overview_camera
    armature.animation_data.action = actions["Walk_Deck"]
    render_still(scene, os.path.join(STILLS_DIR, "04_Uss_Enterprise_Visao_Geral.png"), 1)

    # The export stays at identity transforms; the .blend opens with the worker on deck.
    launch_frame = build_nla_sequence(armature, actions, scene)
    scene.camera = deck_camera
    scene.frame_set(launch_frame)
    if "--stills-only" in sys.argv:
        log("Stills-only validation requested; skipping the full animation render")
    else:
        make_animation_video(scene)

    # Return to a useful, inspectable Blender state and save the complete scene.
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(STILLS_DIR, "render.png")
    scene.camera = deck_camera
    scene.frame_set(launch_frame)
    bpy.ops.object.select_all(action="DESELECT")
    worker_root.select_set(True)
    bpy.context.view_layer.objects.active = worker_root
    for workspace in bpy.data.workspaces:
        for screen_area in workspace.screens[0].areas if workspace.screens else []:
            if screen_area.type == "VIEW_3D":
                screen_area.spaces.active.region_3d.view_perspective = "CAMERA"
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUTPUT)
    log(f"Saved Blender project: {BLEND_OUTPUT}")
    log(f"Total elapsed: {time.time() - started:.1f}s")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)
