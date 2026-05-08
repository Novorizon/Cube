using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public sealed class PopupOptions
    {
        public bool Modal { get; set; } = true;
        public bool SingletonByPath { get; set; } = true;
        public bool CacheOnClose { get; set; } = true;
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
            UIBlocker createdBlocker = null;
            bool allowMultiple = !opt.SingletonByPath;
            bool cacheOnClose = allowMultiple ? false : opt.CacheOnClose;

            UIHandle handle = await factory.OpenAsync(UIKind.Popup, UILayer.Popup, prefabPath, args, allowMultiple, cacheOnClose, parent =>
            {
                if (!opt.Modal)
                {
                    return null;
                }

                createdBlocker = UIBlocker.Create(parent);
                return createdBlocker;
            });

            if (!handle.IsValid)
            {
                if (createdBlocker != null)
                {
                    Object.Destroy(createdBlocker.gameObject);
                }

                return default;
            }

            if (handle.View is UIPopup popup && createdBlocker != null)
            {
                UIHandle popupHandle = handle;
                createdBlocker.Clicked += () =>
                {
                    if (popup.CloseOnBlockerClick)
                    {
                        Close(popupHandle);
                    }
                };
            }

            if (IndexOf(handle) < 0)
            {
                opened.Add((handle, createdBlocker));
            }

            return handle;
        }

        public bool CloseTop()
        {
            UIHandle? top = Peek();
            if (!top.HasValue)
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
    }
}
