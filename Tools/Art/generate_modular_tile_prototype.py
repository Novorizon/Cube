import math
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "Assets" / "Arts" / "Tile" / "ModularPrototype"
OUT_DIR.mkdir(parents=True, exist_ok=True)
PALETTE_TEXTURE = OUT_DIR / "Textures" / "CubeTD_TilePalette.png"
GRASS_TEXTURE = OUT_DIR / "Textures" / "T_GrassTop_Albedo.png"
GRASS_THEME_CLEAN_TEXTURE = OUT_DIR / "Textures" / "T_GrassTheme_Clean_Albedo.png"
GRASS_THEME_DETAIL_TEXTURE = OUT_DIR / "Textures" / "T_GrassTheme_Detail_Albedo.png"
DIRT_TEXTURE = OUT_DIR / "Textures" / "T_DirtSide_Albedo.png"
STONE_TEXTURE = OUT_DIR / "Textures" / "T_StoneBase_Albedo.png"
DETAIL_TEXTURE = OUT_DIR / "Textures" / "T_TileDetail_Grain.png"
PALETTE_SWATCH_COUNT = 16
TILE_SIZE = 2.0


def clamp01(value):
    return max(0.0, min(1.0, value))


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(t):
    return t * t * (3.0 - 2.0 * t)


def hash_noise(x, y, seed):
    value = math.sin(x * 127.1 + y * 311.7 + seed * 74.7) * 43758.5453123
    return value - math.floor(value)


def value_noise(x, y, seed):
    x0 = math.floor(x)
    y0 = math.floor(y)
    tx = smooth(x - x0)
    ty = smooth(y - y0)
    a = hash_noise(x0, y0, seed)
    b = hash_noise(x0 + 1, y0, seed)
    c = hash_noise(x0, y0 + 1, seed)
    d = hash_noise(x0 + 1, y0 + 1, seed)
    return lerp(lerp(a, b, tx), lerp(c, d, tx), ty)


def fractal_noise(x, y, seed, octaves=4):
    total = 0.0
    amplitude = 0.55
    frequency = 1.0
    normalizer = 0.0

    for i in range(octaves):
        total += value_noise(x * frequency, y * frequency, seed + i * 37.0) * amplitude
        normalizer += amplitude
        amplitude *= 0.5
        frequency *= 2.0

    return total / normalizer


def save_texture(path, pixel_func, size=256):
    path.parent.mkdir(parents=True, exist_ok=True)
    image = bpy.data.images.new(path.stem, width=size, height=size, alpha=True)
    pixels = [0.0] * (size * size * 4)

    for y in range(size):
        for x in range(size):
            r, g, b, a = pixel_func(x, y, size)
            index = (y * size + x) * 4
            pixels[index + 0] = clamp01(r)
            pixels[index + 1] = clamp01(g)
            pixels[index + 2] = clamp01(b)
            pixels[index + 3] = clamp01(a)

    image.pixels = pixels
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()


