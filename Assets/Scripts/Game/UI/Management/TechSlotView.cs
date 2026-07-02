using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TechSlotView : MonoBehaviour
    {
        private static readonly HashSet<string> MissingIconWarnings = new HashSet<string>();

        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconLabel;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private TMP_Text lockRequirementText;
        [SerializeField] private GameObject selectedObject;
        [SerializeField] private GameObject lockOverlay;

        private TechNodeConfig config;
        private Action<int> clicked;

        public void Bind(TechNodeConfig nodeConfig, Action<int> onClicked)
        {
            config = nodeConfig;
            clicked = onClicked;
            BindButton();
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        private void BindButton()
        {
            if (button == null)
            {
                Debug.LogError($"[TechSlotView] Missing Button reference on {name}.");
                return;
            }

            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        private void Refresh()
        {
            if (config == null)
            {
                return;
            }

            TechResearchState state = TechManager.Instance.GetResearchState(config, out _);
            bool researched = state == TechResearchState.Researched;
            bool canResearch = state == TechResearchState.CanResearch;
            string stateText = GetStateText(state);

            SetBackground(researched, canResearch);
            SetText(nameText, LocalizedConfigText.TechName(config.Id));
            SetText(costText, researched ? LocalizationManager.Get("ui.tech.state.unlocked") : GetCostText(config.CostGroupId));
            SetText(requirementText, stateText);
            SetText(lockText, LocalizationManager.Get("ui.tech.state.locked"));
            SetText(lockRequirementText, stateText);
            SetActive(selectedObject, false);
            SetActive(lockOverlay, !researched && !canResearch);
            RefreshIcon();

            if (button != null)
            {
                button.interactable = !researched;
            }
        }

        private void OnClick()
        {
            if (config != null)
            {
                clicked?.Invoke(config.Id);
            }
        }

        private void SetBackground(bool researched, bool canResearch)
        {
            if (background == null)
            {
                return;
            }

            background.color = researched
                ? new Color(0.92f, 0.98f, 0.86f, 0.98f)
                : canResearch
                    ? new Color(1.00f, 0.92f, 0.70f, 0.98f)
                    : new Color(0.64f, 0.58f, 0.48f, 0.92f);
        }

        private string GetCostText(int costGroupId)
        {
            if (costGroupId <= 0)
            {
                return LocalizationManager.Get("ui.common.free");
            }

            IReadOnlyList<WorldItem> costs = costResolver.GetCostGroup(costGroupId);
            if (costs == null || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.tech.cost.missing");
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                parts.Add($"{GetItemName(cost.ItemId)} {cost.Count}");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : LocalizationManager.Get("ui.common.free");
        }

        private void RefreshIcon()
        {
            Sprite sprite = LoadIcon(config);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(0.86f, 0.68f, 0.38f, 0.94f);
                icon.preserveAspect = true;
            }

            if (iconLabel != null)
            {
                iconLabel.gameObject.SetActive(sprite == null);
                iconLabel.text = GetIconLabel(LocalizedConfigText.TechName(config.Id));
            }
        }

        private static Sprite LoadIcon(TechNodeConfig nodeConfig)
        {
            if (nodeConfig == null || string.IsNullOrWhiteSpace(nodeConfig.IconLocation))
            {
                return null;
            }

            if (!nodeConfig.IconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (MissingIconWarnings.Add(nodeConfig.IconLocation))
                {
                    Debug.LogWarning($"[TechSlotView] Tech icon location must be a full asset path. location: {nodeConfig.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(nodeConfig.IconLocation);
            if (sprite == null && MissingIconWarnings.Add(nodeConfig.IconLocation))
            {
                Debug.LogWarning($"[TechSlotView] Tech icon load failed. location: {nodeConfig.IconLocation}");
            }

            return sprite;
        }

        private static string GetStateText(TechResearchState state)
        {
            switch (state)
            {
                case TechResearchState.Researched:
                    return LocalizationManager.Get("ui.tech.state.unlocked");
                case TechResearchState.CanResearch:
                    return LocalizationManager.Get("ui.tech.state.can_research");
                case TechResearchState.LockedByPrerequisite:
                    return LocalizationManager.Get("ui.tech.state.prerequisite_locked");
                case TechResearchState.NotEnoughCost:
                    return LocalizationManager.Get("ui.tech.state.not_enough_cost");
                case TechResearchState.MissingCostConfig:
                    return LocalizationManager.Get("ui.tech.cost.missing");
                default:
                    return LocalizationManager.Get("ui.tech.state.locked");
            }
        }

        private static string GetIconLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return LocalizationManager.Get("ui.tech.icon_fallback");
            }

            return name.Length <= 2 ? name : name.Substring(0, 2);
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
