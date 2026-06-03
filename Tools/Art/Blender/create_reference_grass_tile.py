import math
import os
import random

import bpy
from mathutils import Vector


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUTPUT_DIR = os.path.join(ROOT_DIR, "Assets", "Arts", "Map", "Tiles", "Generated")
BLEND_PATH = os.path.join(OUTPUT_DIR, "ReferenceStyleGrassTile.blend")
FBX_PATH = os.path.join(OUTPUT_DIR, "ReferenceStyleGrassTile.fbx")
PREVIEW_PATH = os.path.join(OUTPUT_DIR, "ReferenceStyleGrassTile_preview.png")

ROCK_HEIGHT = 0.26
SOIL_HEIGHT = 0.40
TOP_HEIGHT = 0.36
ROCK_CENTER_Z = ROCK_HEIGHT * 0.5
SOIL_CENTER_Z = ROCK_HEIGHT + SOIL_HEIGHT * 0.5
TOP_CENTER_Z = 1.0 - TOP_HEIGHT * 0.5

TILE_SIZE = 1.0
TOP_SIZE = 1.04
ROCK_BEVEL = 0.035
SOIL_BEVEL = 0.045
TOP_BEVEL = 0.068


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_material(name, color, roughness=0.82):
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


def make_rounded_box(name, dimensions, location, material, bevel=0.03, segments=4):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bevel_mod = obj.modifiers.new("SoftBevel", "BEVEL")
    bevel_mod.width = bevel
    bevel_mod.segments = segments
    bevel_mod.affect = "EDGES"

    normal_mod = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    normal_mod.keep_sharp = True

    obj.data.materials.append(material)
    obj.color = material.diffuse_color
    return obj


def add_flat_disc(name, x, y, z, radius, material, scale_y=0.75, segments=18):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=8, radius=radius, location=(x, y, z))
    obj = bpy.context.object
    obj.name = name
    obj.scale.y = scale_y
    obj.scale.z = 0.035
    obj.rotation_euler.z = random.uniform(0, math.tau)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def add_grass_clump(parent, x, y, z, material, scale=1.0):
    clump = bpy.data.objects.new("GrassClump", None)
    bpy.context.collection.objects.link(clump)
    clump.parent = parent
    for i in range(5):
        angle = -0.55 + i * 0.275
        height = random.uniform(0.045, 0.075) * scale
        width = random.uniform(0.010, 0.016) * scale
        mesh = bpy.data.meshes.new("GrassBladeMesh")
        verts = [(-width, 0, 0), (width, 0, 0), (0, 0, height)]
        faces = [(0, 1, 2)]
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        mesh.materials.append(material)
        blade = bpy.data.objects.new("GrassBlade", mesh)
        bpy.context.collection.objects.link(blade)
        blade.location = (x, y, z)
        blade.rotation_euler = (0, 0, angle + random.uniform(-0.08, 0.08))
        blade.parent = clump
    return clump


def add_flower(parent, x, y, z, petal_mat, center_mat):
    flower = bpy.data.objects.new("TinyFlower", None)
    bpy.context.collection.objects.link(flower)
    flower.parent = parent
    for i in range(5):
        angle = i * math.tau / 5
        petal = add_flat_disc(
            "FlowerPetal",
            x + math.cos(angle) * 0.012,
            y + math.sin(angle) * 0.012,
            z,
            0.008,
            petal_mat,
            scale_y=0.60,
            segments=10,
        )
        petal.parent = flower
    center = add_flat_disc("FlowerCenter", x, y, z + 0.002, 0.005, center_mat, scale_y=1.0, segments=10)
    center.parent = flower
    return flower


def add_side_grass_fringe(parent, mat_light, mat_dark):
    # Fixed vertical grass fringe for the tile's own side style. The tile
    # outline stays square; this is not a neighbor-dependent transition.
    for side in ["front", "back", "left", "right"]:
        count = 11
        for i in range(count):
            t = (i + 0.5) / count
            offset = -0.44 + t * 0.88 + random.uniform(-0.012, 0.012)
            length = random.uniform(0.030, 0.055)
            width = random.uniform(0.050, 0.080)
            mat = mat_light if i % 3 else mat_dark

            if side == "front":
                loc = (offset, -0.507, 0.705 - length * 0.5)
                dims = (width, 0.010, length)
            elif side == "back":
                loc = (offset, 0.507, 0.705 - length * 0.5)
                dims = (width, 0.010, length)
            elif side == "left":
                loc = (-0.507, offset, 0.705 - length * 0.5)
                dims = (0.010, width, length)
            else:
                loc = (0.507, offset, 0.705 - length * 0.5)
                dims = (0.010, width, length)

            drip = make_rounded_box("GrassSideScallop", dims, loc, mat, bevel=0.012, segments=3)
            drip.parent = parent


