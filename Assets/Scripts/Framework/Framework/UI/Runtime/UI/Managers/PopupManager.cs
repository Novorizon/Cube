using System.Threading.Tasks;

namespace UI
{
    public sealed class PopupOptions
    {
        public bool Modal { get; set; } = true;

        public bool SingletonByPath { get; set; } = false;

        public bool CacheOnClose { get; set; } = true;
    }

    public sealed class PopupManager
    {
        readonly UIInstanceFactory factory;
        readonly System.Collections.Generic.List<(UIHandle handle, UIBlocker? blocker)> opened = new();

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

        public async Task<UIHandle> OpenAsync(string prefabPath, object? args = null, PopupOptions? options = null)
        {
            PopupOptions opt = options ?? new PopupOptions();

            UIBlocker? blocker = null;

            UIHandle handle = await factory.OpenAsync(
                UIKind.Popup,
                UILayer.Popup,
                prefabPath,
                args,
                allowMultiple: !opt.SingletonByPath,
                cacheOnClose: opt.CacheOnClose,
                blockerFactory: parent =>
                {
                    if (!opt.Modal)
                    {
                        return null;
                    }

                    blocker = UIBlocker.Create(parent);
                    return blocker;
                });

            if (handle.View is UIPopup popup && blocker != null)
            {
                blocker.Clicked += () =>
                {
                    if (popup.CloseOnBlockerClick)
                    {
                        Close(handle);
                    }
                };
            }

            opened.Add((handle, blocker));
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

            UIBlocker? blocker = opened[index].blocker;
            opened.RemoveAt(index);

            if (blocker != null)
            {
                UnityEngine.Object.Destroy(blocker.gameObject);
            }

            factory.Close(handle, false, true);
        }

        int IndexOf(UIHandle handle)
        {
            for (int i = opened.Count - 1; i >= 0; i--)
            {
                if (opened[i].handle.Id == handle.Id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
