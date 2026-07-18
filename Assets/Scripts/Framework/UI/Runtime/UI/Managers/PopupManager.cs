using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public sealed class PopupOptions
    {
        public bool Modal { get; set; } = true;
        public bool SingletonByPath { get; set; } = true;
        public bool CacheOnClose { get; set; } = true;
        public float BlockerAlpha { get; set; } = 0.4f;
    }

    public sealed class PopupManager
    {
        readonly UIInstanceFactory factory;
        readonly List<(UIHandle handle, UIBlocker blocker)> opened = new List<(UIHandle handle, UIBlocker blocker)>();

        public PopupManager(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public int Count => opened.Count;

        public UIHandle? Peek()
        {
            if (opened.Count == 0)
            {
                return null;
            }

            return opened[opened.Count - 1].handle;
        }

        public async Task<UIHandle> OpenAsync(string prefabPath, object args = null, PopupOptions options = null)
        {
            PopupOptions opt = options ?? new PopupOptions();
            bool allowMultiple = !opt.SingletonByPath;
            bool cacheOnClose = allowMultiple ? false : opt.CacheOnClose;

            UIHandle handle = await factory.OpenAsync(UIKind.Popup, UILayer.Popup, prefabPath, args, allowMultiple, cacheOnClose, null);

            if (!handle.IsValid)
            {
                return default;
            }

            UIBlocker createdBlocker = null;
            if (opt.Modal && handle.View is UIPopup popup)
            {
                UIHandle popupHandle = handle;
                createdBlocker = CreateBlocker(handle.View, "ModalBlocker", opt.BlockerAlpha);
                createdBlocker.Clicked += eventData => CloseByOutsideClick(popupHandle, popup, eventData);
            }

            if (IndexOf(handle) < 0)
            {
                opened.Add((handle, createdBlocker));
            }

            return handle;
        }

        public bool CloseTop()
        {
            return CloseTop(UICloseReason.Back);
        }

        public bool CloseTop(UICloseReason reason)
        {
            UIHandle? top = Peek();
            if (!top.HasValue)
            {
                return false;
            }

            if (top.Value.View != null && !top.Value.View.CanCloseBy(reason))
            {
                return false;
            }

            Close(top.Value);
            return true;
        }

        public void Close(UIHandle handle)
        {
            int index = IndexOf(handle);
            if (index < 0)
            {
                return;
            }

            UIBlocker blocker = opened[index].blocker;
            opened.RemoveAt(index);

            if (blocker != null)
            {
                Object.Destroy(blocker.gameObject);
            }

            factory.Close(handle, false, true);
        }

        public void CloseAll(bool destroy = false)
        {
            for (int i = opened.Count - 1; i >= 0; i--)
            {
                UIBlocker blocker = opened[i].blocker;
                if (blocker != null)
                {
                    Object.Destroy(blocker.gameObject);
                }

                factory.Close(opened[i].handle, destroy, !destroy);
            }

            opened.Clear();
        }

        int IndexOf(UIHandle handle)
        {
            for (int i = opened.Count - 1; i >= 0; i--)
            {
                if (opened[i].handle.Id == handle.Id && opened[i].handle.Version == handle.Version)
                {
                    return i;
                }
            }

            return -1;
        }

        private static UIBlocker CreateBlocker(UIView view, string name, float alpha)
        {
            Transform parent = view != null ? view.transform.parent : null;
            if (parent == null)
            {
                return null;
            }

            UIBlocker blocker = UIBlocker.Create(parent, name, alpha);
            blocker.transform.SetSiblingIndex(view.transform.GetSiblingIndex());
            return blocker;
        }

        private void CloseByOutsideClick(UIHandle handle, UIPopup popup, PointerEventData eventData)
        {
            UICloseReason reason = UICloseTriggerUtility.ToOutsideReason(eventData, popup);

            if (popup != null && popup.CanCloseBy(reason))
            {
                Close(handle);
            }
        }
    }
}
