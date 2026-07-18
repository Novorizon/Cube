using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldEntryBarPanel : MonoBehaviour
    {
        [SerializeField] private Button questButton;

        public void Initialize(Action questClicked)
        {
            if (questButton == null)
            {
                return;
            }

            questButton.onClick.RemoveAllListeners();
            questButton.onClick.AddListener(() => questClicked?.Invoke());
        }
    }
}
