using System.Collections.Generic;
using System.Threading.Tasks;

namespace UI
{
    public sealed class PanelOptions
    {
        public bool AllowMultiple { get; set; } = false;

        public bool CacheOnClose { get; set; } = true;

        public string? GroupId { get; set; }
    }

    public sealed class PanelManager
    {
        readonly UIInstanceFactory factory;

        readonly Dictionary<string, UIHandle> activePanelsByPath = new();
        readonly Dictionary<string, Stack<UIHandle>> groupStacks = new();

        public PanelManager(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public bool IsShown(string prefabPath)
        {
            return activePanelsByPath.ContainsKey(prefabPath);
        }

        public async Task<UIHandle> ShowAsync(string prefabPath, object? args = null, PanelOptions? options = null)
        {
            PanelOptions opt = options ?? new PanelOptions();

            if (opt.GroupId != null)
            {
                return await PushInGroupAsync(opt.GroupId, prefabPath, args, opt);
            }

            if (!opt.AllowMultiple && activePanelsByPath.TryGetValue(prefabPath, out UIHandle existing) && existing.View != null)
            {
                existing.View.gameObject.SetActive(true);
                existing.View.InternalOnOpen(args);
                return existing;
            }

            UIHandle handle = await factory.OpenAsync(UIKind.Panel, UILayer.Panel, prefabPath, args, opt.AllowMultiple, opt.CacheOnClose, null);

            if (!opt.AllowMultiple)
            {
                activePanelsByPath[prefabPath] = handle;
            }

            return handle;
        }

        public bool Hide(string prefabPath)
        {
            if (!activePanelsByPath.TryGetValue(prefabPath, out UIHandle handle))
            {
                return false;
            }

            activePanelsByPath.Remove(prefabPath);
            factory.Close(handle, false, true);
            return true;
        }

        public async Task<UIHandle> ToggleAsync(string prefabPath, object? args = null)
        {
            if (IsShown(prefabPath))
            {
                Hide(prefabPath);
                return default;
            }

            return await ShowAsync(prefabPath, args);
        }

        public async Task<UIHandle> PushInGroupAsync(string groupId, string prefabPath, object? args = null, PanelOptions? options = null)
        {
            PanelOptions opt = options ?? new PanelOptions();

            if (!groupStacks.TryGetValue(groupId, out Stack<UIHandle>? stack))
            {
                stack = new Stack<UIHandle>();
                groupStacks.Add(groupId, stack);
            }

            UIHandle? top = stack.Count > 0 ? stack.Peek() : (UIHandle?)null;
            if (top.HasValue && top.Value.View != null)
            {
                top.Value.View.gameObject.SetActive(false);
            }

            UIHandle handle = await factory.OpenAsync(UIKind.Panel, UILayer.Panel, prefabPath, args, allowMultiple: true, cacheOnClose: opt.CacheOnClose, blockerFactory: null);
            stack.Push(handle);
            return handle;
        }

        public bool PopGroup(string groupId)
        {
            if (!groupStacks.TryGetValue(groupId, out Stack<UIHandle>? stack) || stack.Count == 0)
            {
                return false;
            }

            UIHandle top = stack.Pop();
            factory.Close(top, false, true);

            if (stack.Count == 0)
            {
                groupStacks.Remove(groupId);
                return true;
            }

            UIHandle next = stack.Peek();
            if (next.View != null)
            {
                next.View.gameObject.SetActive(true);
            }

            return true;
        }

        public bool HideAnyBackClosablePanel()
        {
            foreach (var kv in activePanelsByPath)
            {
                UIHandle h = kv.Value;
                if (h.View is UIPanel panel && panel.HideOnBack)
                {
                    Hide(kv.Key);
                    return true;
                }
            }

            return false;
        }
    }
}
