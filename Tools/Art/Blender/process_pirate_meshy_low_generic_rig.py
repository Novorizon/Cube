import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[3]
SRC_DIR = ROOT / "Assets" / "Arts" / "Character" / "Incoming" / "Pirate_Meshy" / "low"
SRC_FBX = SRC_DIR / "Meshy_AI_Captain_Lowpoly_0606044039_texture.fbx"
OUT_DIR = SRC_DIR / "Processed"
OUT_FBX = OUT_DIR / "Pirate_Low_QGenericRigV2.fbx"
OUT_BLEND = OUT_DIR / "Pirate_Low_QGenericRigV2.blend"
OUT_PREVIEW = OUT_DIR / "Pirate_Low_QGenericRigV2_preview.png"
README = OUT_DIR / "README.md"


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def import_source():
    bpy.ops.import_scene.fbx(filepath=str(SRC_FBX))
    mesh_objs = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not mesh_objs:
        raise RuntimeError(f"No mesh objects found in {SRC_FBX}")
    for obj in mesh_objs:
        obj.select_set(False)
    return mesh_objs


def bounds(mesh_objs):
    coords = [obj.matrix_world @ vertex.co for obj in mesh_objs for vertex in obj.data.vertices]
    mins = Vector((min(v.x for v in coords), min(v.y for v in coords), min(v.z for v in coords)))
    maxs = Vector((max(v.x for v in coords), max(v.y for v in coords), max(v.z for v in coords)))
    return mins, maxs, maxs - mins


