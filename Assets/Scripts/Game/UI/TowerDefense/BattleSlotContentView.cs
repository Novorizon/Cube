using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattleSlotContentView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;

        public void SetIcon(Sprite icon)
        {
            if (iconImage == null)
            {
                Debug.LogError($"[{nameof(BattleSlotContentView)}] iconImage is not assigned.", this);
                return;
            }

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }
}
