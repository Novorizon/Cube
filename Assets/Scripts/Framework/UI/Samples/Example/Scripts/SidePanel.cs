using UnityEngine;
using UnityEngine.UI;

namespace UI.Sample
{
    public sealed class SidePanel : UIPanel
    {
        [SerializeField] Button? closeButton;

        protected override void OnCreate()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
        }
    }
}
