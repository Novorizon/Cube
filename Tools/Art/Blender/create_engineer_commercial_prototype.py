import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[3]
OUT_DIR = ROOT / "Assets" / "Arts" / "Character" / "EngineerCommercial"
FBX_PATH = OUT_DIR / "EngineerCommercialPrototype.fbx"
BLEND_PATH = OUT_DIR / "EngineerCommercialPrototype.blend"
PREVIEW_PATH = OUT_DIR / "EngineerCommercialPrototype_preview.png"
README_PATH = OUT_DIR / "README.md"


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_mat(name, color, roughness=0.86, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next((node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = mat.node_tree.nodes.new(type="ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def shade_faceted(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = False


def assign_to_bone(obj, armature, bone_name):
    group = obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "ADD")
    mod = obj.modifiers.new(name="EngineerArmature", type="ARMATURE")
    mod.object = armature
    obj.parent = armature


def apply_bevel(obj, width, segments=1):
    if width <= 0:
        return
    bevel = obj.modifiers.new(name="ControlledFacetBevel", type="BEVEL")
    bevel.width = width
    bevel.segments = segments
    bevel.affect = "EDGES"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)


def cube_part(name, loc, scale, mat, armature, bone_name, rot=(0, 0, 0), bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    apply_bevel(obj, bevel)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def sphere_part(name, loc, scale, mat, armature, bone_name, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def ico_part(name, loc, scale, mat, armature, bone_name, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def cylinder_part(name, loc, radius, depth, mat, armature, bone_name, vertices=12, rot=(0, 0, 0), scale=(1, 1, 1), bevel=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    apply_bevel(obj, bevel)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def cone_part(name, loc, radius1, radius2, depth, mat, armature, bone_name, vertices=7, rot=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def torus_part(name, loc, major, minor, mat, armature, bone_name, rot=(0, 0, 0), major_segments=18, minor_segments=6):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_segments,
        minor_segments=minor_segments,
        major_radius=major,
        minor_radius=minor,
        location=loc,
        rotation=rot,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def cylinder_between(name, start, end, radius, mat, armature, bone_name, vertices=12):
    start = Vector(start)
    end = Vector(end)
    mid = (start + end) * 0.5
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=direction.length, location=mid)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    shade_faceted(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def create_armature():
    arm_data = bpy.data.armatures.new("EngineerCommercialArmatureData")
    arm_obj = bpy.data.objects.new("EngineerCommercialArmature", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bones = {
        "Hips": ((0, 0, 0.82), (0, 0, 1.03), None),
        "Spine": ((0, 0, 1.03), (0, 0, 1.32), "Hips"),
        "Chest": ((0, 0, 1.32), (0, 0, 1.62), "Spine"),
        "Neck": ((0, 0, 1.62), (0, 0, 1.78), "Chest"),
        "Head": ((0, 0, 1.78), (0, 0, 2.32), "Neck"),
        "LeftUpperArm": ((-0.38, -0.02, 1.50), (-0.72, -0.07, 1.28), "Chest"),
        "LeftLowerArm": ((-0.72, -0.07, 1.28), (-0.91, -0.12, 0.98), "LeftUpperArm"),
        "LeftHand": ((-0.91, -0.12, 0.98), (-1.02, -0.12, 0.84), "LeftLowerArm"),
        "RightUpperArm": ((0.38, -0.02, 1.50), (0.70, -0.10, 1.40), "Chest"),
        "RightLowerArm": ((0.70, -0.10, 1.40), (0.96, -0.17, 1.53), "RightUpperArm"),
        "RightHand": ((0.96, -0.17, 1.53), (1.10, -0.18, 1.58), "RightLowerArm"),
        "LeftUpperLeg": ((-0.24, 0, 0.83), (-0.30, -0.01, 0.47), "Hips"),
        "LeftLowerLeg": ((-0.30, -0.01, 0.47), (-0.31, -0.02, 0.15), "LeftUpperLeg"),
        "LeftFoot": ((-0.31, -0.02, 0.15), (-0.31, -0.28, 0.08), "LeftLowerLeg"),
        "RightUpperLeg": ((0.24, 0, 0.83), (0.30, -0.01, 0.47), "Hips"),
        "RightLowerLeg": ((0.30, -0.01, 0.47), (0.31, -0.02, 0.15), "RightUpperLeg"),
        "RightFoot": ((0.31, -0.02, 0.15), (0.31, -0.28, 0.08), "RightLowerLeg"),
    }

    created = {}
    for name, (head, tail, parent) in bones.items():
        bone = arm_data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent:
            bone.parent = created[parent]
            bone.use_connect = False
        created[name] = bone

    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm_obj.pose.bones:
        bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def add_gear(name, center, radius, mat, armature, bone_name, teeth=12):
    torus_part(name + "_Ring", center, radius, 0.025, mat, armature, bone_name, rot=(math.radians(90), 0, 0), major_segments=24, minor_segments=6)
    for i in range(teeth):
        angle = math.tau * i / teeth
        x = center[0] + math.cos(angle) * (radius + 0.045)
        z = center[2] + math.sin(angle) * (radius + 0.045)
        cube_part(
            f"{name}_Tooth_{i:02d}",
            (x, center[1], z),
            (0.040, 0.035, 0.075),
            mat,
            armature,
            bone_name,
            rot=(0, -angle, 0),
            bevel=0.006,
        )
    cylinder_part(name + "_Hub", center, radius * 0.38, 0.035, mat, armature, bone_name, vertices=14, rot=(math.radians(90), 0, 0))


def add_beard(armature, mats):
    beard = mats["beard"]
    dark = mats["beard_dark"]
    positions = [
        (0.00, -0.315, 1.62, 0.25, 0.10, 0.36),
        (-0.16, -0.30, 1.68, 0.16, 0.07, 0.30),
        (0.16, -0.30, 1.68, 0.16, 0.07, 0.30),
        (-0.25, -0.24, 1.77, 0.13, 0.06, 0.26),
        (0.25, -0.24, 1.77, 0.13, 0.06, 0.26),
        (-0.08, -0.34, 1.49, 0.13, 0.03, 0.28),
        (0.08, -0.34, 1.49, 0.13, 0.03, 0.28),
    ]
    for i, (x, y, z, r1, r2, depth) in enumerate(positions):
        mat = beard if i % 2 == 0 else dark
        cone_part(f"Beard_Clump_{i}", (x, y, z), r1, r2, depth, mat, armature, "Head", vertices=8, rot=(0, 0, 0))
    cube_part("Mustache_Left", (-0.13, -0.39, 1.90), (0.19, 0.045, 0.060), beard, armature, "Head", rot=(0, 0, math.radians(-12)), bevel=0.015)
    cube_part("Mustache_Right", (0.13, -0.39, 1.90), (0.19, 0.045, 0.060), beard, armature, "Head", rot=(0, 0, math.radians(12)), bevel=0.015)
    cube_part("Beard_Chin_Ridge", (0, -0.41, 1.70), (0.20, 0.035, 0.055), dark, armature, "Head", bevel=0.01)


def create_character(armature):
    mats = {
        "skin": make_mat("M_Skin_Warm_Matte", (0.96, 0.63, 0.39, 1), 0.86),
        "skin_shadow": make_mat("M_Skin_Shadow_Matte", (0.76, 0.42, 0.24, 1), 0.88),
        "beard": make_mat("M_Beard_Orange_Matte", (0.96, 0.37, 0.05, 1), 0.9),
        "beard_dark": make_mat("M_Beard_DarkOrange_Matte", (0.70, 0.22, 0.04, 1), 0.92),
        "blue": make_mat("M_Cloth_Blue_Matte", (0.05, 0.27, 0.47, 1), 0.88),
        "blue_dark": make_mat("M_Cloth_DarkBlue_Matte", (0.03, 0.15, 0.27, 1), 0.9),
        "shirt": make_mat("M_Shirt_Cream_Matte", (0.92, 0.78, 0.55, 1), 0.88),
        "leather": make_mat("M_Leather_Brown_Matte", (0.32, 0.19, 0.10, 1), 0.82),
        "leather_dark": make_mat("M_Leather_Dark_Matte", (0.18, 0.11, 0.07, 1), 0.86),
        "brass": make_mat("M_Brass_Matte", (0.96, 0.66, 0.16, 1), 0.70),
        "steel": make_mat("M_Steel_Matte", (0.55, 0.55, 0.52, 1), 0.78),
        "steel_dark": make_mat("M_DarkSteel_Matte", (0.30, 0.31, 0.30, 1), 0.82),
        "glass": make_mat("M_Glass_Blue_Matte", (0.23, 0.55, 0.75, 1), 0.92),
        "eye": make_mat("M_Eyes_Matte", (0.035, 0.030, 0.025, 1), 0.96),
        "white": make_mat("M_EyeHighlight_Matte", (1.0, 0.95, 0.85, 1), 0.90),
    }

    # Body silhouette.
    sphere_part("Torso_Barrel_Blue", (0, -0.02, 1.17), (0.46, 0.34, 0.48), mats["blue"], armature, "Spine", 18, 10)
    sphere_part("Chest_Rounded_Blue", (0, -0.03, 1.42), (0.50, 0.36, 0.36), mats["blue"], armature, "Chest", 18, 9)
    cylinder_part("Waist_Belt", (0, -0.05, 0.92), 0.49, 0.13, mats["leather"], armature, "Hips", vertices=16, scale=(1, 0.72, 1), bevel=0.018)
    cube_part("Belt_Buckle", (0, -0.39, 0.94), (0.15, 0.055, 0.105), mats["brass"], armature, "Hips", bevel=0.018)
    torus_part("Belt_Buckle_Hole", (0, -0.425, 0.94), 0.075, 0.013, mats["leather_dark"], armature, "Hips", rot=(math.radians(90), 0, 0), major_segments=12, minor_segments=4)

    for sx, label in [(-1, "Left"), (1, "Right")]:
        cube_part(f"{label}_Overall_Strap", (sx * 0.19, -0.37, 1.34), (0.055, 0.040, 0.35), mats["leather"], armature, "Chest", rot=(0, 0, math.radians(sx * 10)), bevel=0.012)
        torus_part(f"{label}_Strap_Ring", (sx * 0.28, -0.39, 1.58), 0.055, 0.010, mats["brass"], armature, "Chest", rot=(math.radians(90), 0, 0), major_segments=12, minor_segments=4)
        sphere_part(f"{label}_Shoulder_Sleeve", (sx * 0.47, -0.02, 1.46), (0.19, 0.16, 0.20), mats["shirt"], armature, f"{label}UpperArm", 14, 7)
        sphere_part(f"{label}_Shoulder_Pad", (sx * 0.42, -0.06, 1.58), (0.18, 0.15, 0.10), mats["steel"], armature, f"{label}UpperArm", 12, 6)
        cube_part(f"{label}_UpperArm_Sleeve", (sx * 0.66, -0.07, 1.28), (0.15, 0.14, 0.26), mats["shirt"], armature, f"{label}UpperArm", rot=(0, 0, math.radians(sx * 20)), bevel=0.035)
        cube_part(f"{label}_Forearm_Skin", (sx * 0.83, -0.12, 1.05), (0.15, 0.13, 0.22), mats["skin"], armature, f"{label}LowerArm", rot=(0, 0, math.radians(sx * 15)), bevel=0.030)
        sphere_part(f"{label}_Glove", (sx * 0.94, -0.16, 0.86 if sx < 0 else 1.55), (0.18, 0.16, 0.16), mats["leather_dark"], armature, f"{label}Hand", 12, 6)
        for finger in range(4):
            offset = (finger - 1.5) * 0.045
            sphere_part(f"{label}_Glove_Finger_{finger}", (sx * (1.01 + abs(offset) * 0.25), -0.26 + offset, 0.83 if sx < 0 else 1.52), (0.045, 0.040, 0.075), mats["leather_dark"], armature, f"{label}Hand", 8, 4)

    # Head and face.
    sphere_part("Head_Rounded", (0, -0.02, 1.98), (0.39, 0.34, 0.38), mats["skin"], armature, "Head", 18, 10)
    sphere_part("Nose_Round", (0, -0.365, 1.95), (0.115, 0.095, 0.095), mats["skin"], armature, "Head", 12, 6)
    for sx, label in [(-1, "Left"), (1, "Right")]:
        sphere_part(f"{label}_Ear", (sx * 0.36, -0.04, 1.99), (0.090, 0.055, 0.145), mats["skin"], armature, "Head", 10, 5)
        sphere_part(f"{label}_InnerEar", (sx * 0.375, -0.075, 1.99), (0.045, 0.025, 0.075), mats["skin_shadow"], armature, "Head", 8, 4)
        sphere_part(f"{label}_Eye", (sx * 0.135, -0.335, 2.05), (0.060, 0.028, 0.070), mats["eye"], armature, "Head", 10, 5)
        sphere_part(f"{label}_Eye_Highlight", (sx * 0.120, -0.360, 2.075), (0.018, 0.010, 0.020), mats["white"], armature, "Head", 6, 4)
        cube_part(f"{label}_Eyebrow", (sx * 0.145, -0.365, 2.17), (0.130, 0.035, 0.045), mats["beard"], armature, "Head", rot=(0, 0, math.radians(-sx * 10)), bevel=0.012)

    add_beard(armature, mats)

    # Hat and goggles.
    sphere_part("Cap_Crown", (0, -0.02, 2.27), (0.42, 0.36, 0.19), mats["blue"], armature, "Head", 18, 8)
    cube_part("Cap_Brim", (0, -0.36, 2.19), (0.30, 0.095, 0.050), mats["blue_dark"], armature, "Head", bevel=0.018)
    torus_part("Goggle_Left_Ring", (-0.125, -0.38, 2.30), 0.080, 0.023, mats["brass"], armature, "Head", rot=(math.radians(90), 0, 0), major_segments=18, minor_segments=6)
    torus_part("Goggle_Right_Ring", (0.125, -0.38, 2.30), 0.080, 0.023, mats["brass"], armature, "Head", rot=(math.radians(90), 0, 0), major_segments=18, minor_segments=6)
    cylinder_part("Goggle_Left_Lens", (-0.125, -0.405, 2.30), 0.064, 0.020, mats["glass"], armature, "Head", vertices=16, rot=(math.radians(90), 0, 0))
    cylinder_part("Goggle_Right_Lens", (0.125, -0.405, 2.30), 0.064, 0.020, mats["glass"], armature, "Head", vertices=16, rot=(math.radians(90), 0, 0))
    cube_part("Goggle_Bridge", (0, -0.405, 2.30), (0.055, 0.020, 0.025), mats["brass"], armature, "Head", bevel=0.006)
    cylinder_part("Goggle_Strap", (0, -0.02, 2.25), 0.405, 0.035, mats["leather"], armature, "Head", vertices=18, scale=(1, 0.82, 0.22))

    # Legs, boots, and armor.
    for sx, label in [(-1, "Left"), (1, "Right")]:
        cube_part(f"{label}_Pants_Upper", (sx * 0.21, -0.02, 0.61), (0.18, 0.18, 0.30), mats["blue"], armature, f"{label}UpperLeg", bevel=0.032)
        cube_part(f"{label}_Boot_Leg", (sx * 0.25, -0.03, 0.29), (0.17, 0.17, 0.23), mats["leather"], armature, f"{label}LowerLeg", bevel=0.030)
        cube_part(f"{label}_Boot_Foot", (sx * 0.25, -0.20, 0.08), (0.22, 0.34, 0.105), mats["leather"], armature, f"{label}Foot", bevel=0.035)
        cube_part(f"{label}_Boot_ToePlate", (sx * 0.25, -0.38, 0.125), (0.17, 0.10, 0.065), mats["brass"], armature, f"{label}Foot", bevel=0.020)
        cylinder_part(f"{label}_Boot_Cuff", (sx * 0.25, -0.04, 0.43), 0.20, 0.065, mats["leather_dark"], armature, f"{label}LowerLeg", vertices=14, scale=(1, 0.72, 1), bevel=0.010)

    # Backpack, tools, and props.
    cube_part("Backpack_Box", (0, 0.31, 1.32), (0.42, 0.16, 0.44), mats["leather"], armature, "Chest", bevel=0.030)
    cube_part("Backpack_TopFrame", (0, 0.42, 1.68), (0.45, 0.055, 0.065), mats["brass"], armature, "Chest", bevel=0.012)
    cube_part("Backpack_BottomFrame", (0, 0.42, 0.98), (0.45, 0.055, 0.065), mats["brass"], armature, "Chest", bevel=0.012)
    for sx in [-1, 1]:
        cube_part(f"Backpack_SideRail_{sx}", (sx * 0.31, 0.42, 1.32), (0.055, 0.055, 0.40), mats["brass"], armature, "Chest", bevel=0.012)
    cylinder_part("Backpack_BlueTank", (0.14, 0.49, 1.43), 0.13, 0.37, mats["blue"], armature, "Chest", vertices=14, rot=(math.radians(90), 0, 0), bevel=0.010)
    add_gear("Backpack_Gear", (-0.18, 0.51, 1.58), 0.125, mats["steel"], armature, "Chest", teeth=12)
    cylinder_part("Backpack_Valve", (0.10, 0.52, 1.73), 0.050, 0.080, mats["steel_dark"], armature, "Chest", vertices=10, bevel=0.006)

    cube_part("Left_ToolPouch", (-0.45, -0.24, 0.86), (0.12, 0.085, 0.16), mats["leather"], armature, "Hips", bevel=0.018)
    cube_part("Right_ToolPouch", (0.45, -0.24, 0.84), (0.12, 0.085, 0.16), mats["leather"], armature, "Hips", bevel=0.018)
    cylinder_part("Potion_Bottle", (-0.58, -0.28, 0.74), 0.060, 0.22, mats["brass"], armature, "Hips", vertices=10, bevel=0.008)
    cylinder_part("Potion_Cap", (-0.58, -0.28, 0.88), 0.040, 0.050, mats["steel"], armature, "Hips", vertices=8)
    cube_part("Wrench_Handle", (0.05, -0.42, 0.86), (0.035, 0.030, 0.27), mats["steel"], armature, "Hips", rot=(0, 0, math.radians(-20)), bevel=0.008)
    torus_part("Wrench_OpenHead", (0.13, -0.44, 1.02), 0.060, 0.012, mats["steel"], armature, "Hips", rot=(math.radians(90), 0, math.radians(12)), major_segments=12, minor_segments=4)

    cylinder_between("Hammer_Handle", (1.00, -0.20, 1.26), (1.26, -0.24, 2.05), 0.040, mats["leather"], armature, "RightHand", vertices=10)
    cube_part("Hammer_Head_Main", (1.32, -0.25, 2.10), (0.23, 0.16, 0.13), mats["steel"], armature, "RightHand", rot=(0, 0, math.radians(8)), bevel=0.026)
    cylinder_part("Hammer_LeftCap", (1.10, -0.25, 2.10), 0.105, 0.065, mats["brass"], armature, "RightHand", vertices=12, rot=(0, math.radians(90), 0), bevel=0.010)
    cylinder_part("Hammer_RightCap", (1.53, -0.25, 2.10), 0.105, 0.065, mats["steel"], armature, "RightHand", vertices=12, rot=(0, math.radians(90), 0), bevel=0.010)


def clear_pose(armature):
    for pb in armature.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)


def key_pose(armature, frame, rotations=None, locations=None):
    bpy.context.scene.frame_set(frame)
    clear_pose(armature)
    rotations = rotations or {}
    locations = locations or {}
    for name, rot in rotations.items():
        if name in armature.pose.bones:
            armature.pose.bones[name].rotation_euler = tuple(math.radians(v) for v in rot)
    for name, loc in locations.items():
        if name in armature.pose.bones:
            armature.pose.bones[name].location = loc
    for pb in armature.pose.bones:
        pb.keyframe_insert(data_path="rotation_euler", frame=frame)
        pb.keyframe_insert(data_path="location", frame=frame)


def make_action(armature, name, poses):
    action = bpy.data.actions.new(name)
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, rotations, locations in poses:
        key_pose(armature, frame, rotations, locations)
    action.use_fake_user = True
    return action


def create_animations(armature):
    actions = [
        make_action(
            armature,
            "Idle",
            [
                (1, {"Chest": (2, 0, 0), "Head": (-2, 0, 0), "RightUpperArm": (-5, 0, -8)}, {}),
                (20, {"Chest": (-2, 0, 0), "Head": (1, 0, 0), "RightUpperArm": (-2, 0, -10)}, {"Hips": (0, 0, 0.025)}),
                (40, {"Chest": (2, 0, 0), "Head": (-2, 0, 0), "RightUpperArm": (-5, 0, -8)}, {}),
            ],
        ),
        make_action(
            armature,
            "Walk",
            [
                (1, {"LeftUpperLeg": (20, 0, 0), "RightUpperLeg": (-20, 0, 0), "LeftUpperArm": (-18, 0, 5), "RightUpperArm": (10, 0, -5)}, {}),
                (15, {"LeftUpperLeg": (-20, 0, 0), "RightUpperLeg": (20, 0, 0), "LeftUpperArm": (18, 0, 5), "RightUpperArm": (-12, 0, -5)}, {"Hips": (0, 0, 0.030)}),
                (30, {"LeftUpperLeg": (20, 0, 0), "RightUpperLeg": (-20, 0, 0), "LeftUpperArm": (-18, 0, 5), "RightUpperArm": (10, 0, -5)}, {}),
            ],
        ),
        make_action(
            armature,
            "HammerAttack",
            [
                (1, {"RightUpperArm": (-18, 0, -10), "RightLowerArm": (8, 0, 0), "Chest": (0, 0, -4)}, {}),
                (10, {"RightUpperArm": (-72, 0, -20), "RightLowerArm": (-25, 0, 0), "Chest": (8, 0, -12), "Head": (4, 0, -4)}, {"Hips": (0, 0, 0.025)}),
                (18, {"RightUpperArm": (42, 0, -35), "RightLowerArm": (25, 0, 0), "Chest": (-8, 0, 10), "Head": (-3, 0, 5)}, {"Hips": (0, -0.03, -0.01)}),
                (34, {"RightUpperArm": (-18, 0, -10), "RightLowerArm": (8, 0, 0)}, {}),
            ],
        ),
        make_action(
            armature,
            "Die",
            [
                (1, {}, {}),
                (18, {"Hips": (0, 0, 55), "Chest": (-20, 0, 0), "Head": (-18, 0, 0), "LeftUpperArm": (12, 0, 60), "RightUpperArm": (12, 0, -60)}, {"Hips": (0, -0.18, -0.30)}),
                (40, {"Hips": (0, 0, 82), "Chest": (-35, 0, 0), "Head": (-22, 0, 0), "LeftUpperArm": (10, 0, 78), "RightUpperArm": (10, 0, -78)}, {"Hips": (0, -0.26, -0.58)}),
            ],
        ),
    ]
    armature.animation_data.action = None
    for action in actions:
        track = armature.animation_data.nla_tracks.new()
        track.name = action.name
        strip = track.strips.new(action.name, 1, action)
        strip.name = action.name
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 40
    bpy.context.scene.frame_set(1)
    clear_pose(armature)


def setup_scene():
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.world.color = (0.90, 0.87, 0.82)
    bpy.ops.object.light_add(type="AREA", location=(-3, -4, 5))
    key = bpy.context.object
    key.name = "Preview_KeyLight"
    key.data.energy = 550
    key.data.size = 5.5
    bpy.ops.object.light_add(type="POINT", location=(2.4, -2.8, 2.2))
    rim = bpy.context.object
    rim.name = "Preview_FillLight"
    rim.data.energy = 80
    camera_loc = Vector((2.5, -5.0, 1.65))
    target = Vector((0.15, -0.10, 1.22))
    direction = target - camera_loc
    bpy.ops.object.camera_add(location=camera_loc)
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.75
    bpy.context.scene.camera = camera
    bpy.context.scene.render.resolution_x = 1400
    bpy.context.scene.render.resolution_y = 1400


def write_readme(stats):
    README_PATH.write_text(
        "# Engineer Commercial Prototype\n\n"
        "Generated by `Tools/Art/Blender/create_engineer_commercial_prototype.py`.\n\n"
        "Assets:\n"
        "- `EngineerCommercialPrototype.fbx`: higher-density stylized engineer trial model.\n"
        "- `EngineerCommercialPrototype.blend`: editable source scene.\n"
        "- `EngineerCommercialPrototype_preview.png`: quick visual preview.\n\n"
        "Embedded prototype actions:\n"
        "- `Idle`\n"
        "- `Walk`\n"
        "- `HammerAttack`\n"
        "- `Die`\n\n"
        f"Stats from Blender source: `{stats['objects']}` mesh objects, `{stats['verts']}` vertices, about `{stats['tris']}` triangles.\n\n"
        "Import notes:\n"
        "- Materials are matte by default: metallic 0 and high roughness.\n"
        "- This is a higher-spec visual prototype, not a final hand-retopologized production mesh.\n"
        "- Try Humanoid first only after silhouette approval; segmented prop-heavy characters may need Generic for reliable custom actions.\n",
        encoding="utf-8",
    )


def mesh_stats():
    mesh_objs = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    verts = sum(len(obj.data.vertices) for obj in mesh_objs)
    tris = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in mesh_objs)
    return {"objects": len(mesh_objs), "verts": verts, "tris": tris}


def export_assets():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        apply_scale_options="FBX_SCALE_UNITS",
        object_types={"ARMATURE", "MESH"},
        mesh_smooth_type="FACE",
    )
    bpy.context.scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)
    write_readme(mesh_stats())


def main():
    clean_scene()
    armature = create_armature()
    create_character(armature)
    create_animations(armature)
    setup_scene()
    export_assets()
    stats = mesh_stats()
    print(
        "ENGINEER_COMMERCIAL_STATS "
        f"objects={stats['objects']} verts={stats['verts']} tris={stats['tris']} "
        f"actions={','.join(action.name for action in bpy.data.actions)}"
    )


if __name__ == "__main__":
    main()
