using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class ResourceInteractionController
    {
        private enum InteractionState
        {
            Idle,
            MovingToTarget,
            PerformingAction,
        }

        private const float InteractionDistance = 1.35f;
        private const float ActionStandDistance = 0.85f;
        private const float PickupCollectNormalizedTime = 0.55f;
        private const float PickupTimeoutSeconds = 0.75f;
        private const float ToolActionMarkerNormalizedTime = 0.55f;
        private const float ToolActionTimeoutSeconds = 1.25f;

        private readonly NavigationController navigation;
        private readonly ActionController actions;
        private readonly RewardResolver rewardResolver = new RewardResolver(DataManager.Instance.Reward);

        private InteractionState state;
        private WorldResourceView resourceView;
        private ResourceConfig config;
        private WorldResourceInteractionType interactionType;
        private Vector3 fallbackPosition;
        private bool actionSettled;
        private bool repeatAfterAction;

        public ResourceInteractionController(NavigationController navigation, ActionController actions)
        {
            this.navigation = navigation;
            this.actions = actions;
        }

        public bool IsActive => state != InteractionState.Idle;

        public bool TryStartAtPointer(Camera camera)
        {
            if (camera == null ||
                !WorldPointerPicker.TryPickComponent(
                    GameInputManager.Instance.PointerPosition,
                    camera,
                    out WorldResourceView pickedResource,
                    false) ||
                pickedResource == null)
            {
                return false;
            }

            return Start(pickedResource);
        }

        public void Tick()
        {
            if (state == InteractionState.Idle)
            {
                return;
            }

            if (state == InteractionState.PerformingAction && actionSettled)
            {
                return;
            }

            if (!IsTargetValid())
            {
                Cancel(ActionStopReason.TargetInvalid, state == InteractionState.MovingToTarget);
                return;
            }

            if (state == InteractionState.MovingToTarget)
            {
                return;
            }

            if (!IsPlayerInRange())
            {
                Cancel(ActionStopReason.TargetInvalid, false);
                return;
            }

            navigation.FacePosition(GetCurrentTargetPosition());
        }

        public void Cancel(bool stopMovement = false)
        {
            Cancel(ActionStopReason.UserInput, stopMovement);
        }

        private void Cancel(ActionStopReason reason, bool stopMovement)
        {
            bool wasActive = IsActive;
            StopCurrent(
                reason,
                navigation.IsMoving && !stopMovement ? ActionExitMode.ToMove : ActionExitMode.ToIdle);
            if (stopMovement)
            {
                navigation.Stop();
            }
            else if (wasActive)
            {
                navigation.CancelCompletion();
            }
        }

        public void Interrupt()
        {
            StopCurrent(ActionStopReason.UserInput, ActionExitMode.ToIdle);
            navigation.Stop();
        }

        private bool Start(WorldResourceView target)
        {
            if (target == null || target.MapObject == null)
            {
                return false;
            }

            MapObjectData mapObject = target.MapObject;
            if (!DataManager.Instance.Resource.TryGet(mapObject.ConfigId, out ResourceConfig resourceConfig) ||
                resourceConfig == null ||
                !resourceConfig.Enable)
            {
                return false;
            }

            if (!StopCurrent(
                    ActionStopReason.Replaced,
                    navigation.IsMoving ? ActionExitMode.ToMove : ActionExitMode.ToIdle))
            {
                actions.Stop(
                    ActionStopReason.Replaced,
                    navigation.IsMoving ? ActionExitMode.ToMove : ActionExitMode.ToIdle);
            }

            navigation.CancelCompletion();
            resourceView = target;
            config = resourceConfig;
            interactionType = (WorldResourceInteractionType)resourceConfig.InteractionType;
            fallbackPosition = GetInteractionPosition(target, mapObject, navigation.Position);

            if (IsPlayerInRange())
            {
                navigation.Stop();
                BeginAction();
                return true;
            }

            state = InteractionState.MovingToTarget;
            if (TryMoveNearTarget(mapObject))
            {
                return true;
            }

            Debug.Log($"Resource interaction move failed. No reachable path. target: {GetCurrentTargetPosition()}");
            ClearState();
            return true;
        }

        private bool TryMoveNearTarget(MapObjectData mapObject)
        {
            WorldResourceView target = resourceView;
            bool moved = navigation.TryMoveToBestApproach(
                mapObject.Coord,
                coord => GetApproachDistance(coord, target, mapObject) <= InteractionDistance,
                coord => GetApproachDistance(coord, target, mapObject),
                coord => GetApproachDestination(coord, target, mapObject),
                BeginAction,
                IsPlayerInRange);
            if (moved)
            {
                return true;
            }

            Vector3 targetPosition = GetCurrentTargetPosition();
            if (TryGetTopLogicCoordFromWorld(targetPosition, out Vector3Int targetCoord) &&
                navigation.TryMoveTo(targetCoord, BeginAction, IsPlayerInRange))
            {
                return true;
            }

            return navigation.TryMoveTo(mapObject.Coord, BeginAction, IsPlayerInRange);
        }

        private void BeginAction()
        {
            if (!IsTargetValid() || !IsPlayerInRange())
            {
                StopCurrent(ActionStopReason.TargetInvalid, ActionExitMode.ToIdle);
                return;
            }

            state = InteractionState.PerformingAction;
            navigation.FacePosition(GetCurrentTargetPosition());
            StartNextAction();
        }

        private bool StartNextAction()
        {
            if (state != InteractionState.PerformingAction || !IsTargetValid() || !IsPlayerInRange())
            {
                StopCurrent(ActionStopReason.TargetInvalid, ActionExitMode.ToIdle);
                return false;
            }

            actionSettled = false;
            repeatAfterAction = false;
            switch (interactionType)
            {
                case WorldResourceInteractionType.Pickup:
                    if (!actions.TryStart(
                            ActionRequest.Pickup(PickupCollectNormalizedTime, PickupTimeoutSeconds),
                            new ActionCallbacks(SettleCurrentAction, CompleteCurrentActionPlayback)))
                    {
                        StopCurrent(ActionStopReason.StartFailed, ActionExitMode.ToIdle);
                        return false;
                    }

                    return true;

                case WorldResourceInteractionType.Gather:
                    if (!CanContinueGather(resourceView))
                    {
                        StopCurrent(ActionStopReason.TargetInvalid, ActionExitMode.ToIdle);
                        return false;
                    }

                    ToolKitActionType gatherAction = GetGatherToolAction(config);
                    if (!ToolKitManager.Instance.TryUseToolForAction(gatherAction, out _) ||
                        !actions.TryStart(
                            ActionRequest.Tool(
                                ActionId.Gather,
                                gatherAction,
                                ToolActionMarkerNormalizedTime,
                                ToolActionTimeoutSeconds),
                            new ActionCallbacks(SettleCurrentAction, CompleteCurrentActionPlayback)))
                    {
                        StopCurrent(ActionStopReason.StartFailed, ActionExitMode.ToIdle);
                        return false;
                    }

                    return true;

                case WorldResourceInteractionType.MineTarget:
                    if (!ToolKitManager.Instance.TryUseToolForAction(ToolKitActionType.BuildMine, out _) ||
                        !actions.TryStart(
                            ActionRequest.Tool(
                                ActionId.Mine,
                                ToolKitActionType.BuildMine,
                                ToolActionMarkerNormalizedTime,
                                ToolActionTimeoutSeconds),
                            new ActionCallbacks(SettleCurrentAction, CompleteCurrentActionPlayback)))
                    {
                        StopCurrent(ActionStopReason.StartFailed, ActionExitMode.ToIdle);
                        return false;
                    }

                    return true;

                default:
                    StopCurrent(ActionStopReason.StartFailed, ActionExitMode.ToIdle);
                    return false;
            }
        }

        private void SettleCurrentAction()
        {
            if (state != InteractionState.PerformingAction || !IsTargetValid() || !IsPlayerInRange())
            {
                StopCurrent(ActionStopReason.TargetInvalid, ActionExitMode.ToIdle);
                return;
            }

            WorldResourceView completedView = resourceView;
            ResourceConfig completedConfig = config;
            WorldResourceInteractionType completedType = interactionType;
            if (!CompleteActionOnce(completedView, completedConfig, completedType))
            {
                StopCurrent(ActionStopReason.TargetInvalid, ActionExitMode.ToIdle);
                return;
            }

            actionSettled = true;
            WorldMainPanel.Instance?.RefreshNow();
            repeatAfterAction = completedType == WorldResourceInteractionType.Gather &&
                                CanContinueGather(completedView);
            if (completedType == WorldResourceInteractionType.Pickup)
            {
                ClearState();
            }
        }

        private void CompleteCurrentActionPlayback()
        {
            if (state != InteractionState.PerformingAction || !actionSettled)
            {
                return;
            }

            bool shouldRepeat = repeatAfterAction;
            actionSettled = false;
            repeatAfterAction = false;
            if (shouldRepeat && IsTargetValid() && IsPlayerInRange())
            {
                StartNextAction();
                return;
            }

            StopCurrent(ActionStopReason.Completed, ActionExitMode.ToIdle);
        }

        private bool CompleteActionOnce(
            WorldResourceView completedView,
            ResourceConfig completedConfig,
            WorldResourceInteractionType completedType)
        {
            if (completedView == null || completedView.MapObject == null || completedConfig == null)
            {
                return false;
            }

            MapObjectData mapObject = completedView.MapObject;
            switch (completedType)
            {
                case WorldResourceInteractionType.Pickup:
                    return PickupResource(completedView, completedConfig);

                case WorldResourceInteractionType.Gather:
                    if (!ToolKitManager.Instance.TryUseToolForAction(
                            GetGatherToolAction(completedConfig),
                            out int gatherToolItemId) ||
                        !WorldGatherManager.Instance.TryGather(mapObject, out _))
                    {
                        return false;
                    }

                    ItemManager.Instance.NotifyUseCompleted(gatherToolItemId);

                    if (WorldGatherManager.Instance.ShouldRemoveDepletedMapObject(mapObject))
                    {
                        RemoveResourceView(completedView);
                    }
                    else
                    {
                        completedView.RefreshNow();
                    }

                    return true;

                case WorldResourceInteractionType.MineTarget:
                    if (!ToolKitManager.Instance.TryUseToolForAction(
                            ToolKitActionType.BuildMine,
                            out int mineToolItemId) ||
                        !MineManager.Instance.TryBuildMine(completedView, completedConfig))
                    {
                        return false;
                    }

                    ItemManager.Instance.NotifyUseCompleted(mineToolItemId);
                    return true;

                default:
                    return false;
            }
        }

        private bool PickupResource(WorldResourceView target, ResourceConfig resourceConfig)
        {
            IReadOnlyList<ItemStack> rewards = rewardResolver.GetRewardGroup(resourceConfig.PickupRewardGroupId);
            if (rewards.Count == 0 || !BagManager.Instance.TryAddItems(rewards))
            {
                return false;
            }

            RemoveResourceView(target);
            StorageManager.Instance.MarkDirty();
            return true;
        }

        private static void RemoveResourceView(WorldResourceView target)
        {
            if (target == null || target.MapObject == null)
            {
                return;
            }

            MapManager.Instance.TryRemoveMapObject(target.MapObject.ObjectId);
            MapManager.Instance.MarkMapObjectRemoved(target.MapObject.ObjectId);
            Object.Destroy(target.gameObject);
        }

        private static bool CanContinueGather(WorldResourceView target)
        {
            return target != null &&
                   target.MapObject != null &&
                   WorldGatherManager.Instance.TryGetStatus(target.MapObject, out WorldGatherStatus status) &&
                   status.CanGather;
        }

        private static ToolKitActionType GetGatherToolAction(ResourceConfig resourceConfig)
        {
            if (resourceConfig == null)
            {
                return ToolKitActionType.None;
            }

            return ActionToolResolver.GetGatherAction((WorldResourceCategory)resourceConfig.ResourceType);
        }

        private bool IsTargetValid()
        {
            return resourceView != null && resourceView.MapObject != null && config != null;
        }

        private bool IsPlayerInRange()
        {
            if (navigation.Player == null)
            {
                return false;
            }

            Vector3 playerPosition = navigation.Position;
            Vector3 targetPosition = GetCurrentTargetPosition();
            return Vector2.Distance(
                new Vector2(playerPosition.x, playerPosition.z),
                new Vector2(targetPosition.x, targetPosition.z)) <= InteractionDistance;
        }

        private Vector3 GetCurrentTargetPosition()
        {
            if (resourceView != null && resourceView.MapObject != null && navigation.Player != null)
            {
                return GetInteractionPosition(resourceView, resourceView.MapObject, navigation.Position);
            }

            return fallbackPosition;
        }

        private bool StopCurrent(ActionStopReason reason, ActionExitMode exitMode)
        {
            bool hadTarget = state != InteractionState.Idle || resourceView != null;
            ClearState();
            if (hadTarget)
            {
                actions.Stop(reason, exitMode);
            }

            return hadTarget;
        }

        private void ClearState()
        {
            state = InteractionState.Idle;
            resourceView = null;
            config = null;
            interactionType = default;
            fallbackPosition = Vector3.zero;
            actionSettled = false;
            repeatAfterAction = false;
        }

        private static float GetApproachDistance(Vector3Int coord, WorldResourceView target, MapObjectData mapObject)
        {
            Vector3 standPosition = NavigationController.GetStandPosition(coord);
            Vector3 targetPosition = GetInteractionPosition(target, mapObject, standPosition);
            return Vector2.Distance(
                new Vector2(standPosition.x, standPosition.z),
                new Vector2(targetPosition.x, targetPosition.z));
        }

        private static Vector3 GetApproachDestination(Vector3Int coord, WorldResourceView target, MapObjectData mapObject)
        {
            Vector3 standPosition = NavigationController.GetStandPosition(coord);
            Vector3 targetPosition = GetInteractionPosition(target, mapObject, standPosition);
            Vector3 flatDirection = standPosition - targetPosition;
            flatDirection.y = 0f;
            float distance = flatDirection.magnitude;
            if (distance <= 0.0001f)
            {
                return standPosition;
            }

            float preferredDistance = Mathf.Min(distance, ActionStandDistance);
            Vector3 destination = targetPosition + flatDirection.normalized * preferredDistance;
            destination.y = standPosition.y;
            return destination;
        }

        private static Vector3 GetInteractionPosition(WorldResourceView target, MapObjectData mapObject, Vector3 fromPosition)
        {
            if (target != null && target.TryGetClosestPoint(fromPosition, out Vector3 closestPoint))
            {
                return closestPoint;
            }

            if (mapObject == null)
            {
                return target != null ? target.transform.position : fromPosition;
            }

            Vector3 tilePosition = MapManager.Instance.GetTileWorldPosition(mapObject.Coord);
            Vector3 localPosition = mapObject.LocalPosition;
            return tilePosition + new Vector3(localPosition.x, 0f, localPosition.z);
        }

        private static bool TryGetTopLogicCoordFromWorld(Vector3 worldPosition, out Vector3Int coord)
        {
            coord = default;
            float tileSize = Mathf.Max(0.01f, MapManager.Instance.TileSize);
            int x = Mathf.FloorToInt(worldPosition.x / tileSize + 0.5f);
            int z = Mathf.FloorToInt(worldPosition.z / tileSize + 0.5f);
            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData) || tileData == null)
            {
                return false;
            }

            coord = tileData.Coord;
            return true;
        }
    }
}
