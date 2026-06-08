import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[3]
OUT_DIR = ROOT / "Assets" / "Arts" / "Character" / "Engineer"
FBX_PATH = OUT_DIR / "EngineerPrototype.fbx"
BLEND_PATH = OUT_DIR / "EngineerPrototype.blend"
PREVIEW_PATH = OUT_DIR / "EngineerPrototype_preview.png"
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
    if "Alpha" in bsdf.inputs:
        bsdf.inputs["Alpha"].default_value = color[3]
    return mat


def assign_to_bone(obj, armature, bone_name):
    group = obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "ADD")
    mod = obj.modifiers.new(name="EngineerArmature", type="ARMATURE")
    mod.object = armature
    obj.parent = armature


def shade_flat(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = False


def cube_part(name, loc, scale, mat, armature, bone_name, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        bevel_mod = obj.modifiers.new(name="SoftBlockEdges", type="BEVEL")
        bevel_mod.width = bevel
        bevel_mod.segments = 1
        bevel_mod.affect = "EDGES"
        bpy.ops.object.modifier_apply(modifier=bevel_mod.name)
    shade_flat(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def sphere_part(name, loc, scale, mat, armature, bone_name, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    shade_flat(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def cylinder_part(name, loc, radius, depth, mat, armature, bone_name, vertices=8, rot=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    shade_flat(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def torus_part(name, loc, major_radius, minor_radius, mat, armature, bone_name, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=12,
        minor_segments=4,
        major_radius=major_radius,
        minor_radius=minor_radius,
        location=loc,
        rotation=rot,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    shade_flat(obj)
    assign_to_bone(obj, armature, bone_name)
    return obj


def create_armature():
    arm_data = bpy.data.armatures.new("EngineerArmatureData")
    arm_obj = bpy.data.objects.new("EngineerArmature", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bones = {
        "Hips": ((0, 0, 1.02), (0, 0, 1.22), None),
        "Spine": ((0, 0, 1.22), (0, 0, 1.55), "Hips"),
        "Chest": ((0, 0, 1.55), (0, 0, 1.86), "Spine"),
        "Neck": ((0, 0, 1.86), (0, 0, 2.02), "Chest"),
        "Head": ((0, 0, 2.02), (0, 0, 2.48), "Neck"),
        "LeftUpperArm": ((-0.27, 0, 1.78), (-0.64, 0, 1.57), "Chest"),
        "LeftLowerArm": ((-0.64, 0, 1.57), (-0.83, 0, 1.23), "LeftUpperArm"),
        "LeftHand": ((-0.83, 0, 1.23), (-0.91, 0, 1.06), "LeftLowerArm"),
        "RightUpperArm": ((0.27, 0, 1.78), (0.64, 0, 1.57), "Chest"),
        "RightLowerArm": ((0.64, 0, 1.57), (0.83, 0, 1.23), "RightUpperArm"),
        "RightHand": ((0.83, 0, 1.23), (0.91, 0, 1.06), "RightLowerArm"),
        "LeftUpperLeg": ((-0.20, 0, 1.02), (-0.27, 0, 0.62), "Hips"),
        "LeftLowerLeg": ((-0.27, 0, 0.62), (-0.28, 0, 0.22), "LeftUpperLeg"),
        "LeftFoot": ((-0.28, 0, 0.22), (-0.28, -0.22, 0.10), "LeftLowerLeg"),
        "RightUpperLeg": ((0.20, 0, 1.02), (0.27, 0, 0.62), "Hips"),
        "RightLowerLeg": ((0.27, 0, 0.62), (0.28, 0, 0.22), "RightUpperLeg"),
        "RightFoot": ((0.28, 0, 0.22), (0.28, -0.22, 0.10), "RightLowerLeg"),
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


def create_parts(armature):
    skin = make_mat("M_Engineer_Skin_Matte", (0.93, 0.62, 0.39, 1))
    beard = make_mat("M_Engineer_Beard_Orange_Matte", (0.92, 0.34, 0.06, 1))
    blue = make_mat("M_Engineer_Workwear_Blue_Matte", (0.06, 0.26, 0.42, 1))
    shirt = make_mat("M_Engineer_Shirt_Cream_Matte", (0.90, 0.78, 0.55, 1))
    leather = make_mat("M_Engineer_Leather_Matte", (0.30, 0.18, 0.10, 1))
    glove = make_mat("M_Engineer_Gloves_Dark_Matte", (0.18, 0.14, 0.11, 1))
    black = make_mat("M_Engineer_Eyes_Matte", (0.03, 0.025, 0.02, 1), roughness=0.95)
    gold = make_mat("M_Engineer_Brass_Matte", (0.95, 0.68, 0.18, 1), roughness=0.72)
    metal = make_mat("M_Engineer_Steel_Matte", (0.53, 0.53, 0.50, 1), roughness=0.78)
    glass = make_mat("M_Engineer_Glass_Blue_Matte", (0.20, 0.55, 0.78, 1), roughness=0.90)

    cube_part("Torso_Overalls", (0, -0.01, 1.45), (0.42, 0.28, 0.48), blue, armature, "Chest", bevel=0.04)
    cube_part("Belly_Overalls", (0, -0.01, 1.12), (0.46, 0.30, 0.28), blue, armature, "Hips", bevel=0.04)
    cube_part("Overall_Strap_L", (-0.18, -0.30, 1.43), (0.045, 0.035, 0.42), leather, armature, "Chest", rot=(0, 0, math.radians(-9)), bevel=0.008)
    cube_part("Overall_Strap_R", (0.18, -0.30, 1.43), (0.045, 0.035, 0.42), leather, armature, "Chest", rot=(0, 0, math.radians(9)), bevel=0.008)
    cylinder_part("Neck_Collar", (0, -0.01, 1.88), 0.18, 0.075, shirt, armature, "Neck", vertices=10)
    cube_part("Shirt_LeftSleeve", (-0.43, -0.01, 1.54), (0.20, 0.18, 0.36), shirt, armature, "LeftUpperArm", rot=(0, 0.15, 0.22), bevel=0.035)
    cube_part("Shirt_RightSleeve", (0.43, -0.01, 1.54), (0.20, 0.18, 0.36), shirt, armature, "RightUpperArm", rot=(0, -0.15, -0.22), bevel=0.035)
    sphere_part("ShoulderPad_L", (-0.36, -0.02, 1.73), (0.13, 0.12, 0.10), metal, armature, "LeftUpperArm", subdivisions=1)
    sphere_part("ShoulderPad_R", (0.36, -0.02, 1.73), (0.13, 0.12, 0.10), metal, armature, "RightUpperArm", subdivisions=1)

    sphere_part("Head", (0, -0.01, 2.16), (0.35, 0.31, 0.35), skin, armature, "Head", subdivisions=2)
    sphere_part("Nose", (0, -0.30, 2.14), (0.11, 0.09, 0.09), skin, armature, "Head", subdivisions=1)
    sphere_part("Ear_L", (-0.32, -0.01, 2.16), (0.08, 0.04, 0.12), skin, armature, "Head", subdivisions=1)
    sphere_part("Ear_R", (0.32, -0.01, 2.16), (0.08, 0.04, 0.12), skin, armature, "Head", subdivisions=1)
    sphere_part("Eye_L", (-0.105, -0.305, 2.18), (0.035, 0.018, 0.045), black, armature, "Head", subdivisions=1)
    sphere_part("Eye_R", (0.105, -0.305, 2.18), (0.035, 0.018, 0.045), black, armature, "Head", subdivisions=1)
    cube_part("Brow_L", (-0.13, -0.30, 2.26), (0.11, 0.035, 0.035), beard, armature, "Head", rot=(0, 0, 0.16), bevel=0.01)
    cube_part("Brow_R", (0.13, -0.30, 2.26), (0.11, 0.035, 0.035), beard, armature, "Head", rot=(0, 0, -0.16), bevel=0.01)
    sphere_part("Beard_Main", (0, -0.18, 1.92), (0.27, 0.17, 0.22), beard, armature, "Head", subdivisions=2)
    cube_part("Mustache_L", (-0.12, -0.32, 2.03), (0.16, 0.04, 0.055), beard, armature, "Head", rot=(0, 0, -0.20), bevel=0.015)
    cube_part("Mustache_R", (0.12, -0.32, 2.03), (0.16, 0.04, 0.055), beard, armature, "Head", rot=(0, 0, 0.20), bevel=0.015)

    sphere_part("Cap_Crown", (0, 0.00, 2.43), (0.34, 0.30, 0.16), blue, armature, "Head", subdivisions=2)
    cube_part("Cap_Brim", (0, -0.29, 2.36), (0.25, 0.09, 0.035), blue, armature, "Head", bevel=0.02)
    torus_part("Goggle_L_Ring", (-0.12, -0.30, 2.43), 0.075, 0.018, gold, armature, "Head", rot=(math.radians(90), 0, 0))
    torus_part("Goggle_R_Ring", (0.12, -0.30, 2.43), 0.075, 0.018, gold, armature, "Head", rot=(math.radians(90), 0, 0))
    cylinder_part("Goggle_L_Lens", (-0.12, -0.305, 2.43), 0.055, 0.018, glass, armature, "Head", vertices=10, rot=(math.radians(90), 0, 0))
    cylinder_part("Goggle_R_Lens", (0.12, -0.305, 2.43), 0.055, 0.018, glass, armature, "Head", vertices=10, rot=(math.radians(90), 0, 0))
    cube_part("Goggle_Bridge", (0, -0.31, 2.43), (0.065, 0.018, 0.025), gold, armature, "Head", bevel=0.005)

    for side, sx, upper, lower, hand in [
        ("L", -1, "LeftUpperArm", "LeftLowerArm", "LeftHand"),
        ("R", 1, "RightUpperArm", "RightLowerArm", "RightHand"),
    ]:
        cube_part(f"Forearm_{side}", (sx * 0.67, -0.02, 1.28), (0.17, 0.16, 0.28), skin, armature, lower, rot=(0, sx * -0.18, sx * -0.10), bevel=0.035)
        sphere_part(f"Glove_{side}", (sx * 0.80, -0.03, 1.03), (0.14, 0.12, 0.12), glove, armature, hand, subdivisions=1)

    for side, sx, upper_leg, lower_leg, foot in [
        ("L", -1, "LeftUpperLeg", "LeftLowerLeg", "LeftFoot"),
        ("R", 1, "RightUpperLeg", "RightLowerLeg", "RightFoot"),
    ]:
        cube_part(f"UpperLeg_{side}", (sx * 0.20, 0, 0.81), (0.18, 0.18, 0.41), blue, armature, upper_leg, bevel=0.035)
        cube_part(f"LowerLeg_{side}", (sx * 0.22, 0, 0.38), (0.17, 0.17, 0.31), leather, armature, lower_leg, bevel=0.035)
        cube_part(f"Boot_{side}", (sx * 0.23, -0.08, 0.12), (0.19, 0.30, 0.11), leather, armature, foot, bevel=0.035)

    cube_part("Belt", (0, -0.02, 1.05), (0.52, 0.045, 0.055), leather, armature, "Hips", bevel=0.015)
    cube_part("Buckle", (0, -0.30, 1.06), (0.13, 0.035, 0.085), gold, armature, "Hips", bevel=0.012)
    cube_part("ToolPouch_L", (-0.34, -0.20, 0.98), (0.11, 0.08, 0.14), leather, armature, "Hips", bevel=0.018)
    cube_part("ToolPouch_R", (0.34, -0.20, 0.98), (0.11, 0.08, 0.14), leather, armature, "Hips", bevel=0.018)
    cylinder_part("Backpack_Tank", (0, 0.27, 1.42), 0.18, 0.46, metal, armature, "Chest", vertices=8, rot=(math.radians(90), 0, 0), scale=(1, 0.8, 1))
    cube_part("Backpack_Frame", (0, 0.31, 1.41), (0.42, 0.07, 0.38), leather, armature, "Chest", bevel=0.02)
    torus_part("Backpack_Gear", (0.23, 0.37, 1.62), 0.105, 0.018, metal, armature, "Chest", rot=(math.radians(90), 0, 0))

    cylinder_part("Hammer_Handle", (0.90, -0.04, 1.02), 0.035, 0.52, leather, armature, "RightHand", vertices=8, rot=(0, math.radians(14), 0))
    cube_part("Hammer_Head", (0.98, -0.05, 1.34), (0.21, 0.13, 0.11), metal, armature, "RightHand", rot=(0, 0, math.radians(10)), bevel=0.025)
    cylinder_part("Hammer_EndCap", (0.78, -0.05, 1.31), 0.085, 0.055, gold, armature, "RightHand", vertices=8, rot=(0, math.radians(90), 0))
    cube_part("Wrench_Belt", (-0.18, -0.32, 0.86), (0.035, 0.045, 0.23), metal, armature, "Hips", rot=(0, 0, math.radians(-18)), bevel=0.008)


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


def make_action(armature, name, frame_poses):
    action = bpy.data.actions.new(name)
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, rotations, locations in frame_poses:
        key_pose(armature, frame, rotations, locations)
    action.use_fake_user = True
    return action


def create_animations(armature):
    idle = make_action(
        armature,
        "Idle",
        [
            (1, {"Chest": (1, 0, 0), "Head": (-2, 0, 0), "RightUpperArm": (0, 0, -8)}, {"Hips": (0, 0, 0)}),
            (20, {"Chest": (-1, 0, 0), "Head": (1, 0, 0), "RightUpperArm": (0, 0, -5)}, {"Hips": (0, 0, 0.025)}),
            (40, {"Chest": (1, 0, 0), "Head": (-2, 0, 0), "RightUpperArm": (0, 0, -8)}, {"Hips": (0, 0, 0)}),
        ],
    )
    walk = make_action(
        armature,
        "Walk",
        [
            (1, {"LeftUpperLeg": (24, 0, 0), "RightUpperLeg": (-24, 0, 0), "LeftUpperArm": (-20, 0, 8), "RightUpperArm": (20, 0, -8)}, {"Hips": (0, 0, 0)}),
            (15, {"LeftUpperLeg": (-24, 0, 0), "RightUpperLeg": (24, 0, 0), "LeftUpperArm": (20, 0, 8), "RightUpperArm": (-20, 0, -8)}, {"Hips": (0, 0, 0.035)}),
            (30, {"LeftUpperLeg": (24, 0, 0), "RightUpperLeg": (-24, 0, 0), "LeftUpperArm": (-20, 0, 8), "RightUpperArm": (20, 0, -8)}, {"Hips": (0, 0, 0)}),
        ],
    )
    attack = make_action(
        armature,
        "HammerAttack",
        [
            (1, {"RightUpperArm": (-55, 0, -18), "RightLowerArm": (-25, 0, 0), "Chest": (0, 0, -6)}, {}),
            (10, {"RightUpperArm": (-110, 0, -20), "RightLowerArm": (-65, 0, 0), "Chest": (5, 0, -12), "Head": (8, 0, 0)}, {"Hips": (0, 0, 0.03)}),
            (18, {"RightUpperArm": (58, 0, -28), "RightLowerArm": (10, 0, 0), "Chest": (-8, 0, 10), "Head": (-5, 0, 0)}, {"Hips": (0, -0.02, -0.01)}),
            (30, {"RightUpperArm": (-20, 0, -10), "RightLowerArm": (0, 0, 0)}, {}),
        ],
    )
    die = make_action(
        armature,
        "Die",
        [
            (1, {}, {}),
            (18, {"Hips": (0, 0, 65), "Chest": (-20, 0, 0), "Head": (-18, 0, 0), "LeftUpperArm": (15, 0, 65), "RightUpperArm": (20, 0, -65)}, {"Hips": (0, -0.18, -0.35)}),
            (40, {"Hips": (0, 0, 82), "Chest": (-35, 0, 0), "Head": (-22, 0, 0), "LeftUpperArm": (10, 0, 80), "RightUpperArm": (12, 0, -80)}, {"Hips": (0, -0.28, -0.65)}),
        ],
    )

    armature.animation_data.action = None
    for action in [idle, walk, attack, die]:
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
    bpy.context.scene.world.color = (0.78, 0.75, 0.70)
    bpy.ops.object.light_add(type="AREA", location=(0, -4, 5))
    key = bpy.context.object
    key.name = "Preview_KeyLight"
    key.data.energy = 450
    key.data.size = 5
    camera_loc = Vector((2.6, -5.2, 2.0))
    target = Vector((0, -0.04, 1.35))
    direction = target - camera_loc
    bpy.ops.object.camera_add(location=camera_loc)
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.85
    bpy.context.scene.camera = camera
    bpy.context.scene.render.resolution_x = 1200
    bpy.context.scene.render.resolution_y = 1200


def write_readme():
    README_PATH.write_text(
        "# Engineer Prototype\n\n"
        "Generated by `Tools/Art/Blender/create_engineer_humanoid_prototype.py`.\n\n"
        "Assets:\n"
        "- `EngineerPrototype.fbx`: low-poly humanoid engineer trial model.\n"
        "- `EngineerPrototype.blend`: editable source scene.\n"
        "- `EngineerPrototype_preview.png`: quick visual preview.\n\n"
        "Embedded prototype actions:\n"
        "- `Idle`\n"
        "- `Walk`\n"
        "- `HammerAttack`\n"
        "- `Die`\n\n"
        "Import notes:\n"
        "- Intended first as a Unity visual prototype.\n"
        "- Materials are intentionally matte: metallic 0, high roughness.\n"
        "- Try Unity Rig = Humanoid first. If Avatar mapping is not valid, use Generic for this prototype.\n"
        "- Reusable humanoid animation should target the final Humanoid-ready version after the silhouette is accepted.\n",
        encoding="utf-8",
    )


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
    write_readme()


def main():
    clean_scene()
    armature = create_armature()
    create_parts(armature)
    create_animations(armature)
    setup_scene()
    export_assets()
    print(f"Engineer prototype written to: {OUT_DIR}")


if __name__ == "__main__":
    main()
