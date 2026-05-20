using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TowerDefensePngAlphaRepairTool
    {
        private const string RootFolder = "Assets/Arts/UI/TowerDefense";
        private const string BackupFolder = RootFolder + "/_AlphaRepairBackup";

        [MenuItem("Tools/TowerDefense UI/PNG Alpha/Check Alpha")]
        public static void CheckAlpha()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RootFolder });
            int noAlphaCount = 0;
            int transparentCornerCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.StartsWith(BackupFolder))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool hasSourceAlpha = importer.DoesSourceTextureHaveAlpha();
                bool cornersTransparent = HasTransparentCorners(path);

                if (!hasSourceAlpha)
                {
                    noAlphaCount++;
                    Debug.LogWarning("No source alpha: " + path);
                    continue;
                }

                if (cornersTransparent)
                {
                    transparentCornerCount++;
                }
                else
                {
                    Debug.LogWarning("Has alpha, but corners are not transparent: " + path);
                }
            }

            Debug.Log("TowerDefense PNG alpha check complete. NoSourceAlpha=" + noAlphaCount + ", TransparentCorners=" + transparentCornerCount + ", Total=" + guids.Length);
        }

        [MenuItem("Tools/TowerDefense UI/PNG Alpha/Repair Selected From Edge Background")]
        public static void RepairSelected()
        {
            Object[] objects = Selection.objects;
            int count = 0;

            for (int i = 0; i < objects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(objects[i]);
                if (!path.StartsWith(RootFolder) || !path.EndsWith(".png"))
                {
                    continue;
                }

                if (RepairOne(path, true))
                {
                    count++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("Selected PNG alpha repair complete. Repaired count: " + count);
        }

        [MenuItem("Tools/TowerDefense UI/PNG Alpha/Repair All From Edge Background")]
        public static void RepairAll()
        {
            bool ok = EditorUtility.DisplayDialog("Repair PNG Alpha", "This will overwrite PNG files under Assets/Arts/UI/TowerDefense and create backups. Test selected files first.", "Repair", "Cancel");
            if (!ok)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RootFolder });
            int count = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.StartsWith(BackupFolder) || !path.EndsWith(".png"))
                {
                    continue;
                }

                if (RepairOne(path, true))
                {
                    count++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("All PNG alpha repair complete. Repaired count: " + count);
        }

        private static bool RepairOne(string assetPath, bool backup)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            byte[] sourceBytes = File.ReadAllBytes(fullPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(sourceBytes))
            {
                Object.DestroyImmediate(texture);
                return false;
            }

            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;
            bool[] backgroundMask = BuildEdgeConnectedBackgroundMask(pixels, width, height);

            int removed = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (backgroundMask[i])
                {
                    pixels[i].a = 0;
                    removed++;
                }
                else
                {
                    pixels[i].a = 255;
                }
            }

            if (backup)
            {
                BackupOriginal(assetPath, sourceBytes);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            ConfigureImporter(assetPath);
            Debug.Log("Alpha repaired: " + assetPath + ", removed pixels=" + removed);
            return removed > 0;
        }

        private static bool[] BuildEdgeConnectedBackgroundMask(Color32[] pixels, int width, int height)
        {
            bool[] visited = new bool[pixels.Length];
            Queue<int> queue = new Queue<int>();

            for (int x = 0; x < width; x++)
            {
                TryStart(x, 0, pixels, width, height, visited, queue);
                TryStart(x, height - 1, pixels, width, height, visited, queue);
            }

            for (int y = 0; y < height; y++)
            {
                TryStart(0, y, pixels, width, height, visited, queue);
                TryStart(width - 1, y, pixels, width, height, visited, queue);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                TryVisit(x - 1, y, pixels, width, height, visited, queue);
                TryVisit(x + 1, y, pixels, width, height, visited, queue);
                TryVisit(x, y - 1, pixels, width, height, visited, queue);
                TryVisit(x, y + 1, pixels, width, height, visited, queue);
            }

            return visited;
        }

        private static void TryStart(int x, int y, Color32[] pixels, int width, int height, bool[] visited, Queue<int> queue)
        {
            int index = y * width + x;
            if (visited[index] || !LooksLikeBackground(pixels[index]))
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        private static void TryVisit(int x, int y, Color32[] pixels, int width, int height, bool[] visited, Queue<int> queue)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (visited[index] || !LooksLikeBackground(pixels[index]))
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        private static bool LooksLikeBackground(Color32 color)
        {
            if (color.a < 250)
            {
                return true;
            }

            int max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            int min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            bool neutral = max - min <= 22;
            bool white = color.r >= 225 && color.g >= 225 && color.b >= 225;
            bool checker = neutral && color.r >= 185 && color.g >= 185 && color.b >= 185;
            return white || checker;
        }

        private static bool HasTransparentCorners(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                Object.DestroyImmediate(texture);
                return false;
            }

            Color32 a = texture.GetPixel(0, 0);
            Color32 b = texture.GetPixel(texture.width - 1, 0);
            Color32 c = texture.GetPixel(0, texture.height - 1);
            Color32 d = texture.GetPixel(texture.width - 1, texture.height - 1);
            bool result = a.a < 16 && b.a < 16 && c.a < 16 && d.a < 16;
            Object.DestroyImmediate(texture);
            return result;
        }

        private static void BackupOriginal(string assetPath, byte[] bytes)
        {
            string relative = assetPath.Substring(RootFolder.Length).TrimStart('/');
            string backupPath = BackupFolder + "/" + relative;
            string backupDirectory = Path.GetDirectoryName(backupPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            string fullBackupPath = Path.GetFullPath(backupPath);
            if (!File.Exists(fullBackupPath))
            {
                File.WriteAllBytes(fullBackupPath, bytes);
            }
        }

        private static void ConfigureImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
