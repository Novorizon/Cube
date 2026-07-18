using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Propagates a page lifecycle to panels embedded in the same prefab.
    /// Embedded panels are explicitly supplied by the page and are not registered
    /// with <see cref="PanelManager"/> as independent windows.
    /// </summary>
    public sealed class UIEmbeddedPanelGroup
    {
        private readonly UIPanel[] panels;
        private readonly bool[] opened;
        private bool created;

        public UIEmbeddedPanelGroup(params UIPanel[] panels)
        {
            this.panels = panels ?? Array.Empty<UIPanel>();
            opened = new bool[this.panels.Length];
            Validate();
        }

        public void Create()
        {
            if (created)
            {
                return;
            }

            created = true;
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i]?.InternalOnCreate();
            }
        }

        public void Open(object args = null)
        {
            Create();

            for (int i = 0; i < panels.Length; i++)
            {
                UIPanel panel = panels[i];
                if (panel == null || !panel.gameObject.activeInHierarchy || opened[i])
                {
                    continue;
                }

                panel.InternalOnOpen(args);
                opened[i] = true;
            }
        }

        public void Close()
        {
            for (int i = panels.Length - 1; i >= 0; i--)
            {
                if (!opened[i])
                {
                    continue;
                }

                UIPanel panel = panels[i];
                if (panel != null)
                {
                    panel.InternalOnClose();
                }

                opened[i] = false;
            }
        }

        private void Validate()
        {
            HashSet<UIPanel> uniquePanels = new HashSet<UIPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                UIPanel panel = panels[i];
                if (panel == null)
                {
                    Debug.LogError($"[UI] Embedded panel at index {i} is not assigned.");
                    continue;
                }

                if (!uniquePanels.Add(panel))
                {
                    Debug.LogError($"[UI] Embedded panel is assigned more than once: {panel.name}", panel);
                }
            }
        }
    }
}
