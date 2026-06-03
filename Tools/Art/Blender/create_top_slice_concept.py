import math
import os

import bpy
from mathutils import Vector


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUTPUT_DIR = os.path.join(ROOT_DIR, "Assets", "Arts", "Map", "Tiles", "Generated")
BLEND_PATH = os.path.join(OUTPUT_DIR, "TopSliceConcept.blend")
FBX_PATH = os.path.join(OUTPUT_DIR, "TopSliceConcept.fbx")
PREVIEW_PATH = os.path.join(OUTPUT_DIR, "TopSliceConcept_preview.png")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_material(name, color, roughness=0.75):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    output = mat.node_tree.nodes.new(type="ShaderNodeOutputMaterial")
    principled = mat.node_tree.nodes.new(type="ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = roughness
    mat.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    mat.diffuse_color = color
    return mat


def point_camera_at(camera, target):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def make_rounded_box(name, dimensions, location, material, bevel=0.035, segments=4):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bevel_modifier = obj.modifiers.new("SoftBevel", "BEVEL")
    bevel_modifier.width = bevel
    bevel_modifier.segments = segments
    bevel_modifier.affect = "EDGES"

    normal_modifier = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    normal_modifier.keep_sharp = True

    obj.data.materials.append(material)
    obj.color = material.diffuse_color
    return obj


def parent_to(obj, parent):
    obj.parent = parent
    return obj


def add_patch(root, name, x, y, width, height, material, bevel=0.018):
    patch = make_rounded_box(
        name,
        (width, height, 0.018),
        (x, y, 1.014),
        material,
        bevel=bevel,
        segments=4,
    )
    patch.parent = root
    return patch


def build_concept():
    clear_scene()
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    mat_rock = make_material("Rock_Dark", (0.20, 0.22, 0.22, 1))
    mat_soil = make_material("Soil_Warm", (0.48, 0.27, 0.13, 1))
    mat_grass_body = make_material("Continuous_GrassBody", (0.34, 0.56, 0.10, 1))
    mat_grass = make_material("Top_Grass", (0.48, 0.68, 0.15, 1))
    mat_grass_alt = make_material("Top_Grass_Alt", (0.39, 0.60, 0.12, 1))
    mat_snow = make_material("Top_Snow", (0.88, 0.94, 0.98, 1))
    mat_water = make_material("Top_Water", (0.10, 0.58, 0.78, 1))
    mat_sand = make_material("Top_SandHill", (0.70, 0.49, 0.23, 1))
    mat_corner_cool = make_material("Top_Corner_Cool", (0.58, 0.78, 0.55, 1))
    mat_corner_warm = make_material("Top_Corner_Warm", (0.60, 0.60, 0.18, 1))

    root = bpy.data.objects.new("TopSliceConcept", None)
    bpy.context.collection.objects.link(root)

    base = bpy.data.objects.new("Base", None)
    bpy.context.collection.objects.link(base)
    base.parent = root

    topic = bpy.data.objects.new("Topic", None)
    bpy.context.collection.objects.link(topic)
    topic.parent = root

    parent_to(make_rounded_box("Rock", (1.0, 1.0, 0.28), (0, 0, 0.14), mat_rock, 0.03, 3), base)
    parent_to(make_rounded_box("Soil", (1.0, 1.0, 0.40), (0, 0, 0.48), mat_soil, 0.035, 4), base)
    parent_to(make_rounded_box("TopicBody_Continuous_NotSplit", (1.0, 1.0, 0.32), (0, 0, 0.84), mat_grass_body, 0.06, 6), topic)

    patch_root = bpy.data.objects.new("TopPatches_9Pieces", None)
    bpy.context.collection.objects.link(patch_root)
    patch_root.parent = topic

    total = 0.90
    edge = 0.22
    center = total - edge * 2
    c = 0
    n = center / 2 + edge / 2

    add_patch(patch_root, "Top_NW_Corner", -n, n, edge, edge, mat_snow)
    add_patch(patch_root, "Top_N_Edge", c, n, center, edge, mat_snow)
    add_patch(patch_root, "Top_NE_Corner", n, n, edge, edge, mat_corner_cool)
    add_patch(patch_root, "Top_W_Edge", -n, c, edge, center, mat_grass_alt)
    add_patch(patch_root, "Top_Center", c, c, center, center, mat_grass)
    add_patch(patch_root, "Top_E_Edge", n, c, edge, center, mat_water)
    add_patch(patch_root, "Top_SW_Corner", -n, -n, edge, edge, mat_corner_warm)
    add_patch(patch_root, "Top_S_Edge", c, -n, center, edge, mat_sand)
    add_patch(patch_root, "Top_SE_Corner", n, -n, edge, edge, mat_water)

    # Tiny raised details live on top of the patches, proving the visual layer is separate.
    for i, (x, y, mat, radius) in enumerate(
        [
            (-0.16, 0.06, mat_grass_alt, 0.030),
            (0.04, -0.10, mat_grass_alt, 0.025),
            (0.16, 0.14, mat_grass_alt, 0.028),
            (-0.05, 0.18, mat_snow, 0.018),
        ]
    ):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=radius, location=(x, y, 1.034))
        dot = bpy.context.object
        dot.name = "TopDetail"
        dot.scale.z = 0.10
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        dot.data.materials.append(mat)
        dot.parent = patch_root

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (1.35, -1.55, 1.38)
    point_camera_at(camera, (0, 0, 0.60))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.62
    bpy.context.scene.camera = camera

    light_data = bpy.data.lights.new("KeyLight", "AREA")
    light = bpy.data.objects.new("KeyLight", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (-2.2, -2.0, 3.5)
    light.data.energy = 520
    light.data.size = 4.5

    fill_data = bpy.data.lights.new("FillLight", "POINT")
    fill = bpy.data.objects.new("FillLight", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (2.0, 2.0, 1.6)
    fill.data.energy = 70

    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    if hasattr(bpy.context.scene, "eevee"):
        bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.resolution_x = 1024
    bpy.context.scene.render.resolution_y = 1024
    bpy.context.scene.world.color = (0.42, 0.40, 0.36)
    bpy.context.scene.view_settings.view_transform = "Standard"
    bpy.context.scene.view_settings.look = "Medium High Contrast"

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
    )
    bpy.context.scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    print("Saved blend:", BLEND_PATH)
    print("Saved fbx:", FBX_PATH)
    print("Saved preview:", PREVIEW_PATH)


if __name__ == "__main__":
    build_concept()