def generate_textures():
    def grass_base(u, v):
        soft = fractal_noise(u * 5.5, v * 5.5, 17.0, 4)
        patch = fractal_noise(u * 13.0, v * 13.0, 23.0, 3)
        fine = fractal_noise(u * 31.0, v * 31.0, 29.0, 2)
        variation = (soft - 0.5) * 0.17 + (patch - 0.5) * 0.07 + (fine - 0.5) * 0.025

        r = 0.45 + variation
        g = 0.67 + variation * 0.88
        b = 0.10 + variation * 0.25
        return r, g, b

    def grass_pixel(x, y, size):
        u = x / size
        v = y / size
        fine = fractal_noise(u * 24.0, v * 24.0, 31.0, 3)
        mid = fractal_noise(u * 9.0, v * 9.0, 37.0, 3)
        grain = hash_noise(x, y, 43.0)
        variation = (fine - 0.5) * 0.13 + (mid - 0.5) * 0.08

        r = 0.38 + variation
        g = 0.58 + variation * 0.9
        b = 0.08 + variation * 0.25

        if grain > 0.986:
            r, g, b = 0.50, 0.47, 0.30
        elif grain > 0.965:
            r += 0.035
            g += 0.03

        return r, g, b, 1.0

    def grass_theme_clean_pixel(x, y, size):
        u = x / size
        v = y / size
        r, g, b = grass_base(u, v)
        edge = min(u, v, 1.0 - u, 1.0 - v)
        edge_dark = smooth(clamp01(edge / 0.14))
        shade = lerp(-0.055, 0.015, edge_dark)
        return r + shade, g + shade * 0.85, b + shade * 0.3, 1.0

    def grass_theme_detail_pixel(x, y, size):
        u = x / size
        v = y / size
        r, g, b = grass_base(u, v)
        grain = hash_noise(x, y, 71.0)
        moss = fractal_noise(u * 42.0, v * 42.0, 79.0, 2)

        if grain > 0.991:
            r, g, b = 0.52, 0.49, 0.32
        elif grain > 0.978:
            r, g, b = 0.36, 0.50, 0.09
        elif moss > 0.77:
            r += 0.035
            g += 0.045
            b += 0.008

        edge = min(u, v, 1.0 - u, 1.0 - v)
        edge_dark = smooth(clamp01(edge / 0.13))
        shade = lerp(-0.06, 0.012, edge_dark)
        return r + shade, g + shade * 0.85, b + shade * 0.3, 1.0

    def dirt_pixel(x, y, size):
        u = x / size
        v = y / size
        base = fractal_noise(u * 10.0, v * 10.0, 101.0, 4)
        crack = fractal_noise(u * 30.0, v * 14.0, 109.0, 3)
        r = lerp(0.40, 0.62, base)
        g = lerp(0.21, 0.34, base)
        b = lerp(0.09, 0.15, base)

        if crack < 0.27:
            r -= 0.12
            g -= 0.08
            b -= 0.04

        return r, g, b, 1.0

    def stone_pixel(x, y, size):
        u = x / size
        v = y / size
        base = fractal_noise(u * 11.0, v * 11.0, 211.0, 4)
        cell = value_noise(math.floor(u * 5.0), math.floor(v * 5.0), 223.0)
        crack = fractal_noise(u * 34.0, v * 34.0, 229.0, 3)
        gray = lerp(0.22, 0.38, base) + lerp(-0.03, 0.04, cell)

        if crack < 0.23:
            gray -= 0.10

        return gray, gray * 0.98, gray * 0.90, 1.0

    def detail_pixel(x, y, size):
        u = x / size
        v = y / size
        value = fractal_noise(u * 18.0, v * 18.0, 307.0, 4)
        gray = lerp(0.38, 0.70, value)
        return gray, gray, gray, 1.0

    save_texture(GRASS_TEXTURE, grass_pixel)
    save_texture(GRASS_THEME_CLEAN_TEXTURE, grass_theme_clean_pixel)
    save_texture(GRASS_THEME_DETAIL_TEXTURE, grass_theme_detail_pixel)
    save_texture(DIRT_TEXTURE, dirt_pixel)
    save_texture(STONE_TEXTURE, stone_pixel)
    save_texture(DETAIL_TEXTURE, detail_pixel)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_mat(name, color, roughness=0.85):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = roughness
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = color[3]
    mat.diffuse_color = color
    return mat


def make_palette_mat():
    mat = bpy.data.materials.new("CubeTD_TilePalette_Material")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new(type="ShaderNodeTexImage")
    tex.name = "CubeTD_TilePalette"
    tex.extension = "EXTEND"

    if PALETTE_TEXTURE.exists():
        tex.image = bpy.data.images.load(str(PALETTE_TEXTURE), check_existing=True)

    if bsdf:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.9

    return mat


def make_texture_mat(name, texture_path, roughness=0.9):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new(type="ShaderNodeTexImage")
    tex.name = name + "_Texture"
    tex.extension = "REPEAT"

    if texture_path.exists():
        tex.image = bpy.data.images.load(str(texture_path), check_existing=True)

    if bsdf:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = roughness

    return mat


def assign_mat(obj, mat):
    obj.data.materials.append(mat)