def create_armature(mins, maxs, size):
    height = size.z
    center_x = (mins.x + maxs.x) * 0.5
    center_y = (mins.y + maxs.y) * 0.5
    half_width = size.x * 0.5
    front_y = mins.y

    z = lambda ratio: mins.z + height * ratio
    x = lambda offset: center_x + half_width * offset
    y = lambda offset: center_y + size.y * offset

    arm_data = bpy.data.armatures.new("Q_Biped_GenericRig_Data")
    arm_obj = bpy.data.objects.new("Q_Biped_GenericRig", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    specs = {
        "Root": ((center_x, center_y, z(0.00)), (center_x, center_y, z(0.11)), None),
        "Hips": ((center_x, center_y, z(0.26)), (center_x, center_y, z(0.39)), "Root"),
        "Spine": ((center_x, center_y, z(0.39)), (center_x, center_y, z(0.52)), "Hips"),
        "Chest": ((center_x, center_y, z(0.52)), (center_x, center_y, z(0.66)), "Spine"),
        "Neck": ((center_x, center_y, z(0.65)), (center_x, center_y, z(0.73)), "Chest"),
        "Head": ((center_x, center_y, z(0.72)), (center_x, center_y, z(0.96)), "Neck"),
        "Hat": ((center_x, center_y, z(0.86)), (center_x, center_y, z(1.05)), "Head"),
        "Beard": ((center_x, y(-0.34), z(0.64)), (center_x, y(-0.37), z(0.49)), "Head"),
        "CoatTail": ((center_x, y(0.25), z(0.39)), (center_x, y(0.30), z(0.16)), "Hips"),
        "Shoulder_L": ((x(-0.25), y(-0.02), z(0.60)), (x(-0.39), y(-0.05), z(0.58)), "Chest"),
        "Arm_L": ((x(-0.39), y(-0.05), z(0.58)), (x(-0.60), y(-0.10), z(0.50)), "Shoulder_L"),
        "Forearm_L": ((x(-0.60), y(-0.10), z(0.50)), (x(-0.80), y(-0.14), z(0.43)), "Arm_L"),
        "Hand_L": ((x(-0.80), y(-0.14), z(0.43)), (x(-0.98), y(-0.16), z(0.40)), "Forearm_L"),
        "Shoulder_R": ((x(0.25), y(-0.02), z(0.60)), (x(0.39), y(-0.05), z(0.58)), "Chest"),
        "Arm_R": ((x(0.39), y(-0.05), z(0.58)), (x(0.60), y(-0.10), z(0.50)), "Shoulder_R"),
        "Forearm_R": ((x(0.60), y(-0.10), z(0.50)), (x(0.80), y(-0.14), z(0.43)), "Arm_R"),
        "Hand_R": ((x(0.80), y(-0.14), z(0.43)), (x(0.98), y(-0.16), z(0.40)), "Forearm_R"),
        "Leg_L": ((x(-0.18), center_y, z(0.29)), (x(-0.23), center_y, z(0.15)), "Hips"),
        "Foot_L": ((x(-0.23), center_y, z(0.15)), (x(-0.23), front_y, z(0.04)), "Leg_L"),
        "Leg_R": ((x(0.18), center_y, z(0.29)), (x(0.23), center_y, z(0.15)), "Hips"),
        "Foot_R": ((x(0.23), center_y, z(0.15)), (x(0.23), front_y, z(0.04)), "Leg_R"),
        "Weapon": ((x(0.83), y(-0.18), z(0.42)), (x(1.05), y(-0.20), z(0.45)), "Hand_R"),
    }

    created = {}
    for name, (head, tail, parent) in specs.items():
        bone = arm_data.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        if parent:
            bone.parent = created[parent]
            bone.use_connect = False
        created[name] = bone

    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm_obj.pose.bones:
        bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def parent_with_auto_weights(mesh_objs, armature):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in mesh_objs:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        armature.select_set(True)
        bpy.context.view_layer.objects.active = armature
        try:
            bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        except RuntimeError:
            obj.parent = armature
            mod = obj.modifiers.new(name="Q_Biped_GenericRig", type="ARMATURE")
            mod.object = armature
        obj.select_set(False)
        armature.select_set(False)
        for mod in obj.modifiers:
            if mod.type == "ARMATURE":
                mod.show_in_editmode = True
        obj.name = "Pirate_Low_QMesh"


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


def create_actions(armature):
    actions = [
        make_action(
            armature,
            "Idle",
            [
                (1, {"Chest": (0, 0, 0), "Head": (-3, 0, 0), "Arm_L": (0, 0, 10), "Arm_R": (0, 0, -10), "Beard": (4, 0, 0)}, {}),
                (20, {"Chest": (-4, 0, 0), "Head": (3, 0, 0), "Arm_L": (0, 0, 3), "Arm_R": (0, 0, -3), "Beard": (-3, 0, 0)}, {"Root": (0, 0, 0.035)}),
                (40, {"Chest": (0, 0, 0), "Head": (-3, 0, 0), "Arm_L": (0, 0, 10), "Arm_R": (0, 0, -10), "Beard": (4, 0, 0)}, {}),
            ],
        ),
        make_action(
            armature,
            "Walk",
            [
                (1, {"Leg_L": (32, 0, 0), "Leg_R": (-28, 0, 0), "Foot_L": (-8, 0, 0), "Foot_R": (12, 0, 0), "Arm_L": (-18, 0, 18), "Arm_R": (18, 0, -18), "Chest": (0, 0, -8), "Head": (0, 0, 5)}, {}),
                (8, {"Leg_L": (0, 0, 0), "Leg_R": (0, 0, 0), "Foot_L": (0, 0, 0), "Foot_R": (0, 0, 0), "Arm_L": (0, 0, 6), "Arm_R": (0, 0, -6), "Chest": (-3, 0, 0)}, {"Root": (0, 0, 0.045)}),
                (16, {"Leg_L": (-28, 0, 0), "Leg_R": (32, 0, 0), "Foot_L": (12, 0, 0), "Foot_R": (-8, 0, 0), "Arm_L": (18, 0, 18), "Arm_R": (-18, 0, -18), "Chest": (0, 0, 8), "Head": (0, 0, -5)}, {}),
                (24, {"Leg_L": (0, 0, 0), "Leg_R": (0, 0, 0), "Foot_L": (0, 0, 0), "Foot_R": (0, 0, 0), "Arm_L": (0, 0, 6), "Arm_R": (0, 0, -6), "Chest": (-3, 0, 0)}, {"Root": (0, 0, 0.045)}),
                (32, {"Leg_L": (32, 0, 0), "Leg_R": (-28, 0, 0), "Foot_L": (-8, 0, 0), "Foot_R": (12, 0, 0), "Arm_L": (-18, 0, 18), "Arm_R": (18, 0, -18), "Chest": (0, 0, -8), "Head": (0, 0, 5)}, {}),
            ],
        ),
        make_action(
            armature,
            "Attack",
            [
                (1, {"Chest": (0, 0, -8), "Head": (0, 0, -8), "Arm_R": (-35, 0, -35), "Forearm_R": (-20, 0, -12), "Arm_L": (5, 0, 20)}, {}),
                (8, {"Chest": (12, 0, -28), "Head": (5, 0, -15), "Arm_R": (-92, 0, -52), "Forearm_R": (-45, 0, -18), "Arm_L": (-15, 0, 35)}, {"Root": (0, 0, 0.035)}),
                (15, {"Chest": (-18, 0, 35), "Head": (-6, 0, 18), "Arm_R": (70, 0, -55), "Forearm_R": (38, 0, 0), "Arm_L": (22, 0, -24)}, {"Root": (0, -0.050, -0.010)}),
                (25, {"Chest": (0, 0, 5), "Arm_R": (10, 0, -24), "Forearm_R": (12, 0, -8), "Arm_L": (5, 0, 10)}, {}),
                (36, {"Chest": (0, 0, -8), "Arm_R": (-20, 0, -22), "Forearm_R": (0, 0, -4)}, {}),
            ],
        ),
        make_action(
            armature,
            "Hit",
            [
                (1, {}, {}),
                (5, {"Root": (-6, 0, 0), "Chest": (-24, 0, 0), "Head": (-16, 0, 0), "Arm_L": (18, 0, 30), "Arm_R": (18, 0, -30)}, {"Root": (0, 0.040, 0.025)}),
                (10, {"Root": (3, 0, 0), "Chest": (8, 0, 0), "Head": (10, 0, 0)}, {"Root": (0, -0.010, 0)}),
                (18, {}, {}),
            ],
        ),
        make_action(
            armature,
            "Die",
            [
                (1, {}, {}),
                (12, {"Root": (0, 0, 18), "Chest": (-22, 0, 0), "Head": (-18, 0, 0), "Arm_L": (20, 0, 45), "Arm_R": (20, 0, -45)}, {"Root": (0, -0.05, -0.08)}),
                (26, {"Root": (0, 0, 74), "Chest": (-52, 0, 0), "Head": (-30, 0, 0), "Arm_L": (10, 0, 95), "Arm_R": (10, 0, -95), "CoatTail": (-25, 0, 0)}, {"Root": (0, -0.20, -0.32)}),
                (44, {"Root": (0, 0, 88), "Chest": (-58, 0, 0), "Head": (-34, 0, 0), "Arm_L": (0, 0, 100), "Arm_R": (0, 0, -100), "CoatTail": (-30, 0, 0)}, {"Root": (0, -0.24, -0.42)}),
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
    bpy.context.scene.frame_end = 44
    clear_pose(armature)


def setup_preview(mesh_objs, armature):
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.world.color = (0.08, 0.08, 0.08)
    bpy.ops.object.light_add(type="AREA", location=(-2.5, -3.5, 4.0))
    key = bpy.context.object
    key.name = "Preview_KeyLight"
    key.data.energy = 500
    key.data.size = 4.5
    bpy.ops.object.camera_add(location=(1.7, -3.0, 0.92))
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    target = Vector((0, 0, 0.52))
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.35
    bpy.context.scene.camera = camera
    bpy.context.scene.render.resolution_x = 1200
    bpy.context.scene.render.resolution_y = 1200
    bpy.context.scene.frame_set(1)


def stats(mesh_objs):
    verts = sum(len(obj.data.vertices) for obj in mesh_objs)
    tris = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in mesh_objs)
    return {"objects": len(mesh_objs), "verts": verts, "tris": tris}


def write_readme(mesh_objs):
    s = stats(mesh_objs)
    README.write_text(
        "# Pirate Low Q Generic Rig\n\n"
        "Generated from Meshy static FBX by `Tools/Art/Blender/process_pirate_meshy_low_generic_rig.py`.\n\n"
        "Source:\n"
        "- `../Meshy_AI_Captain_Lowpoly_0606044039_texture.fbx`\n\n"
        "Outputs:\n"
        "- `Pirate_Low_QGenericRigV2.fbx`\n"
        "- `Pirate_Low_QGenericRigV2.blend`\n"
        "- `Pirate_Low_QGenericRigV2_preview.png`\n\n"
        "Embedded prototype actions:\n"
        "- `Idle`\n"
        "- `Walk`\n"
        "- `Attack`\n"
        "- `Hit`\n"
        "- `Die`\n\n"
        f"Stats: `{s['objects']}` mesh object, `{s['verts']}` vertices, about `{s['tris']}` triangles.\n\n"
        "Unity notes:\n"
        "- Import as Generic, not Humanoid.\n"
        "- Use the stylized matte material in this folder for first visual checks.\n"
        "- This is an automated first rig pass. Check shoulders, hands, coat/hat deformation, and feet before promoting it to production.\n",
        encoding="utf-8",
    )


def export_assets(mesh_objs):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    bpy.ops.export_scene.fbx(
        filepath=str(OUT_FBX),
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        apply_scale_options="FBX_SCALE_UNITS",
        object_types={"ARMATURE", "MESH"},
        mesh_smooth_type="FACE",
    )
    bpy.context.scene.render.filepath = str(OUT_PREVIEW)
    bpy.ops.render.render(write_still=True)
    write_readme(mesh_objs)


def main():
    clean_scene()
    mesh_objs = import_source()
    mins, maxs, size = bounds(mesh_objs)
    armature = create_armature(mins, maxs, size)
    parent_with_auto_weights(mesh_objs, armature)
    create_actions(armature)
    setup_preview(mesh_objs, armature)
    export_assets(mesh_objs)
    s = stats(mesh_objs)
    print(
        "PIRATE_Q_GENERIC_STATS "
        f"objects={s['objects']} verts={s['verts']} tris={s['tris']} "
        f"actions={','.join(action.name for action in bpy.data.actions)}"
    )


if __name__ == "__main__":
    main()
