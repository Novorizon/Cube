import os
import sys

import bpy


def parse_args():
    if "--" not in sys.argv:
        raise SystemExit("Usage: blender --background --python create_road_top_uv_copy.py -- <source.fbx> <output.fbx>")

    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 2:
        raise SystemExit("Expected source and output FBX paths.")

    return os.path.abspath(args[0]), os.path.abspath(args[1])


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def add_rect_patch_uv(mesh):
    if len(mesh.uv_layers) == 0:
        mesh.uv_layers.new(name="UVMap")

    base_uv = mesh.uv_layers[0]
    patch_uv = mesh.uv_layers.get("RoadPatchUV")
    if patch_uv is None:
        patch_uv = mesh.uv_layers.new(name="RoadPatchUV")

    vertices = mesh.vertices
    min_x = min(vertex.co.x for vertex in vertices)
    max_x = max(vertex.co.x for vertex in vertices)
    min_y = min(vertex.co.y for vertex in vertices)
    max_y = max(vertex.co.y for vertex in vertices)
    inv_x = 1.0 / max(0.0001, max_x - min_x)
    inv_y = 1.0 / max(0.0001, max_y - min_y)

    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = vertices[mesh.loops[loop_index].vertex_index].co
            rect_uv = ((vertex.x - min_x) * inv_x, (vertex.y - min_y) * inv_y)
            base_uv.data[loop_index].uv = rect_uv
            patch_uv.data[loop_index].uv = rect_uv


def main():
    source_path, output_path = parse_args()
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=source_path)

    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError(f"No mesh objects found in {source_path}")

    for obj in mesh_objects:
        add_rect_patch_uv(obj.data)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        add_leaf_bones=False,
        path_mode="AUTO",
    )

    print(f"Created road top UV copy: {output_path}")


if __name__ == "__main__":
    main()
