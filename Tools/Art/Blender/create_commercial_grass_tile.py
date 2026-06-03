import math
import os
import random

import bpy
from mathutils import Vector


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUTPUT_DIR = os.path.join(ROOT_DIR, "Assets", "Arts", "Map", "Tiles", "Generated")
BLEND_PATH = os.path.join(OUTPUT_DIR, "CommercialGrassTilePrototype.blend")
FBX_PATH = os.path.join(OUTPUT_DIR, "CommercialGrassTilePrototype.fbx")
PREVIEW_PATH = os.path.join(OUTPUT_DIR, "CommercialGrassTilePrototype_preview.png")


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
    if "Alpha" in principled.inputs:
        principled.inputs["Alpha"].default_value = color[3]
    mat.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    mat.diffuse_color = color
    return mat


def point_camera_at(camera, target):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def make_rounded_box(name, size_x, size_y, size_z, center_z, material, bevel=0.035, segments=4):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, center_z))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size_x, size_y, size_z)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bevel_modifier = obj.modifiers.new("SoftBevel", "BEVEL")
    bevel_modifier.width = bevel
    bevel_modifier.segments = segments
    bevel_modifier.affect = "EDGES"

    weighted_normal = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    weighted_normal.keep_sharp = True

    obj.data.materials.append(material)
    return obj


def make_top_plane(name, size, z, material):
    half = size * 0.5
    mesh = bpy.data.meshes.new(name + "Mesh")
    verts = [
        (-half, -half, z),
        (half, -half, z),
        (half, half, z),
        (-half, half, z),
    ]
    faces = [(0, 1, 2, 3)]
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    uv_layer = mesh.uv_layers.new(name="UVMap")
    uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
    for i, loop in enumerate(mesh.polygons[0].loop_indices):
        uv_layer.data[loop].uv = uvs[i]

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def make_leaf_patch(name, x, y, z, radius, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=radius, location=(x, y, z))
    obj = bpy.context.object
    obj.name = name
    obj.scale.z = 0.08
    obj.rotation_euler.z = random.uniform(0, math.pi)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def make_flower(name, x, y, z, material):
    pieces = []
    petal_mat = material
    for i in range(5):
        angle = i * math.tau / 5.0
        px = x + math.cos(angle) * 0.015
        py = y + math.sin(angle) * 0.015
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.012, location=(px, py, z))
        petal = bpy.context.object
        petal.name = name + "_Petal"
        petal.scale.z = 0.05
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        petal.data.materials.append(petal_mat)
        pieces.append(petal)
    return pieces


def make_side_grass_blades(parent, material):
    blades = []
    for side in range(4):
        for i in range(10):
            blade_mesh = bpy.data.meshes.new("SideGrassBladeMesh")
            verts = [(-0.006, 0, 0), (0.006, 0, 0), (0, 0, 0.08)]
            faces = [(0, 1, 2)]
            blade_mesh.from_pydata(verts, [], faces)
            blade_mesh.update()
            blade_mesh.materials.append(material)

            offset = -0.42 + i * 0.09 + random.uniform(-0.015, 0.015)
            z = 0.69 + random.uniform(-0.015, 0.015)
            if side == 0:
                loc = (offset, -0.503, z)
                rot = (math.radians(90), 0, random.uniform(-0.2, 0.2))
            elif side == 1:
                loc = (offset, 0.503, z)
                rot = (math.radians(90), 0, math.pi + random.uniform(-0.2, 0.2))
            elif side == 2:
                loc = (-0.503, offset, z)
                rot = (math.radians(90), 0, math.radians(90) + random.uniform(-0.2, 0.2))
            else:
                loc = (0.503, offset, z)
                rot = (math.radians(90), 0, math.radians(-90) + random.uniform(-0.2, 0.2))

            obj = bpy.data.objects.new("SideGrassBlade", blade_mesh)
            bpy.context.collection.objects.link(obj)
            obj.location = loc
            obj.rotation_euler = rot
            obj.parent = parent
            blades.append(obj)
    return blades


def parent_to(obj, parent):
    obj.parent = parent
    return obj


