#if UNITY_EDITOR

using Luban;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class WorldConfigValidator
    {
        private const string BinPathPrefix = "Assets/Data/Bin/";

        [MenuItem("Data/校验/大地图配置")]
        public static void ValidateWorldConfigs()
        {
            int errorCount = 0;
            int warningCount = 0;

            Tables tables = LoadTables();
            IReadOnlyDictionary<int, WorldBuildingConfig> buildings = tables.TbWorldBuilding.DataMap;
            IReadOnlyDictionary<int, TechNodeConfig> techNodes = tables.TbTechNode.DataMap;

            Dictionary<int, TechNodeConfig> buildingUnlockTechs = new Dictionary<int, TechNodeConfig>();

            foreach (KeyValuePair<int, TechNodeConfig> pair in techNodes)
            {
                TechNodeConfig tech = pair.Value;
                if (tech == null || !tech.Enable || tech.UnlockBuildingId <= 0)
                {
                    continue;
                }

                if (!buildings.TryGetValue(tech.UnlockBuildingId, out WorldBuildingConfig building) || building == null)
                {
                    LogError(ref errorCount, $"科技 {tech.Id} 解锁的建筑 {tech.UnlockBuildingId} 不存在。");
                    continue;
                }

                if (!building.Enable)
                {
                    LogError(ref errorCount, $"科技 {tech.Id} 解锁的建筑 {building.Id} 已禁用。");
                }

                if (buildingUnlockTechs.TryGetValue(tech.UnlockBuildingId, out TechNodeConfig existingTech))
                {
                    LogError(ref errorCount, $"建筑 {tech.UnlockBuildingId} 被多个科技解锁：{existingTech.Id}, {tech.Id}。");
                    continue;
                }

                buildingUnlockTechs.Add(tech.UnlockBuildingId, tech);
            }

            foreach (KeyValuePair<int, WorldBuildingConfig> pair in buildings)
            {
                WorldBuildingConfig building = pair.Value;
                if (building == null || !building.Enable)
                {
                    continue;
                }

                ValidateBuilding(building, buildingUnlockTechs, ref errorCount, ref warningCount);
            }

            if (errorCount > 0)
            {
                Debug.LogError($"大地图配置校验失败：{errorCount} 个错误，{warningCount} 个警告。");
                return;
            }

            if (warningCount > 0)
            {
                Debug.LogWarning($"大地图配置校验完成：0 个错误，{warningCount} 个警告。");
                return;
            }

            Debug.Log("大地图配置校验通过。");
        }

        private static void ValidateBuilding(
            WorldBuildingConfig building,
            IReadOnlyDictionary<int, TechNodeConfig> buildingUnlockTechs,
            ref int errorCount,
            ref int warningCount)
        {
            WorldBuildingUnlockSource source = (WorldBuildingUnlockSource)building.UnlockSourceType;

            if (!System.Enum.IsDefined(typeof(WorldBuildingUnlockSource), source))
            {
                LogError(ref errorCount, $"建筑 {building.Id} 的 unlockSourceType 无效：{building.UnlockSourceType}。");
                return;
            }

            if ((WorldBuildingType)building.BuildingType == WorldBuildingType.House)
            {
                if (!building.DefaultUnlocked || source != WorldBuildingUnlockSource.Default)
                {
                    LogError(ref errorCount, $"House {building.Id} 必须配置为 defaultUnlocked=true 且 unlockSourceType=Default。");
                }
            }

            if (building.DefaultUnlocked && source != WorldBuildingUnlockSource.Default)
            {
                LogWarning(ref warningCount, $"建筑 {building.Id} 已配置 defaultUnlocked=true，但 unlockSourceType 不是 Default。");
            }

            switch (source)
            {
                case WorldBuildingUnlockSource.None:
                    if (building.ShowInBuildPanel)
                    {
                        LogError(ref errorCount, $"建筑 {building.Id} 显示在建造面板，但 unlockSourceType=None，无法解锁。");
                    }
                    break;

                case WorldBuildingUnlockSource.Default:
                    if (!building.DefaultUnlocked)
                    {
                        LogError(ref errorCount, $"建筑 {building.Id} 使用 Default 解锁来源，但 defaultUnlocked=false。");
                    }
                    break;

                case WorldBuildingUnlockSource.Tech:
                    if (!buildingUnlockTechs.ContainsKey(building.Id))
                    {
                        LogError(ref errorCount, $"建筑 {building.Id} 使用 Tech 解锁来源，但没有 TechNode.unlockBuildingId 指向它。");
                    }
                    break;

                case WorldBuildingUnlockSource.Runtime:
                    break;
            }

            if (!building.ShowInBuildPanel)
            {
                return;
            }

            if (building.BuildCategory <= 0)
            {
                LogError(ref errorCount, $"建筑 {building.Id} 显示在建造面板，但 buildCategory 无效。");
            }

            if (building.SizeX <= 0 || building.SizeZ <= 0)
            {
                LogError(ref errorCount, $"建筑 {building.Id} 显示在建造面板，但 sizeX/sizeZ 无效。");
            }

            if (string.IsNullOrWhiteSpace(building.PrefabLocation))
            {
                LogWarning(ref warningCount, $"建筑 {building.Id} 显示在建造面板，但 prefabLocation 为空。");
            }
        }

        private static Tables LoadTables()
        {
            return new Tables(LoadByteBuf);
        }

        private static ByteBuf LoadByteBuf(string file)
        {
            string path = Path.Combine(BinPathPrefix, file + ".bytes");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Luban bytes not found.", path);
            }

            return new ByteBuf(File.ReadAllBytes(path));
        }

        private static void LogError(ref int errorCount, string message)
        {
            errorCount++;
            Debug.LogError("[大地图配置] " + message);
        }

        private static void LogWarning(ref int warningCount, string message)
        {
            warningCount++;
            Debug.LogWarning("[大地图配置] " + message);
        }
    }
}

#endif
