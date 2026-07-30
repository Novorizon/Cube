using UnityEngine;

namespace Game
{
    public static class MiniMapTextureBuilder
    {
        private static readonly Color EmptyColor = new Color(0.035f, 0.055f, 0.075f, 1f);
        private static readonly Color DeepShadowColor = new Color(0.025f, 0.035f, 0.035f, 1f);
        private static readonly Color ShoreColor = new Color(0.43f, 0.64f, 0.70f, 1f);
        private const int PreferredPixelsPerCell = 3;
        private const int MaxTextureSide = 1024;

        public static Texture2D Build(MapData map)
        {
            if (map == null || map.Width <= 0 || map.Depth <= 0)
            {
                return null;
            }

            int width = map.Width;
            int depth = map.Depth;
            int cellCount = width * depth;
            MapCellData[] topCells = new MapCellData[cellCount];
            int[] topHeights = new int[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                topHeights[i] = int.MinValue;
            }

            if (map.Cells != null)
            {
                for (int i = 0; i < map.Cells.Count; i++)
                {
                    MapCellData cell = map.Cells[i];
                    if (cell == null || cell.X < 0 || cell.X >= width || cell.Z < 0 || cell.Z >= depth)
                    {
                        continue;
                    }

                    int index = cell.Z * width + cell.X;
                    if (cell.Y < topHeights[index])
                    {
                        continue;
                    }

                    topHeights[index] = cell.Y;
                    topCells[index] = cell;
                }
            }

            GetHeightRange(topHeights, out int minHeight, out int maxHeight);

            int maxDimension = Mathf.Max(width, depth);
            int pixelsPerCell = Mathf.Clamp(MaxTextureSide / maxDimension, 1, PreferredPixelsPerCell);
            int textureWidth = width * pixelsPerCell;
            int textureDepth = depth * pixelsPerCell;
            Color[] pixels = new Color[textureWidth * textureDepth];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = EmptyColor;
            }

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cellIndex = z * width + x;
                    MapCellData cell = topCells[cellIndex];
                    if (cell == null)
                    {
                        continue;
                    }

