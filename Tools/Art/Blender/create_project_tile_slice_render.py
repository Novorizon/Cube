import math
import os
import random

import bpy
from mathutils import Vector


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUTPUT_DIR = os.path.join(ROOT_DIR, "Assets", "Arts", "Map", "Tiles", "Generated")
BLEND_PATH = os.path.join(OUTPUT_DIR, "ProjectTileSliceRender.blend")
FBX_PATH = os.path.join(OUTPUT_DIR, "ProjectTileSliceRender.fbx")
PREVIEW_PATH = os.path.join(OUTPUT_DIR, "ProjectTileSliceRender_preview.png")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_material(name, color, roughness=0.8):
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


def make_rounded_box(name, dimensions, location, material, bevel=0.035, segments=5):
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


def make_top_mesh(name, verts2d, z, material, thickness=0.014):
    area = 0.0
    for i in range(len(verts2d)):
        x1, y1 = verts2d[i]
        x2, y2 = verts2d[(i + 1) % len(verts2d)]
        area += x1 * y2 - x2 * y1
    if area < 0:
        verts2d = list(reversed(verts2d))

    mesh = bpy.data.meshes.new(name + "Mesh")
    verts = [(x, y, z) for x, y in verts2d]
    faces = [tuple(range(len(verts)))]
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)

    solidify = obj.modifiers.new("SoftThickness", "SOLIDIFY")
    solidify.thickness = thickness
    solidify.offset = -1

    bevel = obj.modifiers.new("TinyRoundedPatchEdge", "BEVEL")
    bevel.width = 0.008
    bevel.segments = 3

    normal = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    normal.keep_sharp = True
    return obj


def arc_corner(cx, cy, radius, start, end, steps):
    pts = []
    for i in range(steps + 1):
        t = start + (end - start) * i / steps
        pts.append((cx + math.cos(t) * radius, cy + math.sin(t) * radius))
    return pts


def wavy_line(start, end, steps, amp, phase):
    pts = []
    sx, sy = start
    ex, ey = end
    dx = ex - sx
    dy = ey - sy
    length = max(0.0001, math.hypot(dx, dy))
    nx = -dy / length
    ny = dx / length
    for i in range(steps + 1):
        t = i / steps
        wave = math.sin(t * math.tau * 1.5 + phase) * amp
        pts.append((sx + dx * t + nx * wave, sy + dy * t + ny * wave))
    return pts


def add_disc(name, x, y, z, radius, material, scale_y=0.75):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius, location=(x, y, z))
    obj = bpy.context.object
    obj.name = name
    obj.scale.y = scale_y
    obj.scale.z = 0.055
    obj.rotation_euler.z = random.uniform(0, math.tau)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def add_flower(root, x, y, z, petal_mat, center_mat):
    for i in range(5):
        angle = i * math.tau / 5
        add_disc("SmallFlowerPetal", x + math.cos(angle) * 0.012, y + math.sin(angle) * 0.012, z, 0.008, petal_mat, 0.65).parent = root
    add_disc("SmallFlowerCenter", x, y, z + 0.001, 0.005, center_mat, 1.0).parent = root


