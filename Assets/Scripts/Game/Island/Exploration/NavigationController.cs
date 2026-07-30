using Game.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class NavigationController
    {
        private const float MoveSpeed = 2f;
        private const float TurnSpeed = 540f;
        private const int TargetSearchRadius = 6;
        private const float ArrivalDistanceSqr = 0.0025f;

        private readonly MapPathFinder pathFinder = new MapPathFinder();
        private readonly List<Vector3Int> path = new List<Vector3Int>();
        private readonly List<Vector3Int> smoothedPath = new List<Vector3Int>();
        private readonly Func<ActionController> actionProvider;

        private Transform player;
        private WorldPlayerView playerView;
        private Vector3 destination;
        private bool hasDestination;
        private int pathIndex;
        private bool hasCustomFinalDestination;
        private Vector3 customFinalDestination;
        private Action arrived;
        private Func<bool> stopCondition;

        public bool UsePathSmoothing { get; set; } = true;
        public bool IsMoving => hasDestination;
        public Transform Player => player;
        public WorldPlayerView View => playerView;
        public Vector3 Position => player != null ? player.position : Vector3.zero;

        public NavigationController(Func<ActionController> actionProvider)
        {
            this.actionProvider = actionProvider;
        }

        public bool Ensure()
        {
            if (player != null)
            {
                return false;
            }

            GameObject playerObject = GameObject.Find("WorldPlayer");
            if (playerObject == null)
            {
                GameObject prefab = ResourceManager.Instance.LoadGameObject(WorldPlayerView.PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Missing world player prefab: {WorldPlayerView.PrefabPath}");
                    return false;
                }

                playerObject = GameObject.Instantiate(prefab);
                playerObject.name = "WorldPlayer";
            }

            player = playerObject.transform;
            playerView = playerObject.GetComponent<WorldPlayerView>();
            if (playerView == null)
            {
                playerView = playerObject.AddComponent<WorldPlayerView>();
            }

            ApplyStartTransform();
            ClearPath();
            destination = player.position;
            playerView.SetMoveSpeed(0f);
            return true;
        }

        public void Tick()
        {
            if (player == null)
            {
                return;
            }

            if (!hasDestination)
            {
                playerView?.SetMoveSpeed(0f);
                return;
            }

            if (TryFinishFromStopCondition())
            {
                return;
            }

            Vector3 current = player.position;
            Vector3 next = Vector3.MoveTowards(current, destination, MoveSpeed * Time.deltaTime);
            FaceDirection(destination - current);
            player.position = next;
            playerView?.SetMoveSpeed(MoveSpeed);

            if (TryFinishFromStopCondition())
            {
                return;
            }

            if ((next - destination).sqrMagnitude > ArrivalDistanceSqr)
            {
                return;
            }

            player.position = destination;
            if (TryAdvancePath())
            {
                return;
            }

            FinishMovement();
        }

        public bool TryMoveTo(Vector3Int destinationCoord, Action onArrived = null, Func<bool> shouldStop = null)
        {
            ClearCustomFinalDestination();
            if (!TryGetPathStartCoord(out Vector3Int startCoord) ||
                !TryResolveReachablePath(startCoord, destinationCoord, out List<Vector3Int> resolvedPath) ||
                resolvedPath == null ||
                resolvedPath.Count == 0)
            {
                return false;
            }

            BeginPath(resolvedPath, onArrived, shouldStop);
            return true;
        }

        public bool TryMoveTo(Vector3Int destinationCoord, Vector3 finalDestination, Action onArrived = null, Func<bool> shouldStop = null)
        {
            ClearCustomFinalDestination();
            if (!TryGetPathStartCoord(out Vector3Int startCoord) ||
                !TryResolveReachablePath(startCoord, destinationCoord, out List<Vector3Int> resolvedPath) ||
                resolvedPath == null ||
                resolvedPath.Count == 0)
            {
                return false;
            }

            if (resolvedPath[resolvedPath.Count - 1] == destinationCoord)
            {
                customFinalDestination = finalDestination;
                hasCustomFinalDestination = true;
            }

            BeginPath(resolvedPath, onArrived, shouldStop);
            return true;
        }

        public bool TryMoveToBestApproach(
            Vector3Int origin,
            Func<Vector3Int, bool> isCandidate,
            Func<Vector3Int, float> getTargetDistance,
            Func<Vector3Int, Vector3> getFinalDestination,
            Action onArrived,
            Func<bool> shouldStop)
        {
            ClearCustomFinalDestination();
            if (!TryGetPathStartCoord(out Vector3Int startCoord))
            {
                return false;
            }

            bool found = false;
            List<Vector3Int> bestPath = null;
            int bestPathCost = int.MaxValue;
            float bestTargetDistance = float.MaxValue;
            for (int radius = 0; radius <= TargetSearchRadius; radius++)
            {
                if (radius == 0)
                {
                    ConsiderApproach(startCoord, origin, 0, 0, isCandidate, getTargetDistance, ref found, ref bestPath, ref bestPathCost, ref bestTargetDistance);
                }
                else
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        ConsiderApproach(startCoord, origin, dx, -radius, isCandidate, getTargetDistance, ref found, ref bestPath, ref bestPathCost, ref bestTargetDistance);
                        ConsiderApproach(startCoord, origin, dx, radius, isCandidate, getTargetDistance, ref found, ref bestPath, ref bestPathCost, ref bestTargetDistance);
                    }

                    for (int dz = -radius + 1; dz <= radius - 1; dz++)
                    {
                        ConsiderApproach(startCoord, origin, -radius, dz, isCandidate, getTargetDistance, ref found, ref bestPath, ref bestPathCost, ref bestTargetDistance);
                        ConsiderApproach(startCoord, origin, radius, dz, isCandidate, getTargetDistance, ref found, ref bestPath, ref bestPathCost, ref bestTargetDistance);
                    }
                }
            }

            if (!found || bestPath == null || bestPath.Count == 0)
            {
                return false;
            }

            Vector3Int finalCoord = bestPath[bestPath.Count - 1];
            customFinalDestination = getFinalDestination(finalCoord);
            hasCustomFinalDestination = true;
            BeginPath(bestPath, onArrived, shouldStop);
            return true;
        }

        public void Stop()
        {
            hasDestination = false;
            ClearPath();
            ClearCompletion();
            playerView?.SetMoveSpeed(0f);
        }

        public void CancelCompletion()
        {
            ClearCompletion();
        }

        public void FacePosition(Vector3 targetPosition)
        {
            if (player != null)
            {
                FaceDirection(targetPosition - player.position);
            }
        }

        public Vector3? GetCameraTargetPosition()
        {
            if (playerView != null)
            {
                return playerView.CameraTargetPosition;
            }

            return player != null ? player.position : (Vector3?)null;
        }

        public SavePlayerData CreateSaveData()
        {
            if (player == null || MapManager.Instance.CurrentMap == null)
            {
                return null;
            }

            Vector3 position = player.position;
            return new SavePlayerData
            {
                MapId = MapManager.Instance.CurrentMap.Id,
                X = position.x,
                Y = position.y,
                Z = position.z,
                RotationY = player.eulerAngles.y,
            };
        }

        public bool TryGetTileCoord(out Vector3Int coord)
        {
            coord = default;
            if (player == null)
            {
                return false;
            }

            float tileSize = Mathf.Max(0.01f, MapManager.Instance.TileSize);
            int x = Mathf.FloorToInt(player.position.x / tileSize + 0.5f);
            int z = Mathf.FloorToInt(player.position.z / tileSize + 0.5f);
            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData) || tileData == null)
            {
                return false;
            }

            coord = tileData.Coord;
            return true;
        }

        public static Vector3 GetStandPosition(Vector3Int coord)
        {
            return MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * (MapManager.Instance.TileSize * 1.12f);
        }

        private void ApplyStartTransform()
        {
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            if (StorageManager.Instance.TryGetPlayerSaveData(mapId, out SavePlayerData savedPlayer) && savedPlayer != null)
            {
                player.position = new Vector3(savedPlayer.X, savedPlayer.Y, savedPlayer.Z);
                player.rotation = Quaternion.Euler(0f, savedPlayer.RotationY, 0f);
                return;
            }

            player.position = FindStartPosition();
        }

        private static Vector3 FindStartPosition()
        {
            if (MapManager.Instance.CurrentMap != null &&
                MapManager.Instance.CurrentMap.SpawnPoints != null &&
                MapManager.Instance.CurrentMap.SpawnPoints.Count > 0)
            {
                return GetStandPosition(MapManager.Instance.CurrentMap.SpawnPoints[0]);
            }

            MapData map = MapManager.Instance.CurrentMap;
            if (map == null)
            {
                return Vector3.zero;
            }

            Vector3 center = new Vector3(
                (map.Width - 1) * MapManager.Instance.TileSize * 0.5f,
                0f,
                (map.Depth - 1) * MapManager.Instance.TileSize * 0.5f);
            return center + Vector3.up * (MapManager.Instance.TileSize * 1.12f);
        }

        private void BeginPath(IReadOnlyList<Vector3Int> resolvedPath, Action onArrived, Func<bool> shouldStop)
        {
            ActionController actionController = actionProvider?.Invoke();
            if (actionController != null && actionController.IsRunning)
            {
                actionController.Stop(ActionStopReason.Movement, ActionExitMode.ToMove);
            }

            path.Clear();
            smoothedPath.Clear();
            IReadOnlyList<Vector3Int> movementPath = resolvedPath;
            if (UsePathSmoothing && resolvedPath.Count > 2)
            {
                MapPathSmoother.SmoothBySupercoverLineOfSight(resolvedPath, smoothedPath);
                if (smoothedPath.Count > 0)
                {
                    movementPath = smoothedPath;
                }
            }

            for (int i = 0; i < movementPath.Count; i++)
            {
                path.Add(movementPath[i]);
            }

            arrived = onArrived;
            stopCondition = shouldStop;
            pathIndex = path.Count > 1 ? 1 : 0;
            SetDestinationForCurrentPathIndex();
        }

        private bool TryAdvancePath()
        {
            if (path.Count == 0 || pathIndex >= path.Count - 1)
            {
                ClearPath();
                return false;
            }

            pathIndex++;
            SetDestinationForCurrentPathIndex();
            return true;
        }

        private void SetDestinationForCurrentPathIndex()
        {
            if (hasCustomFinalDestination && pathIndex == path.Count - 1)
            {
                destination = customFinalDestination;
                hasDestination = true;
                return;
            }

            destination = GetStandPosition(path[pathIndex]);
            hasDestination = true;
        }

        private bool TryFinishFromStopCondition()
        {
            if (stopCondition == null)
            {
                return false;
            }

            if (hasCustomFinalDestination)
            {
                if (path.Count > 0 && pathIndex < path.Count - 1)
                {
                    return false;
                }

                if ((player.position - customFinalDestination).sqrMagnitude > ArrivalDistanceSqr)
                {
                    return false;
                }
            }
            else if (!stopCondition())
            {
                return false;
            }

            FinishMovement();
            return true;
        }

        private void FinishMovement()
        {
            Action callback = arrived;
            hasDestination = false;
            ClearPath();
            ClearCompletion();
            playerView?.SetMoveSpeed(0f);
            StorageManager.Instance.MarkDirty();
            callback?.Invoke();
        }

        private void FaceDirection(Vector3 direction)
        {
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            player.rotation = Quaternion.RotateTowards(player.rotation, targetRotation, TurnSpeed * Time.deltaTime);
        }

        private bool TryGetPathStartCoord(out Vector3Int coord)
        {
            coord = default;
            if (!TryGetTileCoord(out Vector3Int currentCoord))
            {
                return false;
            }

            if (MapManager.Instance.IsWalkable(currentCoord))
            {
                coord = currentCoord;
                return true;
            }

            return TryFindNearestWalkableCoord(currentCoord, TargetSearchRadius, out coord);
        }

        private bool TryResolveReachablePath(Vector3Int startCoord, Vector3Int targetCoord, out List<Vector3Int> resolvedPath)
        {
            resolvedPath = null;
            if (MapManager.Instance.IsWalkable(targetCoord) &&
                pathFinder.TryFindPath(startCoord, targetCoord, out resolvedPath) &&
                resolvedPath != null &&
                resolvedPath.Count > 0)
            {
                return true;
            }

            return TryFindNearestReachablePath(startCoord, targetCoord, TargetSearchRadius, out resolvedPath);
        }

        private bool TryFindNearestReachablePath(Vector3Int startCoord, Vector3Int origin, int maxRadius, out List<Vector3Int> resolvedPath)
        {
            resolvedPath = null;
            maxRadius = Mathf.Max(0, maxRadius);
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                bool found = false;
                List<Vector3Int> bestPath = null;
                int bestDistance = int.MaxValue;
                if (radius == 0)
                {
                    ConsiderReachablePath(startCoord, origin, 0, 0, ref found, ref bestPath, ref bestDistance);
                }
                else
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        ConsiderReachablePath(startCoord, origin, dx, -radius, ref found, ref bestPath, ref bestDistance);
                        ConsiderReachablePath(startCoord, origin, dx, radius, ref found, ref bestPath, ref bestDistance);
                    }

                    for (int dz = -radius + 1; dz <= radius - 1; dz++)
                    {
                        ConsiderReachablePath(startCoord, origin, -radius, dz, ref found, ref bestPath, ref bestDistance);
                        ConsiderReachablePath(startCoord, origin, radius, dz, ref found, ref bestPath, ref bestDistance);
                    }
                }

                if (found)
                {
                    resolvedPath = bestPath;
                    return true;
                }
            }

            return false;
        }

        private void ConsiderReachablePath(Vector3Int startCoord, Vector3Int origin, int offsetX, int offsetZ, ref bool found, ref List<Vector3Int> bestPath, ref int bestDistance)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(origin.x + offsetX, origin.z + offsetZ, out TileData tileData) ||
                tileData == null ||
                !MapManager.Instance.IsWalkable(tileData.Coord))
            {
                return;
            }

            int distance = Mathf.Abs(offsetX) + Mathf.Abs(offsetZ);
            if (found && distance >= bestDistance)
            {
                return;
            }

            if (!pathFinder.TryFindPath(startCoord, tileData.Coord, out List<Vector3Int> candidatePath) ||
                candidatePath == null ||
                candidatePath.Count == 0)
            {
                return;
            }

            found = true;
            bestPath = candidatePath;
            bestDistance = distance;
        }

        private static bool TryFindNearestWalkableCoord(Vector3Int origin, int maxRadius, out Vector3Int result)
        {
            result = default;
            if (MapManager.Instance.IsWalkable(origin))
            {
                result = origin;
                return true;
            }

            for (int radius = 1; radius <= Mathf.Max(0, maxRadius); radius++)
            {
                bool found = false;
                Vector3Int best = default;
                int bestDistance = int.MaxValue;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    ConsiderWalkableCoord(origin, dx, -radius, ref found, ref best, ref bestDistance);
                    ConsiderWalkableCoord(origin, dx, radius, ref found, ref best, ref bestDistance);
                }

                for (int dz = -radius + 1; dz <= radius - 1; dz++)
                {
                    ConsiderWalkableCoord(origin, -radius, dz, ref found, ref best, ref bestDistance);
                    ConsiderWalkableCoord(origin, radius, dz, ref found, ref best, ref bestDistance);
                }

                if (found)
                {
                    result = best;
                    return true;
                }
            }

            return false;
        }

        private static void ConsiderWalkableCoord(Vector3Int origin, int offsetX, int offsetZ, ref bool found, ref Vector3Int best, ref int bestDistance)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(origin.x + offsetX, origin.z + offsetZ, out TileData tileData) ||
                tileData == null ||
                !MapManager.Instance.IsWalkable(tileData.Coord))
            {
                return;
            }

            int distance = Mathf.Abs(offsetX) + Mathf.Abs(offsetZ);
            if (found && distance >= bestDistance)
            {
                return;
            }

            found = true;
            best = tileData.Coord;
            bestDistance = distance;
        }

        private void ConsiderApproach(
            Vector3Int startCoord,
            Vector3Int origin,
            int offsetX,
            int offsetZ,
            Func<Vector3Int, bool> isCandidate,
            Func<Vector3Int, float> getTargetDistance,
            ref bool found,
            ref List<Vector3Int> bestPath,
            ref int bestPathCost,
            ref float bestTargetDistance)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(origin.x + offsetX, origin.z + offsetZ, out TileData tileData) ||
                tileData == null ||
                !MapManager.Instance.IsWalkable(tileData.Coord) ||
                isCandidate == null ||
                !isCandidate(tileData.Coord))
            {
                return;
            }

            if (!pathFinder.TryFindPath(startCoord, tileData.Coord, out List<Vector3Int> candidatePath) ||
                candidatePath == null ||
                candidatePath.Count == 0)
            {
                return;
            }

            int pathCost = GetPathCost(candidatePath);
            float targetDistance = getTargetDistance != null ? getTargetDistance(tileData.Coord) : 0f;
            if (found && (pathCost > bestPathCost || pathCost == bestPathCost && targetDistance >= bestTargetDistance))
            {
                return;
            }

            found = true;
            bestPath = candidatePath;
            bestPathCost = pathCost;
            bestTargetDistance = targetDistance;
        }

        private static int GetPathCost(IReadOnlyList<Vector3Int> candidatePath)
        {
            int cost = 0;
            for (int i = 1; i < candidatePath.Count; i++)
            {
                int moveCost = MapManager.Instance.GetMoveCost(candidatePath[i]);
                cost += moveCost == int.MaxValue ? 1 : Mathf.Max(1, moveCost);
            }

            return cost;
        }

        private void ClearPath()
        {
            path.Clear();
            smoothedPath.Clear();
            pathIndex = 0;
            ClearCustomFinalDestination();
        }

        private void ClearCustomFinalDestination()
        {
            hasCustomFinalDestination = false;
            customFinalDestination = Vector3.zero;
        }

        private void ClearCompletion()
        {
            arrived = null;
            stopCondition = null;
        }
    }
}