def add_soil_facets(parent, materials):
    for side in ["front", "left", "right"]:
        count = 8 if side != "right" else 6
        for i in range(count):
            t = (i + 0.5) / count
            offset = -0.42 + t * 0.84 + random.uniform(-0.012, 0.012)
            z = random.uniform(0.38, 0.58)
            mat = random.choice(materials)
            if side == "front":
                loc = (offset, -0.506, z)
                dims = (random.uniform(0.035, 0.065), 0.014, random.uniform(0.030, 0.060))
            elif side == "left":
                loc = (-0.506, offset, z)
                dims = (0.014, random.uniform(0.035, 0.065), random.uniform(0.030, 0.060))
            else:
                loc = (0.506, offset, z)
                dims = (0.014, random.uniform(0.035, 0.065), random.uniform(0.030, 0.060))
            facet = make_rounded_box("SoilFacet", dims, loc, mat, bevel=0.006, segments=2)
            facet.parent = parent


def add_rock_blocks(parent, mat_rock, mat_rock_light):
    # Decorative stone blocks on front/side faces, approximating the reference map's rock base.
    for side in ["front", "left", "right"]:
        count = 5 if side != "right" else 4
        for i in range(count):
            t = (i + 0.5) / count
            offset = -0.40 + t * 0.80
            z = random.uniform(0.08, 0.20)
            mat = mat_rock_light if i % 2 else mat_rock
            if side == "front":
                loc = (offset, -0.508, z)
                dims = (0.13, 0.020, 0.12)
            elif side == "left":
                loc = (-0.508, offset, z)
                dims = (0.020, 0.13, 0.12)
            else:
                loc = (0.508, offset, z)
                dims = (0.020, 0.13, 0.12)
            block = make_rounded_box("RockFaceBlock", dims, loc, mat, bevel=0.018, segments=3)
            block.parent = parent


def build_tile():
    random.seed(602)
    clear_scene()
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    mat_rock = make_material("Rock_DarkBlueGray", (0.14, 0.17, 0.18, 1))
    mat_rock_light = make_material("Rock_BlueGray_Light", (0.22, 0.27, 0.28, 1))
    mat_soil = make_material("Soil_WarmBrown", (0.47, 0.25, 0.10, 1))
    mat_soil_light = make_material("Soil_LightFacet", (0.62, 0.36, 0.16, 1))
    mat_soil_dark = make_material("Soil_DarkFacet", (0.32, 0.16, 0.07, 1))
    mat_grass_side = make_material("Grass_Side", (0.27, 0.48, 0.075, 1))
    mat_grass_top = make_material("Grass_Top", (0.32, 0.55, 0.085, 1))
    mat_grass_light = make_material("Grass_LightDetail", (0.43, 0.64, 0.13, 1))
    mat_grass_dark = make_material("Grass_DarkDetail", (0.18, 0.35, 0.055, 1))
    mat_flower_white = make_material("Flower_White", (0.96, 0.94, 0.80, 1))
    mat_flower_yellow = make_material("Flower_Yellow", (0.96, 0.75, 0.20, 1))

    root = bpy.data.objects.new("ReferenceStyleGrassTile", None)
    bpy.context.collection.objects.link(root)
    topic = bpy.data.objects.new("Topic", None)
    base = bpy.data.objects.new("Base", None)
    for obj in [topic, base]:
        bpy.context.collection.objects.link(obj)
        obj.parent = root

    rock = make_rounded_box("Rock", (TILE_SIZE, TILE_SIZE, ROCK_HEIGHT), (0, 0, ROCK_CENTER_Z), mat_rock, bevel=ROCK_BEVEL, segments=4)
    soil = make_rounded_box("Soil", (TILE_SIZE, TILE_SIZE, SOIL_HEIGHT), (0, 0, SOIL_CENTER_Z), mat_soil, bevel=SOIL_BEVEL, segments=5)
    rock.parent = base
    soil.parent = base

    top_body = make_rounded_box("TopBody", (TOP_SIZE, TOP_SIZE, TOP_HEIGHT), (0, 0, TOP_CENTER_Z), mat_grass_top, bevel=TOP_BEVEL, segments=8)
    top_body.parent = topic

    add_side_grass_fringe(topic, mat_grass_light, mat_grass_dark)
    add_soil_facets(base, [mat_soil_light, mat_soil_dark])
    add_rock_blocks(base, mat_rock, mat_rock_light)

    # Top flowers, grass clumps, stones, and paint spots are intentionally not
    # baked into the base tile. They should be placed by decoration prefabs.

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (1.45, -1.62, 1.34)
    point_camera_at(camera, (0, 0, 0.58))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.52
    bpy.context.scene.camera = camera

    key_data = bpy.data.lights.new("KeyLight", "AREA")
    key = bpy.data.objects.new("KeyLight", key_data)
    bpy.context.collection.objects.link(key)
    key.location = (-2.2, -2.0, 3.6)
    key.data.energy = 440
    key.data.size = 4.4

    fill_data = bpy.data.lights.new("FillLight", "POINT")
    fill = bpy.data.objects.new("FillLight", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (1.8, 2.0, 1.5)
    fill.data.energy = 70

    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    if hasattr(bpy.context.scene, "eevee"):
        bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.resolution_x = 1024
    bpy.context.scene.render.resolution_y = 1024
    bpy.context.scene.world.color = (0.42, 0.40, 0.36)
    bpy.context.scene.view_settings.view_transform = "Standard"
    bpy.context.scene.view_settings.look = "Medium High Contrast"
    bpy.context.scene.view_settings.exposure = -0.35

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
