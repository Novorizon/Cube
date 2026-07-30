using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public sealed class TooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IBeginDragHandler
    {
        [SerializeField]
        private float delaySeconds = -1f;

        [SerializeField]
        private TooltipPlacement placement = TooltipPlacement.Auto;

        [SerializeField]
        private string title;

        [SerializeField, TextArea]
        private string description;

        [SerializeField]
        private Sprite icon;

        private Func<TooltipData> dataProvider;

        public void Bind(Func<TooltipData> provider)
        {
            dataProvider = provider;
        }

        public void ClearBinding()
        {
            Hide();
            dataProvider = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerId >= 0)
            {
                return;
            }

            UIManager.Instance.Tooltips.Show(
                this,
                transform as RectTransform,
                ResolveData,
                new TooltipOptions
                {
                    DelaySeconds = delaySeconds,
                    Placement = placement,
                });
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Hide();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private TooltipData ResolveData()
        {
            return dataProvider?.Invoke() ?? new TooltipData
            {
                Title = title,
                Description = description,
                Icon = icon,
            };
        }

        private void Hide()
        {
            UIManager.Current?.Tooltips?.Hide(this);
        }
    }
}
