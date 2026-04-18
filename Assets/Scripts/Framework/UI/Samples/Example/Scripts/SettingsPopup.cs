using UnityEngine;
using UnityEngine.UI;

namespace UI.Sample
{
    public sealed class SettingsPopup : UIPopup
    {
        [SerializeField] Button? closeButton;

        public override bool CloseOnBlockerClick => true;

        protected override void OnCreate()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSelf);
            }
        }

        void CloseSelf()
        {
            gameObject.SetActive(false);
        }
    }
}
