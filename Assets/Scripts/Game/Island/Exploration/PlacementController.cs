using Game.Framework;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class PlacementController
    {
        private const float DragThresholdPixels = 12f;
        private const float CultivateMarkerNormalizedTime = 0.55f;
        private const float CultivateTimeoutSeconds = 1.25f;

        private readonly CameraController camera;
        private readonly NavigationController navigation;
        private readonly ResourceInteractionController resourceInteraction;
        private readonly ActionController actions;
        private readonly BuildingPreview buildingPreview;
        private readonly FarmAreaPreview farmAreaPreview;

        private bool pointerActive;
        private bool pressOverUi;
        private Vector2 pressScreenPosition;
        private Vector3Int pressCoord;
        private bool pressHasTile;
        private Farm selectedFarm;
        private WorldBuilding selectedBuilding;
        private int selectedBuildingId;
        private bool farmAreaMode;

        public PlacementController(
            CameraController camera,
            NavigationController navigation,
            ResourceInteractionController resourceInteraction,
            ActionController actions)
        {
            this.camera = camera;
            this.navigation = navigation;
            this.resourceInteraction = resourceInteraction;
            this.actions = actions;
            PlacementMaterials materials = new PlacementMaterials();
            buildingPreview = new BuildingPreview(materials);
            farmAreaPreview = new FarmAreaPreview(materials);
        }

        public int SelectedBuildingId => selectedBuildingId;
        public bool IsFarmAreaMode => farmAreaMode;
        public Farm SelectedFarm => selectedFarm;
        public WorldBuilding SelectedBuilding => selectedBuilding;

        public void BeginPointer()
        {
            pointerActive = true;
            pressScreenPosition = GameInputManager.Instance.PointerPosition;
            pressOverUi = WorldPointerPicker.IsPointerOverUi();
            pressHasTile = TryPickTileCoord(out pressCoord);
        }

        public void Tick()
        {
            UpdateBuildingPreview();
            UpdateFarmAreaPreview();
            UpdatePointer();
        }

        public bool TryCancelCurrentMode()
        {
            bool hasModeToCancel = selectedBuildingId > 0 ||
                                   farmAreaMode ||
                                   selectedFarm != null ||
                                   selectedBuilding != null;
            if (!hasModeToCancel)
            {
                return false;
            }

            StopCultivateAction(ActionStopReason.UserInput);
            HideSeedPanel();
            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            selectedBuildingId = 0;
            ClearSelectedObject();
            SetFarmAreaMode(false);
            buildingPreview.Clear();
            WorldMainPanel.Instance?.RefreshNow();
            return true;
        }

        public RequirementResult TryPlantSelectedFarm(int cropId)
        {
            bool success = FarmManager.Instance.TryPlant(selectedFarm, cropId, out RequirementResult requirement);
            if (success)
            {
                WorldMainPanel.Instance?.RefreshNow();
            }

            return requirement;
        }

        public void SelectBuilding(int buildingId)
        {
            StopCultivateAction(ActionStopReason.Replaced);
            selectedBuildingId = buildingId;
            ClearSelectedObject();
            SetFarmAreaMode(false);
            buildingPreview.Clear();
            HideSeedPanel();
            WorldMainPanel.Instance?.RefreshNow();
        }

        public RequirementResult SelectFarmAreaMode()
        {
            RequirementResult requirement = FarmRequirementChecker.CheckCanEnterCultivation(out _);
            if (!requirement.Succeeded)
            {
                return requirement;
            }

            StopCultivateAction(ActionStopReason.Replaced);
            selectedBuildingId = 0;
            ClearSelectedObject();
            buildingPreview.Clear();
            ResetPointer();
            SetFarmAreaMode(true);
            HideSeedPanel();
            WorldMainPanel.Instance?.RefreshNow();
            return RequirementResult.Success();
        }

        public void ClearSelectedBuilding()
        {
            StopCultivateAction(ActionStopReason.UserInput);
            selectedBuildingId = 0;
            ClearSelectedObject();
            SetFarmAreaMode(false);
            buildingPreview.Clear();
            WorldMainPanel.Instance?.RefreshNow();
        }

        public void Dispose()
        {
            StopCultivateAction(ActionStopReason.Disabled);
            HideSeedPanel();
            buildingPreview.Clear();
            farmAreaPreview.Clear();
            ResetPointer();
        }

        private void UpdatePointer()
        {
            if (!pointerActive || GameInputManager.Instance.WorldSelectHeld)
            {
                return;
            }

            float dragDistance = Vector2.Distance(pressScreenPosition, GameInputManager.Instance.PointerPosition);
            if (!pressOverUi)
            {
                if (dragDistance >= DragThresholdPixels)
                {
                    CompleteFarmDrag();
                }
                else
                {
                    HandleClick();
                }
            }

            pointerActive = false;
            pressHasTile = false;
        }

        private void HandleClick()
        {
            resourceInteraction.Cancel();

            if (farmAreaMode)
            {
                RequirementToast.TryPass(FarmRequirementChecker.DragRequired());
                return;
            }

            if (selectedBuildingId > 0)
            {
                HideSeedPanel();
                WorldMainPanel.Instance?.HideBuildingDetailPanel();
                ClearSelectedObject();
                if (TryPickTileCoord(out Vector3Int buildCoord))
                {
                    TryBuildSelectedBuilding(buildCoord);
                }

                return;
            }

            if (!TryPickTileCoord(out Vector3Int coord))
            {
                HideSeedPanel();
                WorldMainPanel.Instance?.HideBuildingDetailPanel();
                ClearSelectedObject();
                return;
            }

            if (FarmManager.Instance.TryGetFarmAt(coord, out Farm farm))
            {
                SelectFarmForInteraction(farm);
                return;
            }

            HideSeedPanel();
            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            ClearSelectedObject();
            if (TrySelectBuildingAt(coord))
            {
                WorldMainPanel.Instance?.ShowBuildingDetailPanel(selectedBuilding);
                WorldMainPanel.Instance?.RefreshNow();
            }
        }

        private void SelectFarmForInteraction(Farm farm)
        {
            if (farm == null)
            {
                return;
            }

            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            ClearSelectedObject();
            selectedFarm = farm;
            ShowSeedPanel();
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void CompleteFarmDrag()
        {
            if (!farmAreaMode)
            {
                return;
            }

            if (!pressHasTile || !TryPickTileCoord(out Vector3Int endCoord))
            {
                Debug.Log("Create farm area failed. Drag start or end is not a valid tile.");
                RequirementToast.TryPass(FarmRequirementChecker.InvalidDrag());
                return;
            }

            RequirementResult entryRequirement = FarmRequirementChecker.CheckCanEnterCultivation(out _);
            if (!RequirementToast.TryPass(entryRequirement))
            {
                return;
            }

            if (selectedBuildingId > 0)
            {
                Debug.Log("Create farm area failed. Building mode is active.");
                RequirementToast.TryPass(FarmRequirementChecker.BuildingModeActive());
                return;
            }

            if (!ToolKitManager.Instance.TryUseToolForAction(
                    ToolKitActionType.CultivateFarm,
                    out int cultivateToolItemId))
            {
                Debug.Log("Create farm area failed. Missing hoe in toolkit.");
                RequirementToast.TryPass(FarmRequirementChecker.CheckCanEnterCultivation(out _));
                return;
            }

            Vector3Int startCoord = pressCoord;
            resourceInteraction.Cancel(true);
            actions.Stop(ActionStopReason.Replaced, ActionExitMode.ToIdle);
            if (!actions.TryStart(
                    ActionRequest.Tool(
                        ActionId.CultivateFarm,
                        ToolKitActionType.CultivateFarm,
                        CultivateMarkerNormalizedTime,
                        CultivateTimeoutSeconds),
                    ActionCallbacks.AtMarker(
                        () => CompleteCultivateFarm(startCoord, endCoord, cultivateToolItemId))))
            {
                Debug.Log("Create farm area failed. Another action is active.");
                RequirementToast.TryPass(FarmRequirementChecker.ActionUnavailable());
                return;
            }
        }

        private void CompleteCultivateFarm(Vector3Int startCoord, Vector3Int endCoord, int toolItemId)
        {
            selectedFarm = FarmManager.Instance.CreateFarmArea(startCoord, endCoord);
            if (selectedFarm != null)
            {
                ItemManager.Instance.NotifyUseCompleted(toolItemId);
                SetFarmAreaMode(false);
                ShowSeedPanel();
                return;
            }

            RequirementToast.TryPass(FarmRequirementChecker.NoBuildableCells());
        }

        private void StopCultivateAction(ActionStopReason reason)
        {
            if (actions.CurrentActionId == ActionId.CultivateFarm)
            {
                actions.Stop(
                    reason,
                    navigation.IsMoving ? ActionExitMode.ToMove : ActionExitMode.ToIdle);
            }
        }

        private bool TryBuildSelectedBuilding(Vector3Int coord)
        {
            if (selectedBuildingId <= 0 || !WorldBuildingManager.Instance.TryBuild(selectedBuildingId, coord))
            {
                return false;
            }

            ClearSelectedBuilding();
            WorldMainPanel.Instance?.RefreshNow();
            return true;
        }

        private void UpdateBuildingPreview()
        {
            if (selectedBuildingId <= 0 || WorldPointerPicker.IsPointerOverUi() || !TryPickTileCoord(out Vector3Int coord))
            {
                buildingPreview.Hide();
                return;
            }

            buildingPreview.Show(selectedBuildingId, coord);
        }

        private void UpdateFarmAreaPreview()
        {
            if (!farmAreaMode || selectedBuildingId > 0 || WorldPointerPicker.IsPointerOverUi() || !HasHouse() ||
                !TryPickTileCoord(out Vector3Int currentCoord))
            {
                farmAreaPreview.Hide();
                return;
            }

            Vector3Int startCoord = currentCoord;
            if (pointerActive && GameInputManager.Instance.WorldSelectHeld && !pressOverUi && pressHasTile)
            {
                startCoord = pressCoord;
            }

            farmAreaPreview.Show(startCoord, currentCoord);
        }

        private bool TrySelectBuildingAt(Vector3Int coord)
        {
            if (!TryGetBuildingAt(coord, out WorldBuilding building))
            {
                return false;
            }

            selectedBuilding = building;
            return true;
        }

        private static bool TryGetBuildingAt(Vector3Int coord, out WorldBuilding building)
        {
            building = null;
            if (!MapManager.Instance.TryGetMapObjectsAt(coord, out IReadOnlyList<MapObjectData> objects) || objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject == null || mapObject.ObjectType != MapObjectType.Building)
                {
                    continue;
                }

                if (WorldBuildingManager.Instance.TryGetBuilding(mapObject.ObjectId, out WorldBuilding foundBuilding) &&
                    foundBuilding != null)
                {
                    building = foundBuilding;
                    return true;
                }
            }

            return false;
        }

        private bool TryPickTileCoord(out Vector3Int coord)
        {
            camera.Ensure();
            return WorldPointerPicker.TryPickTileCoord(
                GameInputManager.Instance.PointerPosition,
                camera.MainCamera,
                out coord,
                false);
        }

        private static bool HasHouse()
        {
            return WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.House);
        }

        private void ShowSeedPanel()
        {
            WorldMainPanel.Instance?.ShowFarmPanel(selectedFarm);
        }

        private static void HideSeedPanel()
        {
            WorldMainPanel.Instance?.HideFarmPanel();
        }

        private void ClearSelectedObject()
        {
            selectedFarm = null;
            selectedBuilding = null;
        }

        private void SetFarmAreaMode(bool enabled)
        {
            if (farmAreaMode == enabled)
            {
                return;
            }

            farmAreaMode = enabled;
            if (!farmAreaMode)
            {
                farmAreaPreview.Hide();
            }
        }

        private void ResetPointer()
        {
            pointerActive = false;
            pressOverUi = false;
            pressHasTile = false;
            pressCoord = default;
            pressScreenPosition = default;
        }
    }
}
