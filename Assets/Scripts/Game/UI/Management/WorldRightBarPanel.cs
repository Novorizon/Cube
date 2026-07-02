using System;
using UnityEngine;

namespace Game
{
    internal sealed class WorldRightBarPanel
    {
        public bool Bind(
            Transform root,
            Action productionClicked,
            Action toolKitClicked,
            Action farmClicked,
            Action techClicked)
        {
            if (root == null)
            {
                return false;
            }

            Transform production = root.Find("Production") ?? root.Find("Status");
            WorldPanelBindingUtility.BindButton(production, () => productionClicked?.Invoke(), "Production entry");
            WorldPanelBindingUtility.BindButton(root.Find("ToolKitEntry"), () => toolKitClicked?.Invoke(), "ToolKit entry");
            WorldPanelBindingUtility.BindButton(root.Find("QuickFarm"), () => farmClicked?.Invoke(), "Farm entry");
            Transform tech = root.Find("Tech") ?? root.Find("TechEntry");
            WorldPanelBindingUtility.BindButton(tech, () => techClicked?.Invoke(), "Tech entry");
            return true;
        }
    }
}