def set_origin_bottom_center(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    obj.select_set(False)


def add_beveled_cube(name, size, loc, mat, bevel=0.04, shade_flat=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_mat(obj, mat)

    if bevel > 0:
        mod = obj.modifiers.new("SmallBevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
        obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")

    if shade_flat:
        for poly in obj.data.polygons:
            poly.use_smooth = False

    return obj


def create_rock_mesh(name, center, radius, height, sides, mat, seed_angle=0.0):
    verts = []
    faces = []
    bottom = []
    top = []

    for i in range(sides):
        a = seed_angle + i * math.tau / sides
        noise = 0.82 + 0.22 * math.sin(i * 1.91 + seed_angle * 3.0)
        r = radius * noise
        x = center[0] + math.cos(a) * r
        y = center[1] + math.sin(a) * r
        bottom.append(len(verts))
        verts.append((x, y, center[2]))

    for i in range(sides):
        a = seed_angle + i * math.tau / sides + 0.08
        noise = 0.55 + 0.15 * math.cos(i * 2.17 + seed_angle)
        r = radius * noise
        x = center[0] + math.cos(a) * r
        y = center[1] + math.sin(a) * r
        z = center[2] + height * (0.88 + 0.16 * math.sin(i + seed_angle))
        top.append(len(verts))
        verts.append((x, y, z))

    faces.append(tuple(reversed(bottom)))
    faces.append(tuple(top))
    for i in range(sides):
        faces.append((bottom[i], bottom[(i + 1) % sides], top[(i + 1) % sides], top[i]))

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign_mat(obj, mat)
    for poly in mesh.polygons:
        poly.use_smooth = False
    return obj


def rounded_square_points(size, radius, segments):
    half = size * 0.5
    centers = [
        (half - radius, half - radius, 0.0, math.pi * 0.5),
        (-half + radius, half - radius, math.pi * 0.5, math.pi),
        (-half + radius, -half + radius, math.pi, math.pi * 1.5),
        (half - radius, -half + radius, math.pi * 1.5, math.tau),
    ]

    points = []
    for cx, cy, start, end in centers:
        for i in range(segments + 1):
            angle = lerp(start, end, i / segments)
            x = cx + math.cos(angle) * radius
            y = cy + math.sin(angle) * radius
            if not points or abs(points[-1][0] - x) > 0.0001 or abs(points[-1][1] - y) > 0.0001:
                points.append((x, y))

    if len(points) > 1 and abs(points[0][0] - points[-1][0]) < 0.0001 and abs(points[0][1] - points[-1][1]) < 0.0001:
        points.pop()

    return points


def create_grass_theme_mesh(name, mat, size=TILE_SIZE, height=0.32, radius=0.16):
    top_points = rounded_square_points(size, radius, 5)
    verts = []
    top_indices = []
    lower_indices = []

    for x, y in top_points:
        verts.append((x, y, height))
        top_indices.append(len(verts) - 1)

    for i, (x, y) in enumerate(top_points):
        angle = i * 1.73
        inset = 0.018 + 0.012 * (0.5 + 0.5 * math.sin(angle + 0.4))
        drop = 0.018 + 0.045 * (0.5 + 0.5 * math.sin(angle * 1.37 + 1.1))
        length = math.sqrt(x * x + y * y) or 1.0
        ix = x - x / length * inset
        iy = y - y / length * inset
        verts.append((ix, iy, drop))
        lower_indices.append(len(verts) - 1)

    faces = [tuple(top_indices), tuple(reversed(lower_indices))]
    count = len(top_points)
    for i in range(count):
        next_i = (i + 1) % count
        faces.append((top_indices[next_i], top_indices[i], lower_indices[i], lower_indices[next_i]))

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign_mat(obj, mat)

    bevel = obj.modifiers.new("SoftGrassEdge", "BEVEL")
    bevel.width = 0.045
    bevel.segments = 4
    bevel.affect = "EDGES"
    obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")

    return obj


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def export_selected(objects, name):
    select_only(objects)

    glb_path = OUT_DIR / f"{name}.glb"
    fbx_path = OUT_DIR / f"{name}.fbx"

    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
    )

    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"MESH"},
        path_mode="AUTO",
    )


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for mod in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except RuntimeError:
            pass
    obj.select_set(False)


def smart_uv_project(obj):
    if obj.type != "MESH":
        return

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def textured_material_for_object(obj, mats):
    name = obj.name
    if "Grass" in name:
        return mats["grass"]
    if "Dirt" in name:
        return mats["dirt"]
    if "Stone" in name:
        return mats["stone"]
    return mats["stone"]


def convert_to_textured(objects, mats):
    for obj in objects:
        apply_modifiers(obj)
        smart_uv_project(obj)
        obj.data.materials.clear()
        obj.data.materials.append(textured_material_for_object(obj, mats))


def swatch_uv(index):
    u = (index + 0.5) / PALETTE_SWATCH_COUNT
    return (u, 0.5)


def palette_index_for_object(obj):
    name = obj.name
    if "Grass_Lip" in name:
        return 1
    if "Grass" in name:
        return 0
    if "Dirt" in name:
        return 4
    if "StoneLayer" in name:
        return 6
    if "Stone" in name:
        return 7
    return 15


def assign_palette_uv(obj, swatch_index):
    if obj.type != "MESH":
        return

    mesh = obj.data
    uv_layer = mesh.uv_layers.active or mesh.uv_layers.new(name="PaletteUV")
    uv = swatch_uv(swatch_index)

    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            uv_layer.data[loop_index].uv = uv


def convert_to_palette_uv(objects, palette_mat):
    for obj in objects:
        apply_modifiers(obj)
        obj.data.materials.clear()
        obj.data.materials.append(palette_mat)
        assign_palette_uv(obj, palette_index_for_object(obj))