                    Color baseColor = ShadeCell(
                        GetCellColor(cell),
                        cell,
                        x,
                        z,
                        width,
                        depth,
                        topHeights,
                        minHeight,
                        maxHeight);
                    FillCellBlock(
                        pixels,
                        textureWidth,
                        x,
                        z,
                        pixelsPerCell,
                        baseColor);
                }
            }

            PaintTerrainEdges(
                pixels,
                textureWidth,
                width,
                depth,
                pixelsPerCell,
                topCells,
                topHeights);

            Texture2D texture = new Texture2D(textureWidth, textureDepth, TextureFormat.RGBA32, false)
            {
                name = $"MiniMap_{map.Id}_{textureWidth}x{textureDepth}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void GetHeightRange(int[] topHeights, out int minHeight, out int maxHeight)
        {
            minHeight = int.MaxValue;
            maxHeight = int.MinValue;
            for (int i = 0; i < topHeights.Length; i++)
            {
                int height = topHeights[i];
                if (height == int.MinValue)
                {
                    continue;
                }

                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }

            if (minHeight == int.MaxValue)
            {
                minHeight = 0;
                maxHeight = 0;
            }
        }

        private static Color ShadeCell(
            Color color,
            MapCellData cell,
            int x,
            int z,
            int width,
            int depth,
            int[] heights,
            int minHeight,
            int maxHeight)
        {
            int index = z * width + x;
            int height = heights[index];
            float normalizedHeight = maxHeight > minHeight
                ? (height - minHeight) / (float)(maxHeight - minHeight)
                : 0.5f;

            int west = SampleHeight(heights, width, depth, x - 1, z, height);
            int east = SampleHeight(heights, width, depth, x + 1, z, height);
            int south = SampleHeight(heights, width, depth, x, z - 1, height);
            int north = SampleHeight(heights, width, depth, x, z + 1, height);
            float directionalLight = Mathf.Clamp((west - east + north - south) * 0.035f, -0.16f, 0.16f);
            float noise = HashNoise(x, z) * (cell.Type == MapTileType.Water ? 0.025f : 0.07f);
            float brightness = 0.89f + normalizedHeight * 0.18f + directionalLight + noise;

            color.r = Mathf.Clamp01(color.r * brightness);
            color.g = Mathf.Clamp01(color.g * brightness);
            color.b = Mathf.Clamp01(color.b * brightness);
            color.a = 1f;
            return color;
        }

        private static int SampleHeight(
            int[] heights,
            int width,
            int depth,
            int x,
            int z,
            int fallback)
        {
            if (x < 0 || x >= width || z < 0 || z >= depth)
            {
                return fallback;
            }

            int value = heights[z * width + x];
            return value == int.MinValue ? fallback : value;
        }

        private static float HashNoise(int x, int z)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + z * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                return (hash & 1023u) / 1023f - 0.5f;
            }
        }

        private static void FillCellBlock(
            Color[] pixels,
            int textureWidth,
            int cellX,
            int cellZ,
            int pixelsPerCell,
            Color color)
        {
            int startX = cellX * pixelsPerCell;
            int startZ = cellZ * pixelsPerCell;
            for (int localZ = 0; localZ < pixelsPerCell; localZ++)
            {
                int row = (startZ + localZ) * textureWidth;
                for (int localX = 0; localX < pixelsPerCell; localX++)
                {
                    pixels[row + startX + localX] = color;
                }
            }
        }

        private static void PaintTerrainEdges(
            Color[] pixels,
            int textureWidth,
            int width,
            int depth,
            int pixelsPerCell,
            MapCellData[] cells,
            int[] heights)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    MapCellData cell = cells[index];
                    if (cell == null)
                    {
                        continue;
                    }

                    PaintEdge(
                        pixels,
                        textureWidth,
                        width,
                        depth,
                        pixelsPerCell,
                        cells,
                        heights,
                        cell,
                        x,
                        z,
                        -1,
                        0);
                    PaintEdge(
                        pixels,
                        textureWidth,
                        width,
                        depth,
                        pixelsPerCell,
                        cells,
                        heights,
                        cell,
                        x,
                        z,
                        1,
                        0);
                    PaintEdge(
                        pixels,
                        textureWidth,
                        width,
                        depth,
                        pixelsPerCell,
                        cells,
                        heights,
                        cell,
                        x,
                        z,
                        0,
                        -1);
                    PaintEdge(
                        pixels,
                        textureWidth,
                        width,
                        depth,
                        pixelsPerCell,
                        cells,
                        heights,
                        cell,
                        x,
                        z,
                        0,
                        1);
                }
            }
        }

        private static void PaintEdge(
            Color[] pixels,
            int textureWidth,
            int width,
            int depth,
            int pixelsPerCell,
            MapCellData[] cells,
            int[] heights,
            MapCellData cell,
            int x,
            int z,
            int offsetX,
            int offsetZ)
        {
            int neighborX = x + offsetX;
            int neighborZ = z + offsetZ;
            MapCellData neighbor = null;
            int neighborHeight = int.MinValue;
            if (neighborX >= 0 && neighborX < width && neighborZ >= 0 && neighborZ < depth)
            {
                int neighborIndex = neighborZ * width + neighborX;
                neighbor = cells[neighborIndex];
                neighborHeight = heights[neighborIndex];
            }

            int currentHeight = heights[z * width + x];
            Color edgeColor;
            if (!TryGetEdgeColor(cell, neighbor, currentHeight, neighborHeight, out edgeColor))
            {
                return;
            }

            int startX = x * pixelsPerCell;
            int startZ = z * pixelsPerCell;
            if (offsetX != 0)
            {
                int pixelX = offsetX < 0 ? startX : startX + pixelsPerCell - 1;
                for (int localZ = 0; localZ < pixelsPerCell; localZ++)
                {
                    int pixelIndex = (startZ + localZ) * textureWidth + pixelX;
                    pixels[pixelIndex] = Color.Lerp(pixels[pixelIndex], edgeColor, 0.72f);
                }
            }
            else
            {
                int pixelZ = offsetZ < 0 ? startZ : startZ + pixelsPerCell - 1;
                int row = pixelZ * textureWidth;
                for (int localX = 0; localX < pixelsPerCell; localX++)
                {
                    int pixelIndex = row + startX + localX;
                    pixels[pixelIndex] = Color.Lerp(pixels[pixelIndex], edgeColor, 0.72f);
                }
            }
        }

        private static bool TryGetEdgeColor(
            MapCellData cell,
            MapCellData neighbor,
            int currentHeight,
            int neighborHeight,
            out Color color)
        {
            bool currentWater = cell.Type == MapTileType.Water;
            bool neighborWater = neighbor != null && neighbor.Type == MapTileType.Water;
            if (currentWater != neighborWater)
            {
                color = currentWater ? ShoreColor : DeepShadowColor;
                return true;
            }

            if (neighbor == null)
            {
                color = DeepShadowColor;
                return true;
            }

            int heightDelta = neighborHeight == int.MinValue ? 0 : Mathf.Abs(currentHeight - neighborHeight);
            if (heightDelta > 0)
            {
                float strength = Mathf.Clamp01(0.32f + heightDelta * 0.10f);
                color = Color.Lerp(GetCellColor(cell), DeepShadowColor, strength);
                return true;
            }

            bool currentRoad = cell.Type == MapTileType.Road;
            bool neighborRoad = neighbor.Type == MapTileType.Road;
            if (currentRoad != neighborRoad)
            {
                color = currentRoad
                    ? Color.Lerp(GetCellColor(cell), DeepShadowColor, 0.28f)
                    : Color.Lerp(GetCellColor(cell), DeepShadowColor, 0.12f);
                return true;
            }

            color = Color.clear;
            return false;
        }

        private static Color GetCellColor(MapCellData cell)
        {
            cell.EnsureLayers();
            switch (cell.OverlayType)
            {
                case MapTileOverlay.Bridge:
                    return new Color(0.62f, 0.44f, 0.24f, 1f);
                case MapTileOverlay.Stair:
                case MapTileOverlay.Ramp:
                    return new Color(0.52f, 0.48f, 0.38f, 1f);
            }

            switch (cell.Type)
            {
                case MapTileType.Grass:
                    return cell.GrassVisual != null
                        ? cell.GrassVisual.BaseGreen
                        : new Color(0.25f, 0.50f, 0.22f, 1f);
                case MapTileType.Hill:
                    return new Color(0.34f, 0.38f, 0.25f, 1f);
                case MapTileType.Snow:
                    return new Color(0.78f, 0.86f, 0.88f, 1f);
                case MapTileType.Water:
                    return new Color(0.12f, 0.35f, 0.58f, 1f);
                case MapTileType.Road:
                    return new Color(0.56f, 0.48f, 0.36f, 1f);
                case MapTileType.Bridge:
                    return new Color(0.60f, 0.42f, 0.23f, 1f);
                case MapTileType.Soil:
                    return new Color(0.45f, 0.29f, 0.16f, 1f);
                default:
                    return EmptyColor;
            }
        }
    }
}
