using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldRightBarPanel : MonoBehaviour
    {
        [SerializeField] private Button productionButton;
        [SerializeField] private Button toolKitButton;
        [SerializeField] private Button farmButton;
        [SerializeField] private Button techButton;
        [SerializeField] private Button battleButton;

        public void Initialize(
            Action productionClicked,
            Action toolKitClicked,
            Action farmClicked,
            Action techClicked,
            Action battleClicked)
        {
            BindButton(productionButton, productionClicked);
            BindButton(toolKitButton, toolKitClicked);
            BindButton(farmButton, farmClicked);
            BindButton(techButton, techClicked);
            BindButton(battleButton, battleClicked);
        }

        private static void BindButton(Button button, Action clicked)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
        }
    }
}