def build_tile():
    random.seed(31)
    clear_scene()
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    mat_rock = make_material("Rock_Warm_Dark", (0.16, 0.17, 0.18, 1.0), 0.85)
    mat_soil = make_material("Soil_Warm_Brown", (0.42, 0.23, 0.10, 1.0), 0.82)
    mat_soil_dark = make_material("Soil_Detail_Dark", (0.25, 0.12, 0.055, 1.0), 0.9)
    mat_grass = make_material("Grass_Soft_Main", (0.38, 0.58, 0.10, 1.0), 0.72)
    mat_grass_light = make_material("Grass_Soft_Light", (0.46, 0.66, 0.13, 1.0), 0.7)
    mat_grass_dark = make_material("Grass_Soft_Dark", (0.20, 0.38, 0.06, 1.0), 0.8)
    mat_flower = make_material("Flower_Cream", (0.92, 0.86, 0.45, 1.0), 0.65)

    root = bpy.data.objects.new("CommercialGrassTilePrototype", None)
    bpy.context.collection.objects.link(root)

    base = bpy.data.objects.new("Base", None)
    bpy.context.collection.objects.link(base)
    base.parent = root

    topic = bpy.data.objects.new("Topic", None)
    bpy.context.collection.objects.link(topic)
    topic.parent = root

    rock = parent_to(make_rounded_box("Rock", 0.98, 0.98, 0.32, 0.16, mat_rock, bevel=0.03, segments=3), base)
    soil = parent_to(make_rounded_box("Soil", 0.99, 0.99, 0.40, 0.52, mat_soil, bevel=0.035, segments=4), base)
    grass_body = parent_to(make_rounded_box("GrassBody", 1.0, 1.0, 0.28, 0.86, mat_grass, bevel=0.055, segments=6), topic)
    top_plane = parent_to(make_top_plane("TopicTop", 0.94, 1.004, mat_grass_light), topic)
    rock.color = mat_rock.diffuse_color
    soil.color = mat_soil.diffuse_color
    grass_body.color = mat_grass.diffuse_color
    top_plane.color = mat_grass_light.diffuse_color

    for i in range(26):
        x = random.uniform(-0.38, 0.38)
        y = random.uniform(-0.38, 0.38)
        radius = random.uniform(0.025, 0.07)
        mat = mat_grass_dark if i % 3 == 0 else mat_grass
        patch = make_leaf_patch("TopSoftPatch", x, y, 1.012, radius, mat)
        patch.parent = topic

    for i in range(8):
        x = random.uniform(-0.38, 0.38)
        y = random.uniform(-0.38, 0.38)
        for flower_part in make_flower("TinyFlower", x, y, 1.017, mat_flower):
            flower_part.parent = topic

    make_side_grass_blades(topic, mat_grass_dark)

    # Add a few soil side facets so the sides do not read as a flat box.
    for i in range(16):
        side = i % 4
        offset = random.uniform(-0.38, 0.38)
        z = random.uniform(0.38, 0.65)
        bpy.ops.mesh.primitive_cube_add(size=1)
        chip = bpy.context.object
        chip.name = "SoilFacet"
        chip.dimensions = (0.08, 0.012, 0.05)
        if side == 0:
            chip.location = (offset, -0.501, z)
        elif side == 1:
            chip.location = (offset, 0.501, z)
        elif side == 2:
            chip.dimensions = (0.012, 0.08, 0.05)
            chip.location = (-0.501, offset, z)
        else:
            chip.dimensions = (0.012, 0.08, 0.05)
            chip.location = (0.501, offset, z)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        chip.data.materials.append(mat_soil_dark)
        chip.parent = base

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (1.25, -1.45, 1.28)
    point_camera_at(camera, (0, 0, 0.55))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.65
    bpy.context.scene.camera = camera

    light_data = bpy.data.lights.new("KeyLight", "AREA")
    light = bpy.data.objects.new("KeyLight", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (-2.0, -2.0, 3.0)
    light.data.energy = 450
    light.data.size = 4.0

    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    if hasattr(bpy.context.scene, "eevee"):
        bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.resolution_x = 1024
    bpy.context.scene.render.resolution_y = 1024
    bpy.context.scene.world.color = (0.42, 0.40, 0.36)
    bpy.context.scene.view_settings.view_transform = "Standard"
    bpy.context.scene.view_settings.look = "Medium High Contrast"
    bpy.context.scene.view_settings.exposure = 0
    bpy.context.scene.view_settings.gamma = 1

    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    root.select_set(True)
    bpy.context.view_layer.objects.active = root

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
    build_tile()
