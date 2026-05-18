using TMPro;
using UnityEngine;

namespace UI.Sample
{
    public sealed class SimpleToast : UIToast
    {
        [SerializeField] TMP_Text? text;

        [Header("Colors")]
        [SerializeField] Color infoColor = Color.white;
        [SerializeField] Color warningColor = new Color(1f, 0.75f, 0.2f);
        [SerializeField] Color errorColor = new Color(1f, 0.25f, 0.25f);

        public override float Duration => 1.5f;

        protected override void OnOpen(object? args)
        {
            if (text != null)
            {
                ApplyArgs(args);
            }

            base.OnOpen(args);
        }

        void ApplyArgs(object? args)
        {
            if (text == null)
            {
                return;
            }

            if (args is ToastArgs toastArgs)
            {
                text.text = toastArgs.Message;
                text.color = GetColor(toastArgs.Level);
                return;
            }

            text.text = args as string ?? string.Empty;
            text.color = infoColor;
        }

        Color GetColor(ToastLevel level)
        {
            switch (level)
            {
                case ToastLevel.Warning:
                    return warningColor;

                case ToastLevel.Error:
                    return errorColor;

                case ToastLevel.Info:
                default:
                    return infoColor;
            }
        }
    }
}