Voxel Cube Shaders for Unity URP

Files:
- Grass.shader
- Hill.shader
- Water.shader
- Snow.shader

Usage:
1. Put these files under Assets/Shaders/Voxel/
2. In Unity, create 4 materials:
   - Grass.mat
   - Hill.mat
   - Water.mat
   - Snow.mat
3. Assign shaders:
   - Custom/Voxel/Grass
   - Custom/Voxel/Hill
   - Custom/Voxel/Water
   - Custom/Voxel/Snow
4. Apply materials to 1x1x1 Cube terrain blocks.

Notes:
- These shaders do not require textures.
- They are written for URP.
- For Water, consider setting Cube scale Y to around 0.85 for a nicer water tile look.