def build_render():
    random.seed(20260602)
    clear_scene()
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    mat_rock = make_material("Rock_Block", (0.18, 0.20, 0.20, 1))
    mat_soil = make_material("Soil_Block", (0.47, 0.27, 0.13, 1))
    mat_soil_light = make_material("Soil_Light_Edge", (0.66, 0.42, 0.20, 1))
    mat_body = make_material("GrassBody_Continuous", (0.33, 0.55, 0.10, 1))
    mat_grass = make_material("Patch_Grass", (0.45, 0.66, 0.13, 1))
    mat_grass_dark = make_material("Patch_Grass_Dark_Detail", (0.26, 0.48, 0.08, 1))
    mat_grass_light = make_material("Patch_Grass_Light_Detail", (0.56, 0.74, 0.20, 1))
    mat_snow = make_material("Patch_Snow", (0.88, 0.94, 0.98, 1))
    mat_snow_shadow = make_material("Patch_Snow_Shadow", (0.70, 0.82, 0.88, 1))
    mat_water = make_material("Patch_Water", (0.10, 0.55, 0.78, 1), 0.55)
    mat_water_deep = make_material("Patch_Water_Deep", (0.06, 0.34, 0.62, 1), 0.55)
    mat_hill = make_material("Patch_Hill_Sand", (0.68, 0.48, 0.23, 1))
    mat_flower = make_material("Tiny_Flower", (0.97, 0.90, 0.48, 1))
    mat_flower_center = make_material("Tiny_Flower_Center", (0.96, 0.66, 0.18, 1))

    root = bpy.data.objects.new("ProjectTileSliceRender", None)
    bpy.context.collection.objects.link(root)
    base = bpy.data.objects.new("Base", None)
    topic = bpy.data.objects.new("Topic", None)
    patches = bpy.data.objects.new("TopPatches_ActualNinePieces", None)
    for obj in [base, topic, patches]:
        bpy.context.collection.objects.link(obj)
    base.parent = root
    topic.parent = root
    patches.parent = topic

    make_rounded_box("Rock", (1.0, 1.0, 0.28), (0, 0, 0.14), mat_rock, 0.032, 4).parent = base
    make_rounded_box("Soil", (1.0, 1.0, 0.40), (0, 0, 0.48), mat_soil, 0.038, 5).parent = base
    make_rounded_box("SoilTopHighlight", (0.98, 0.98, 0.035), (0, 0, 0.695), mat_soil_light, 0.025, 3).parent = base
    make_rounded_box("TopicBody_OneContinuousRoundedModel", (1.0, 1.0, 0.31), (0, 0, 0.86), mat_body, 0.065, 7).parent = topic

    z = 1.022
    outer = 0.455
    inner = 0.245
    corner_radius = 0.045

    # Nine practical replaceable top meshes. They overlap very slightly to avoid visible cracks.
    center = [
        *wavy_line((-inner, -inner), (inner, -inner), 8, 0.010, 0.6),
        *wavy_line((inner, -inner), (inner, inner), 8, 0.010, 1.7)[1:],
        *reversed(wavy_line((-inner, inner), (inner, inner), 8, 0.010, 2.3)[:-1]),
        *reversed(wavy_line((-inner, -inner), (-inner, inner), 8, 0.010, 3.2)[1:-1]),
    ]
    make_top_mesh("Top_Center_Grass", center, z + 0.002, mat_grass).parent = patches

    north = [(-inner - 0.01, inner - 0.01), (inner + 0.01, inner - 0.01), (outer - corner_radius, outer)]
    north += arc_corner(outer - corner_radius, outer - corner_radius, corner_radius, math.pi / 2, 0, 5)
    north += [(inner + 0.01, outer), (-inner - 0.01, outer)]
    north += arc_corner(-outer + corner_radius, outer - corner_radius, corner_radius, math.pi, math.pi / 2, 5)
    make_top_mesh("Top_N_SnowEdge", north, z + 0.004, mat_snow).parent = patches

    east = [(inner - 0.01, -inner - 0.01), (outer, -inner - 0.01), (outer, inner + 0.01), (inner - 0.01, inner + 0.01)]
    make_top_mesh("Top_E_WaterEdge", east, z + 0.005, mat_water).parent = patches

    south = [(-inner - 0.01, -outer), (inner + 0.01, -outer), (inner + 0.01, -inner + 0.01), (-inner - 0.01, -inner + 0.01)]
    make_top_mesh("Top_S_HillEdge", south, z + 0.003, mat_hill).parent = patches

    west = [(-outer, -inner - 0.01), (-inner + 0.01, -inner - 0.01), (-inner + 0.01, inner + 0.01), (-outer, inner + 0.01)]
    make_top_mesh("Top_W_GrassEdge", west, z + 0.001, mat_grass).parent = patches

    make_top_mesh("Top_NW_SnowGrassCorner", [(-outer, inner - 0.01), (-inner + 0.015, inner - 0.01), (-inner + 0.015, outer), (-outer + 0.04, outer), (-outer, outer - 0.04)], z + 0.006, mat_snow).parent = patches
    make_top_mesh("Top_NE_SnowWaterCorner", [(inner - 0.015, inner - 0.01), (outer, inner - 0.01), (outer, outer - 0.04), (outer - 0.04, outer), (inner - 0.015, outer)], z + 0.006, mat_snow).parent = patches
    make_top_mesh("Top_SE_WaterHillCorner", [(inner - 0.01, -outer), (outer - 0.04, -outer), (outer, -outer + 0.04), (outer, -inner + 0.015), (inner - 0.01, -inner + 0.015)], z + 0.006, mat_water).parent = patches
    make_top_mesh("Top_SW_HillGrassCorner", [(-outer, -outer + 0.04), (-outer + 0.04, -outer), (-inner + 0.015, -outer), (-inner + 0.015, -inner + 0.015), (-outer, -inner + 0.015)], z + 0.004, mat_hill).parent = patches

    # Commercial-style hand-authored detail that would be ordinary mesh/texture detail in the project.
    for x, y, r, mat in [
        (-0.12, 0.05, 0.055, mat_grass_light),
        (0.06, -0.08, 0.045, mat_grass_dark),
        (0.15, 0.13, 0.035, mat_grass_light),
        (-0.04, 0.17, 0.030, mat_grass_dark),
    ]:
        add_disc("GrassPaintBlob", x, y, z + 0.025, r, mat).parent = patches

    for x, y in [(-0.18, -0.02), (0.12, 0.02), (0.02, 0.16)]:
        add_flower(patches, x, y, z + 0.035, mat_flower, mat_flower_center)

    for x, y, r in [(0.33, 0.10, 0.055), (0.34, -0.10, 0.035), (0.39, -0.02, 0.025)]:
        add_disc("WaterSoftSpot", x, y, z + 0.026, r, mat_water_deep, 0.85).parent = patches

    for x, y, r in [(-0.08, 0.34, 0.040), (0.10, 0.34, 0.028), (0.24, 0.31, 0.020)]:
        add_disc("SnowSoftShadow", x, y, z + 0.026, r, mat_snow_shadow, 0.75).parent = patches

    for x, y, r in [(-0.10, -0.34, 0.035), (0.08, -0.35, 0.025)]:
        add_disc("HillPebble", x, y, z + 0.026, r, mat_soil_light, 0.75).parent = patches

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (1.45, -1.65, 1.38)
    point_camera_at(camera, (0, 0, 0.62))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.58
    bpy.context.scene.camera = camera

    key_data = bpy.data.lights.new("KeyLight", "AREA")
    key = bpy.data.objects.new("KeyLight", key_data)
    bpy.context.collection.objects.link(key)
    key.location = (-2.2, -2.0, 3.5)
    key.data.energy = 550
    key.data.size = 4.5

    fill_data = bpy.data.lights.new("FillLight", "POINT")
    fill = bpy.data.objects.new("FillLight", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (2.1, 2.0, 1.6)
    fill.data.energy = 85

    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    if hasattr(bpy.context.scene, "eevee"):
        bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.resolution_x = 1024
    bpy.context.scene.render.resolution_y = 1024
    bpy.context.scene.world.color = (0.41, 0.39, 0.35)
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
    build_render()
