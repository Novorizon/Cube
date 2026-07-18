using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WorldGatherManager
    {
        public static WorldGatherManager Instance { get; } = new WorldGatherManager();

        private readonly Dictionary<int, WorldGatherNodeState> nodeStates = new Dictionary<int, WorldGatherNodeState>();
        private RewardResolver rewardResolver;

        private WorldGatherManager()
        {
        }

        public void Initialize()
        {
            nodeStates.Clear();
            rewardResolver = new RewardResolver(DataManager.Instance.Reward);
        }

        public bool TryGather(MapObjectData mapObject, out IReadOnlyList<ItemStack> rewards)
        {
            rewards = Array.Empty<ItemStack>();

            if (mapObject == null || mapObject.ObjectType != MapObjectType.Resource)
            {
                return false;
            }

            if (!TryGetGatherConfig(mapObject.ConfigId, out GatherConfig config))
            {
                Debug.LogWarning($"Gather failed. Missing resource or gather config: {mapObject.ConfigId}");
                return false;
            }

            int objectId = GetObjectId(mapObject);
            WorldGatherNodeState state = GetOrCreateState(objectId, config);
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!state.CanGather(currentUnixTime, config))
            {
                return false;
            }

            rewardResolver ??= new RewardResolver(DataManager.Instance.Reward);
            rewards = rewardResolver.GetRewardGroup(config.RewardGroupId);
            if (rewards.Count == 0)
            {
                Debug.LogWarning($"Gather failed. Empty reward group: {config.RewardGroupId}");
                return false;
            }

            if (!BagManager.Instance.TryAddItems(rewards))
            {
                return false;
            }

            state.Consume(config, currentUnixTime);
            StorageManager.Instance.MarkDirty();
            return true;
        }

        public bool TryGetStatus(MapObjectData mapObject, out WorldGatherStatus status)
        {
            status = default;

            if (mapObject == null || mapObject.ObjectType != MapObjectType.Resource)
            {
                return false;
            }

            if (!TryGetGatherConfig(mapObject.ConfigId, out GatherConfig config))
            {
                return false;
            }

            int objectId = GetObjectId(mapObject);
            WorldGatherNodeState state = GetOrCreateState(objectId, config);
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool canGather = state.CanGather(currentUnixTime, config);
            status = new WorldGatherStatus(
                state.ObjectId,
                state.GatherConfigId,
                state.RemainingTimes,
                state.AvailableAtUnixTime,
                canGather);
            return true;
        }

        public bool ShouldRemoveDepletedMapObject(MapObjectData mapObject)
        {
            if (mapObject == null || mapObject.ObjectType != MapObjectType.Resource)
            {
                return false;
            }

            if (!TryGetGatherConfig(mapObject.ConfigId, out GatherConfig config) ||
                config.RespawnSeconds > 0)
            {
                return false;
            }

            int objectId = GetObjectId(mapObject);
            return nodeStates.TryGetValue(objectId, out WorldGatherNodeState state) &&
                   state != null &&
                   state.IsDepleted;
        }

        public void Clear()
        {
            nodeStates.Clear();
        }

        private WorldGatherNodeState GetOrCreateState(int objectId, GatherConfig config)
        {
            if (!nodeStates.TryGetValue(objectId, out WorldGatherNodeState state))
            {
                state = new WorldGatherNodeState(objectId, config);
                nodeStates.Add(objectId, state);
            }

            return state;
        }

        private bool TryGetGatherConfig(int worldResourceId, out GatherConfig config)
        {
            config = null;

            if (DataManager.Instance.Resource != null &&
                DataManager.Instance.Resource.TryGet(worldResourceId, out ResourceConfig resourceConfig) &&
                resourceConfig != null &&
                resourceConfig.Enable)
            {
                if (resourceConfig.GatherConfigId <= 0)
                {
                    return false;
                }

                return DataManager.Instance.Gather.TryGet(resourceConfig.GatherConfigId, out config) && config != null && config.Enable;
            }

            return DataManager.Instance.Gather.TryGet(worldResourceId, out config) && config != null && config.Enable;
        }

        private static int GetObjectId(MapObjectData mapObject)
        {
            if (mapObject.ObjectId > 0)
            {
                return mapObject.ObjectId;
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + mapObject.ConfigId;
                hash = hash * 31 + mapObject.X;
                hash = hash * 31 + mapObject.Y;
                hash = hash * 31 + mapObject.Z;
                return hash;
            }
        }

        public SaveGatherNodeData[] CreateSaveData()
        {
            List<SaveGatherNodeData> nodes = new List<SaveGatherNodeData>();
            foreach (KeyValuePair<int, WorldGatherNodeState> pair in nodeStates)
            {
                WorldGatherNodeState state = pair.Value;
                if (state == null || state.ObjectId == 0 || state.GatherConfigId <= 0)
                {
                    continue;
                }

                nodes.Add(new SaveGatherNodeData
                {
                    ObjectId = state.ObjectId,
                    GatherConfigId = state.GatherConfigId,
                    RemainingTimes = state.RemainingTimes,
                    AvailableAtUnixTime = state.AvailableAtUnixTime,
                });
            }

            return nodes.ToArray();
        }

        public void LoadSaveData(IReadOnlyList<SaveGatherNodeData> nodes)
        {
            nodeStates.Clear();
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                SaveGatherNodeData node = nodes[i];
                if (node == null || node.ObjectId == 0 || node.GatherConfigId <= 0)
                {
                    continue;
                }

                nodeStates[node.ObjectId] = new WorldGatherNodeState(
                    node.ObjectId,
                    node.GatherConfigId,
                    node.RemainingTimes,
                    node.AvailableAtUnixTime);
            }
        }
    }
}
