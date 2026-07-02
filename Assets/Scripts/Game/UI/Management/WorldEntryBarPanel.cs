using System;
using UnityEngine;

namespace Game
{
    internal sealed class WorldEntryBarPanel
    {
        public bool Bind(Transform root, Action questClicked)
        {
            if (root == null)
            {
                return false;
            }

            WorldPanelBindingUtility.BindButton(
                WorldPanelBindingUtility.FindFirst(root, "Entry_Quest", "Quest"),
                () => questClicked?.Invoke(),
                "Quest entry");
            return true;
        }
    }
}
