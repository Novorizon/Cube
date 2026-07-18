using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldBuildingDetailPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private TMP_Text blueprintText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button craftButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button closeButton;

        private WorldBuilding selectedBuilding;
        private BlueprintConfig selectedBlueprint;
        private Action closeClicked;
        private Action changed;

        public GameObject Root => gameObject;

        public void Initialize(Action onCloseClicked, Action onChanged)
        {
            closeClicked = onCloseClicked;
            changed = onChanged;

            BindButton(upgradeButton, TryUpgrade);
            BindButton(craftButton, TryCraft);
            BindButton(removeButton, TryRemove);
            BindButton(closeButton, () => closeClicked?.Invoke());
        }

        public void Show(WorldBuilding building)
        {
            selectedBuilding = building;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            selectedBuilding = null;
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (selectedBuilding == null)
            {
                SetText(infoText, LocalizationManager.Get("ui.building_detail.none"));
                SetText(blueprintText, LocalizationManager.GetOrFallback("ui.blueprint.none", "No blueprint"));
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

            selectedBlueprint = GetDisplayBlueprint(selectedBuilding.ConfigId);
            if (selectedBlueprint != null)
            {
                SetText(blueprintText, BlueprintManager.Instance.FormatBlueprint(selectedBlueprint));
            }
            else
            {
                SetText(blueprintText, LocalizationManager.GetOrFallback("ui.building_detail.no_blueprint_for_building", "No blueprint for this building"));
            }

            SetInteractable(upgradeButton, WorldBuildingManager.Instance.CanUpgrade(selectedBuilding.InstanceId, out _));
            SetInteractable(craftButton, selectedBlueprint != null && BlueprintManager.Instance.CanComplete(selectedBlueprint.Id));
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

            if (selectedBlueprint != null && BlueprintManager.Instance.TryComplete(selectedBlueprint.Id))
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

        private static void BindButton(Button button, UnityEngine.Events.UnityAction clicked)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
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

        private static string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }

        private static BlueprintConfig GetDisplayBlueprint(int buildingId)
        {
            int questBlueprintId = QuestManager.Instance.GetActiveBlueprintObjectiveForBuilding(buildingId);
            if (questBlueprintId > 0)
            {
                BlueprintConfig questBlueprint = BlueprintManager.Instance.Get(questBlueprintId);
                if (questBlueprint != null)
                {
                    return questBlueprint;
                }
            }

            return BlueprintManager.Instance.GetFirstBlueprintForBuilding(buildingId);
        }
    }
}
