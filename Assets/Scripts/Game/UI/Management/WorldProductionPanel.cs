using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal sealed class WorldProductionPanel
    {
        private readonly StringBuilder builder = new StringBuilder(512);
        private GameObject root;
        private TMP_Text contentText;
        private ProductionStatGroup activeGroup = ProductionStatGroup.Overview;

        public GameObject Root => root;

        public bool Bind(Transform rootTransform)
        {
            root = rootTransform != null ? rootTransform.gameObject : null;
            if (rootTransform == null)
            {
                contentText = null;
                return false;
            }

            contentText =
                FindText(rootTransform, "Content") ??
                FindText(rootTransform, "ModeHint") ??
                FindText(rootTransform, "Status") ??
                FindText(rootTransform, "Text") ??
                rootTransform.GetComponentInChildren<TMP_Text>(true);

            BindTab(rootTransform, "Tab_Overview", ProductionStatGroup.Overview);
            BindTab(rootTransform, "Tab_Crops", ProductionStatGroup.Crops);
            BindTab(rootTransform, "Tab_Ores", ProductionStatGroup.Ores);
            BindTab(rootTransform, "Tab_Basic", ProductionStatGroup.BasicResources);
            BindTab(rootTransform, "Tab_Buildings", ProductionStatGroup.Buildings);
            WorldPanelBindingUtility.BindButton(rootTransform.Find("Close"), () => root.SetActive(false), "Production close");
            Refresh();
            return contentText != null;
        }

        public void Refresh()
        {
            if (contentText == null)
            {
                return;
            }

            List<ProductionStat> stats = ProductionStatsProvider.Instance.GetStats(activeGroup);
            builder.Clear();
            builder.AppendLine(GetGroupTitle(activeGroup));
            builder.AppendLine(LocalizationManager.Get("ui.production.header"));

            if (stats.Count == 0)
            {
                builder.AppendLine(LocalizationManager.Get("ui.production.no_data"));
            }
            else
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    ProductionStat stat = stats[i];
                    builder.AppendLine($"{stat.Name}   {stat.Count}   {FormatPerMinute(stat.PerMinute)}");
                }
            }

            contentText.text = builder.ToString();
        }

        private void BindTab(Transform rootTransform, string path, ProductionStatGroup group)
        {
            Transform tab = rootTransform.Find(path);
            WorldPanelBindingUtility.BindButton(tab, () =>
            {
                activeGroup = group;
                Refresh();
            }, path);
        }

        private static string GetGroupTitle(ProductionStatGroup group)
        {
            switch (group)
            {
                case ProductionStatGroup.Crops:
                    return LocalizationManager.Get("ui.production.group.crops");
                case ProductionStatGroup.Ores:
                    return LocalizationManager.Get("ui.production.group.ores");
                case ProductionStatGroup.BasicResources:
                    return LocalizationManager.Get("ui.production.group.basic");
                case ProductionStatGroup.Buildings:
                    return LocalizationManager.Get("ui.production.group.buildings");
                default:
                    return LocalizationManager.Get("ui.production.group.overview");
            }
        }

        private static string FormatPerMinute(float value)
        {
            if (value <= 0.001f)
            {
                return "0";
            }

            return Math.Abs(value - Mathf.Round(value)) < 0.01f
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.0");
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChild(root.GetChild(i), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
