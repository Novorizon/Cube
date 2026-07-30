using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldProductionPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Production/ProductionPanel.prefab";
        private const string CountColumnPosition = "<pos=210>";
        private const string PerMinuteColumnPosition = "<pos=290>";
        private static readonly char[] HeaderSeparators = { ' ', '\t', '\r', '\n' };

        private readonly StringBuilder builder = new StringBuilder(512);
        private readonly Dictionary<ProductionStatGroup, Graphic> tabGraphics =
            new Dictionary<ProductionStatGroup, Graphic>();

        [SerializeField] private Color selectedTabColor = new Color(0.75f, 0.58f, 0.24f, 0.96f);
        [SerializeField] private Color normalTabColor = new Color(0.96f, 0.84f, 0.62f, 0.96f);

        private GameObject root;
        private TMP_Text contentText;
        private ProductionStatGroup activeGroup = ProductionStatGroup.Overview;
        private float nextRefreshTime;

        public GameObject Root => root;
        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BindStaticLayout();
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            RefreshNow();
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh();
        }

        private bool BindStaticLayout()
        {
            Transform rootTransform = transform;
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

            if (contentText != null)
            {
                contentText.richText = true;
                contentText.enableWordWrapping = false;
                contentText.overflowMode = TextOverflowModes.Truncate;
                contentText.lineSpacing = 6f;
            }

            tabGraphics.Clear();
            BindTab(rootTransform, "Tab_Overview", ProductionStatGroup.Overview);
            BindTab(rootTransform, "Tab_Crops", ProductionStatGroup.Crops);
            BindTab(rootTransform, "Tab_Ores", ProductionStatGroup.Ores);
            BindTab(rootTransform, "Tab_Basic", ProductionStatGroup.BasicResources);
            BindTab(rootTransform, "Tab_Buildings", ProductionStatGroup.Buildings);
            WorldPanelBindingUtility.BindButton(rootTransform.Find("Close"), CloseSelf, "Production close");
            RefreshTabVisuals();
            Refresh();
            return contentText != null;
        }

        private void RefreshNow()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        public void Refresh()
        {
            if (contentText == null)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.5f;
            List<ProductionStat> stats = ProductionStatsProvider.Instance.GetStats(activeGroup);
            builder.Clear();
            AppendHeader();

            if (stats.Count == 0)
            {
                builder.Append("<color=#8F7953>");
                builder.Append(EscapeRichText(LocalizationManager.Get("ui.production.no_data")));
                builder.AppendLine("</color>");
            }
            else
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    ProductionStat stat = stats[i];
                    AppendDataRow(stat);
                }
            }

            contentText.text = builder.ToString();
        }

        private void AppendHeader()
        {
            string localizedHeader = LocalizationManager.Get("ui.production.header");
            string[] columns = localizedHeader.Split(
                HeaderSeparators,
                StringSplitOptions.RemoveEmptyEntries);
            string nameHeader = columns.Length > 0 ? columns[0] : localizedHeader;
            string countHeader = columns.Length > 1 ? columns[1] : string.Empty;
            string perMinuteHeader = columns.Length > 2 ? columns[2] : string.Empty;

            builder.Append("<b><color=#6A4C19>");
            AppendColumns(nameHeader, countHeader, perMinuteHeader);
            builder.AppendLine("</color></b>");
            builder.AppendLine("<color=#C5A667>────────────────────────</color>");
        }

        private void AppendDataRow(ProductionStat stat)
        {
            builder.Append("<color=#4A3822>");
            AppendColumns(
                stat.Name,
                stat.Count.ToString(),
                FormatPerMinute(stat.PerMinute));
            builder.AppendLine("</color>");
        }

        private void AppendColumns(string name, string count, string perMinute)
        {
            builder.Append(EscapeRichText(name));
            builder.Append(CountColumnPosition);
            builder.Append(EscapeRichText(count));
            builder.Append(PerMinuteColumnPosition);
            builder.Append(EscapeRichText(perMinute));
        }

        private void BindTab(Transform rootTransform, string path, ProductionStatGroup group)
        {
            Transform tab = rootTransform.Find(path);
            Button button = tab != null ? tab.GetComponent<Button>() : null;
            if (button != null && button.targetGraphic != null)
            {
                tabGraphics[group] = button.targetGraphic;
            }

            WorldPanelBindingUtility.BindButton(tab, () =>
            {
                activeGroup = group;
                RefreshTabVisuals();
                Refresh();
            }, path);
        }

        private void RefreshTabVisuals()
        {
            foreach (KeyValuePair<ProductionStatGroup, Graphic> pair in tabGraphics)
            {
                if (pair.Value != null)
                {
                    pair.Value.color = pair.Key == activeGroup ? selectedTabColor : normalTabColor;
                }
            }
        }

        private void CloseSelf()
        {
            if (CanCloseBy(UICloseReason.CloseButton))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
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
