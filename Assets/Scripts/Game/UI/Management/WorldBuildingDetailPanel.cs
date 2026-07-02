using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal sealed class WorldBuildingDetailPanel
    {
        private GameObject root;
        private TMP_Text infoText;
        private TMP_Text recipeText;
        private Button upgradeButton;
        private Button craftButton;
        private Button removeButton;
        private WorldBuilding selectedBuilding;
        private Action closeClicked;
        private Action changed;

        public GameObject Root => root;

        public bool Bind(Transform rootTransform, Action onCloseClicked, Action onChanged)
        {
            root = rootTransform != null ? rootTransform.gameObject : null;
            closeClicked = onCloseClicked;
            changed = onChanged;

            if (rootTransform == null)
            {
                Clear();
                return false;
            }

            infoText = WorldPanelBindingUtility.FindText(rootTransform, "Info");
            recipeText = WorldPanelBindingUtility.FindText(rootTransform, "RecipeInfo");
            upgradeButton = BindButton(rootTransform.Find("Upgrade"), TryUpgrade, "Building upgrade");
            craftButton = BindButton(rootTransform.Find("Craft"), TryCraft, "Building craft");
            removeButton = BindButton(rootTransform.Find("Remove"), TryRemove, "Building remove");
            WorldPanelBindingUtility.BindButton(rootTransform.Find("Close"), () => closeClicked?.Invoke(), "Building close");
            return infoText != null;
        }

        public void Show(WorldBuilding building)
        {
            selectedBuilding = building;
            if (root != null)
            {
                root.SetActive(true);
            }

            Refresh();
        }

        public void Hide()
        {
            selectedBuilding = null;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Refresh()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            if (selectedBuilding == null)
            {
                SetText(infoText, LocalizationManager.Get("ui.building_detail.none"));
                SetText(recipeText, LocalizationManager.Get("ui.recipe.none"));
                SetInteractable(upgradeButton, false);
                SetInteractable(craftButton, false);
                SetInteractable(removeButton, false);
                return;
            }

            string buildingName = GetBuildingName(selectedBuilding.ConfigId);
            string status = selectedBuilding.Status == WorldBuildingStatus.Constructing
                ? LocalizationManager.Get("ui.build.status.constructing")
                : LocalizationManager.Get("ui.build.status.active");
            string upgradeState = WorldBuildingManager.Instance.CanUpgrade(selectedBuilding.InstanceId, out string reason)
                ? LocalizationManager.Get("ui.common.ready")
                : reason;

            SetText(
                infoText,
                LocalizationManager.Format(
                    "ui.building_detail.info",
                    buildingName,
                    selectedBuilding.Level,
                    status,
                    selectedBuilding.Coord.x,
                    selectedBuilding.Coord.y,
                    selectedBuilding.Coord.z,
                    upgradeState));

            WorldRecipeConfig recipe = WorldRecipeManager.Instance.GetFirstRecipeForBuilding(selectedBuilding.ConfigId);
            if (recipe != null)
            {
                SetText(recipeText, WorldRecipeManager.Instance.FormatRecipe(recipe));
            }
            else
            {
                SetText(recipeText, LocalizationManager.Get("ui.building_detail.no_recipe_for_building"));
            }

            SetInteractable(upgradeButton, WorldBuildingManager.Instance.CanUpgrade(selectedBuilding.InstanceId, out _));
            SetInteractable(craftButton, recipe != null && WorldRecipeManager.Instance.CanCraft(recipe.Id));
            SetInteractable(removeButton, !WorldBuildingManager.Instance.IsBuildingType(selectedBuilding, WorldBuildingType.House));
        }

        private void TryUpgrade()
        {
            if (selectedBuilding == null)
            {
                return;
            }

            if (WorldBuildingManager.Instance.TryUpgrade(selectedBuilding.InstanceId))
            {
                changed?.Invoke();
            }

            Refresh();
        }

        private void TryCraft()
        {
            if (selectedBuilding == null)
            {
                return;
            }

            if (WorldRecipeManager.Instance.TryCraftFirstForBuilding(selectedBuilding.ConfigId))
            {
                changed?.Invoke();
            }

            Refresh();
        }

        private void TryRemove()
        {
            if (selectedBuilding == null)
            {
                return;
            }

            int instanceId = selectedBuilding.InstanceId;
            if (WorldBuildingManager.Instance.TryRemove(instanceId))
            {
                Hide();
                changed?.Invoke();
            }
        }

        private static Button BindButton(Transform transform, UnityEngine.Events.UnityAction clicked, string label)
        {
            WorldPanelBindingUtility.BindButton(transform, clicked, label);
            return transform != null ? transform.GetComponent<Button>() : null;
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private void Clear()
        {
            infoText = null;
            recipeText = null;
            upgradeButton = null;
            craftButton = null;
            removeButton = null;
            selectedBuilding = null;
        }

        private static string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }
    }
}
