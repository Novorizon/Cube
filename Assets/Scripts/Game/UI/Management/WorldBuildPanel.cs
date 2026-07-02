using System;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal sealed class WorldBuildPanel
    {
        private readonly Dictionary<WorldBuildCategory, Transform> tabRoots = new Dictionary<WorldBuildCategory, Transform>();

        public GameObject Root { get; private set; }
        public Transform ButtonContainer { get; private set; }
        public WorldBuildCategory CurrentCategory { get; private set; } = WorldBuildCategory.All;
        private bool hasHouse = true;

        public bool Bind(
            Transform root,
            Action closeClicked,
            Action categoryChanged)
        {
            Root = root != null ? root.gameObject : null;
            if (root == null)
            {
                Clear();
                return false;
            }

            ButtonContainer = FindBuildButtonContainer(root);

            BindTabs(root, categoryChanged);
            WorldPanelBindingUtility.BindButton(root.Find("Close"), () => closeClicked?.Invoke(), "Build panel close");

            return ButtonContainer != null;
        }

        public void RefreshTabs(bool hasHouseBuilt)
        {
            hasHouse = hasHouseBuilt;
            if (!hasHouse && IsLockedBeforeHouse(CurrentCategory))
            {
                CurrentCategory = WorldBuildCategory.Building;
            }

            RefreshTabs();
        }

        public void RefreshTabs()
        {
            foreach (KeyValuePair<WorldBuildCategory, Transform> pair in tabRoots)
            {
                bool selected = pair.Key == CurrentCategory;
                bool locked = IsTabLocked(pair.Key);
                Transform tab = pair.Value;
                if (tab == null)
                {
                    continue;
                }

                Transform selectedTransform = tab.Find("Selected");
                if (selectedTransform != null)
                {
                    selectedTransform.gameObject.SetActive(selected);
                }

                Image image = tab.GetComponent<Image>();
                if (image != null)
                {
                    image.color = selected
                        ? new Color(0.96f, 0.74f, 0.25f, 0.96f)
                        : locked
                            ? new Color(0.70f, 0.66f, 0.58f, 0.82f)
                            : new Color(0.98f, 0.91f, 0.78f, 0.92f);
                }

                Button button = tab.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !locked;
                }

                SetLockMarker(tab, locked);
            }
        }

        private void BindTabs(Transform root, Action categoryChanged)
        {
            tabRoots.Clear();
            Transform tabBar = root.Find("TabBar");
            if (tabBar == null)
            {
                CurrentCategory = WorldBuildCategory.All;
                return;
            }

            TryBindTab(tabBar, WorldBuildCategory.Building, categoryChanged, "Tab_Building", "Building");
            TryBindTab(tabBar, WorldBuildCategory.Production, categoryChanged, "Tab_Production", "Production");
            TryBindTab(tabBar, WorldBuildCategory.Resource, categoryChanged, "Tab_Resource", "Resource");
            TryBindTab(tabBar, WorldBuildCategory.Farm, categoryChanged, "Tab_Farm", "Farm");
            TryBindTab(tabBar, WorldBuildCategory.Decoration, categoryChanged, "Tab_Decoration", "Decoration");
            TryBindTab(tabBar, WorldBuildCategory.Special, categoryChanged, "Tab_Special", "Special");

            if (tabRoots.Count == 0)
            {
                CurrentCategory = WorldBuildCategory.All;
            }
            else if (CurrentCategory == WorldBuildCategory.All || !tabRoots.ContainsKey(CurrentCategory))
            {
                CurrentCategory = GetFirstTabCategory();
            }

            RefreshTabs();
        }

        private WorldBuildCategory GetFirstTabCategory()
        {
            if (tabRoots.ContainsKey(WorldBuildCategory.Building))
            {
                return WorldBuildCategory.Building;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Production))
            {
                return WorldBuildCategory.Production;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Resource))
            {
                return WorldBuildCategory.Resource;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Farm))
            {
                return WorldBuildCategory.Farm;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Decoration))
            {
                return WorldBuildCategory.Decoration;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Special))
            {
                return WorldBuildCategory.Special;
            }

            return WorldBuildCategory.All;
        }

        private void TryBindTab(Transform tabBar, WorldBuildCategory category, Action categoryChanged, params string[] names)
        {
            Transform tab = WorldPanelBindingUtility.FindFirst(tabBar, names);
            if (tab == null)
            {
                return;
            }

            tabRoots[category] = tab;
            WorldBuildCategory capturedCategory = category;
            WorldPanelBindingUtility.BindButton(tab, () =>
            {
                if (IsTabLocked(capturedCategory))
                {
                    Toast.Warning(LocalizationManager.Get("ui.build.require_house"));
                    return;
                }

                if (CurrentCategory == capturedCategory)
                {
                    return;
                }

                CurrentCategory = capturedCategory;
                RefreshTabs();
                categoryChanged?.Invoke();
            }, $"{category} build tab");
        }

        private bool IsTabLocked(WorldBuildCategory category)
        {
            return !hasHouse && IsLockedBeforeHouse(category);
        }

        private static bool IsLockedBeforeHouse(WorldBuildCategory category)
        {
            return category != WorldBuildCategory.All && category != WorldBuildCategory.Building;
        }

        private static void SetLockMarker(Transform tab, bool locked)
        {
            if (tab == null)
            {
                return;
            }

            Transform marker =
                tab.Find("Lock") ??
                tab.Find("LockIcon") ??
                tab.Find("Locked") ??
                tab.Find("LockOverlay");
            if (marker != null)
            {
                marker.gameObject.SetActive(locked);
            }
        }

        private static Transform FindBuildButtonContainer(Transform root)
        {
            Transform scrollView = root != null ? root.Find("ScrollView") : null;
            if (scrollView == null)
            {
                return null;
            }

            Transform viewport = scrollView.Find("Viewport");
            Transform content = viewport != null ? viewport.Find("Content") : null;
            if (content == null)
            {
                return null;
            }

            return content;
        }

        private void Clear()
        {
            ButtonContainer = null;
            tabRoots.Clear();
            CurrentCategory = WorldBuildCategory.All;
        }
    }
}
