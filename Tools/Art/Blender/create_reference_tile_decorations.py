import math
import os
import random

import bpy


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUTPUT_DIR = os.path.join(ROOT_DIR, "Assets", "Arts", "Map", "Tiles", "Generated", "Decorations")
BLEND_PATH = os.path.join(OUTPUT_DIR, "ReferenceTileDecorations.blend")


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


def create_grass_clump(material):
    root = bpy.data.objects.new("GrassClump_A", None)
    bpy.context.collection.objects.link(root)
    for i in range(7):
        angle = -0.78 + i * 0.26
        height = 0.055 + (i % 3) * 0.012
        width = 0.010 + (i % 2) * 0.004
        mesh = bpy.data.meshes.new("GrassBladeMesh")
        verts = [(-width, 0, 0), (width, 0, 0), (0, 0, height)]
        faces = [(0, 1, 2)]
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        mesh.materials.append(material)
        blade = bpy.data.objects.new("GrassBlade", mesh)
        bpy.context.collection.objects.link(blade)
        blade.rotation_euler = (0, 0, angle)
        blade.parent = root
    return root


def create_flower(petal_mat, center_mat, leaf_mat):
    root = bpy.data.objects.new("SmallFlower_A", None)
    bpy.context.collection.objects.link(root)
    for i in range(5):
        angle = i * math.tau / 5.0
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=10,
            ring_count=5,
            radius=0.012,
            location=(math.cos(angle) * 0.016, math.sin(angle) * 0.016, 0.012),
        )
        petal = bpy.context.object
        petal.name = "Petal"
        petal.scale.z = 0.10
        petal.scale.y = 0.65
        petal.rotation_euler.z = angle
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        petal.data.materials.append(petal_mat)
        petal.parent = root

    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=5, radius=0.006, location=(0, 0, 0.015))
    center = bpy.context.object
    center.name = "Center"
    center.scale.z = 0.20
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    center.data.materials.append(center_mat)
    center.parent = root

    for x in [-0.018, 0.018]:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.014, location=(x, -0.018, 0.004))
        leaf = bpy.context.object
        leaf.name = "Leaf"
        leaf.scale.z = 0.08
        leaf.scale.y = 0.45
        leaf.rotation_euler.z = -0.5 if x < 0 else 0.5
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        leaf.data.materials.append(leaf_mat)
        leaf.parent = root
    return root


def create_pebble(material):
    root = bpy.data.objects.new("Pebble_A", None)
    bpy.context.collection.objects.link(root)
    for i, (x, y, r) in enumerate([(-0.018, 0, 0.018), (0.012, 0.010, 0.012), (0.026, -0.012, 0.009)]):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=r, location=(x, y, r * 0.45))
        pebble = bpy.context.object
        pebble.name = "Pebble"
        pebble.scale.z = 0.45
        pebble.rotation_euler = (random.random(), random.random(), random.random())
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        pebble.data.materials.append(material)
        pebble.parent = root
    return root


def export_object(obj, filename):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for child in obj.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUTPUT_DIR, filename),
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
    )


def build():
    clear_scene()
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    random.seed(602)

    grass_mat = make_material("Deco_Grass", (0.22, 0.42, 0.06, 1))
    petal_mat = make_material("Deco_FlowerWhite", (0.96, 0.94, 0.80, 1))
    center_mat = make_material("Deco_FlowerYellow", (0.96, 0.72, 0.18, 1))
    pebble_mat = make_material("Deco_Pebble", (0.55, 0.50, 0.42, 1))

    grass = create_grass_clump(grass_mat)
    flower = create_flower(petal_mat, center_mat, grass_mat)
    pebble = create_pebble(pebble_mat)

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_object(grass, "GrassClump_A.fbx")
    export_object(flower, "SmallFlower_A.fbx")
    export_object(pebble, "Pebble_A.fbx")

    print("Saved blend:", BLEND_PATH)
    print("Saved decorations to:", OUTPUT_DIR)


if __name__ == "__main__":
    build()
