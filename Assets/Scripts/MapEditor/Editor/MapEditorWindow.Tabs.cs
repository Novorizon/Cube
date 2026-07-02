#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor
{
    public partial class MapEditorWindow
    {
        private void DrawMainTabToolbar()
        {
            ApplyFixedEditorMode();
            List<MainTab> tabs = GetSupportedMainTabs();
            int currentIndex = tabs.IndexOf(activeMainTab);
            if (currentIndex < 0)
            {
                currentIndex = 0;
                activeMainTab = tabs[0];
            }

            string[] labels = new string[tabs.Count];
            for (int i = 0; i < tabs.Count; i++)
            {
                labels[i] = GetMainTabLabel(tabs[i]);
            }

            int nextIndex = GUILayout.Toolbar(currentIndex, labels, GUILayout.Height(20f));
            if (nextIndex >= 0 && nextIndex < tabs.Count)
            {
                activeMainTab = tabs[nextIndex];
            }
        }

        private List<MainTab> GetSupportedMainTabs()
        {
            List<MainTab> tabs = new List<MainTab>
            {
                MainTab.Map,
                MainTab.Paint,
            };

            if (SupportsPointsTab)
            {
                tabs.Add(MainTab.Points);
            }

            tabs.Add(MainTab.Decoration);

            if (SupportsResourcesTab)
            {
                tabs.Add(MainTab.Resources);
            }

            return tabs;
        }

        private static string GetMainTabLabel(MainTab tab)
        {
            switch (tab)
            {
                case MainTab.Map:
                    return "Map";
                case MainTab.Paint:
                    return "Paint";
                case MainTab.Points:
                    return "Points";
                case MainTab.Decoration:
                    return "Decoration";
                case MainTab.Resources:
                    return "Resources";
                default:
                    return tab.ToString();
            }
        }

        private void ApplyFixedEditorMode()
        {
            if (UsesFixedValidationMode)
            {
                validationMode = DefaultValidationMode;
            }
        }
    }
}

#endif
