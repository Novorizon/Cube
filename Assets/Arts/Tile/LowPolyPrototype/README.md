# Low-Poly Tile Prototype

Prototype asset for Cube TD map tiles.

Files:
- Tile_Grass_Hill_Prototype.obj
- Tile_Grass_Hill_Prototype.mtl

Style notes:
- Non-PBR diffuse materials for the first pass.
- Metallic/specular is intentionally disabled in the MTL (`Ks 0 0 0`, `illum 1`) to avoid oily/plastic highlights.
- Approximate triangle budget: under 400 triangles for the whole sample tile.
- Intended Unity material direction: Standard/Lit with Metallic 0 and Smoothness 0.15-0.25, or a simple toon/diffuse shader.

Next iteration ideas:
- Split this into reusable base, top grass, and prop rock prefabs.
- Add snow and water top layers that share the same base footprint.
- Add a small palette texture if Unity import does not preserve MTL colors well enough.