def main():
    clear_scene()
    generate_textures()

    stone_dark = make_mat("CubeTD_Stone_Dark", (0.18, 0.18, 0.16, 1.0))
    dirt = make_mat("CubeTD_Dirt_Warm", (0.52, 0.27, 0.11, 1.0))
    grass = make_mat("CubeTD_Grass_Main", (0.46, 0.70, 0.10, 1.0))
    grass_dark = make_mat("CubeTD_Grass_Edge", (0.31, 0.53, 0.08, 1.0))
    base_objects = []
    base_objects.append(add_beveled_cube("TileBase_StoneLayer", (TILE_SIZE, TILE_SIZE, 0.32), (0, 0, 0.16), stone_dark, bevel=0.055))
    base_objects.append(add_beveled_cube("TileBase_DirtLayer", (TILE_SIZE, TILE_SIZE, 0.38), (0, 0, 0.51), dirt, bevel=0.05))

    top_objects = []
    top_objects.append(add_beveled_cube("TileTop_Grass_Cap", (TILE_SIZE, TILE_SIZE, 0.18), (0, 0, 0.79), grass, bevel=0.08))

    # Irregular underside lips, sparse and flattened so the edge reads organic instead of gridded.
    lip_specs = [
        (-0.72, -0.985, 0.70, 0.32, 0.03), (-0.20, -0.985, 0.69, 0.42, 0.03), (0.46, -0.985, 0.705, 0.36, 0.03),
        (-0.52, 0.985, 0.70, 0.38, 0.03), (0.18, 0.985, 0.695, 0.46, 0.03), (0.76, 0.985, 0.705, 0.28, 0.03),
    ]
    for i, (x, y, z, sx, sy) in enumerate(lip_specs):
        top_objects.append(add_beveled_cube(f"TileTop_Grass_LipY_{i}", (sx, sy, 0.11), (x, y, z), grass_dark, bevel=0.025))

    lip_specs_x = [
        (-0.985, -0.66, 0.70, 0.03, 0.30), (-0.985, 0.02, 0.69, 0.03, 0.42), (-0.985, 0.68, 0.705, 0.03, 0.34),
        (0.985, -0.46, 0.70, 0.03, 0.38), (0.985, 0.22, 0.695, 0.03, 0.44), (0.985, 0.76, 0.705, 0.03, 0.26),
    ]
    for i, (x, y, z, sx, sy) in enumerate(lip_specs_x):
        top_objects.append(add_beveled_cube(f"TileTop_Grass_LipX_{i}", (sx, sy, 0.11), (x, y, z), grass_dark, bevel=0.025))

    # A tiny marker object at ground origin helps import inspection; excluded from exports.
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
    bpy.context.object.name = "UnityOrigin_DoNotExport"

    export_selected(base_objects, "TileBase_Prototype")
    export_selected(top_objects, "TileTop_Grass_Prototype")
    export_selected(base_objects + top_objects, "TilePreview_Base_Grass_Prototype")

    textured_mats = {
        "grass": make_texture_mat("CubeTD_Tex_GrassTop", GRASS_TEXTURE),
        "dirt": make_texture_mat("CubeTD_Tex_DirtSide", DIRT_TEXTURE),
        "stone": make_texture_mat("CubeTD_Tex_StoneBase", STONE_TEXTURE),
    }
    convert_to_textured(base_objects + top_objects, textured_mats)

    export_selected(base_objects, "TileBase_Textured_Prototype")
    export_selected(top_objects, "TileTop_Grass_Textured_Prototype")
    export_selected(base_objects + top_objects, "TilePreview_Base_Grass_Textured_Prototype")

    palette_mat = make_palette_mat()
    convert_to_palette_uv(base_objects + top_objects, palette_mat)

    export_selected(base_objects, "TileBase_PaletteUV_Prototype")
    export_selected(top_objects, "TileTop_Grass_PaletteUV_Prototype")
    export_selected(base_objects + top_objects, "TilePreview_Base_Grass_PaletteUV_Prototype")

    grass_clean_mat = make_texture_mat("CubeTD_Tex_GrassThemeClean", GRASS_THEME_CLEAN_TEXTURE)
    grass_detail_mat = make_texture_mat("CubeTD_Tex_GrassThemeDetail", GRASS_THEME_DETAIL_TEXTURE)
    grass_theme_clean = create_grass_theme_mesh("TileTheme_GrassSoft_Clean", grass_clean_mat)
    grass_theme_detail = create_grass_theme_mesh("TileTheme_GrassSoft_Detail", grass_detail_mat)

    for obj in [grass_theme_clean, grass_theme_detail]:
        apply_modifiers(obj)
        smart_uv_project(obj)

    export_selected([grass_theme_clean], "TileTheme_GrassSoft_Clean")
    export_selected([grass_theme_detail], "TileTheme_GrassSoft_Detail")

    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_DIR / "ModularTilePrototype.blend"))


if __name__ == "__main__":
    main()
