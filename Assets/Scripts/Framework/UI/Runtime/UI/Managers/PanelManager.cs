using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public sealed class PanelOptions
    {
        public bool AllowMultiple { get; set; } = false;
        public bool CacheOnClose { get; set; } = true;
        public string StackGroupId { get; set; }
        public string GroupId
        {
            get => StackGroupId;
            set => StackGroupId = value;
        }

        public bool UseOutsideClickDetector { get; set; } = true;
    }

    public sealed class PanelManager
    {
        readonly UIInstanceFactory factory;
        readonly Dictionary<string, UIHandle> activePanelsByPath = new Dictionary<string, UIHandle>();
        readonly Dictionary<string, PanelOptions> activePanelOptionsByPath = new Dictionary<string, PanelOptions>();
        readonly Dictionary<string, Stack<PanelStackEntry>> stackGroups = new Dictionary<string, Stack<PanelStackEntry>>();
        readonly Dictionary<string, HashSet<string>> exclusiveGroups = new Dictionary<string, HashSet<string>>();

        public PanelManager(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public bool IsShown(string prefabPath)
        {
            if (activePanelsByPath.TryGetValue(prefabPath, out UIHandle handle) && handle.IsValid)
            {
                return true;
            }

            return IsStackTop(prefabPath);
        }

        public async Task<UIHandle> ShowAsync(string prefabPath, object args = null, PanelOptions options = null)
        {
            PanelOptions opt = options ?? new PanelOptions();

            if (!string.IsNullOrEmpty(opt.StackGroupId))
            {
                return await PushStackAsync(opt.StackGroupId, prefabPath, args, opt);
            }

            if (!opt.AllowMultiple && activePanelsByPath.TryGetValue(prefabPath, out UIHandle existing) && existing.IsValid)
            {
                existing.View.gameObject.SetActive(true);
                existing.View.InternalOnOpen(args);
                activePanelOptionsByPath[prefabPath] = opt;
                return existing;
            }

            bool cacheOnClose = opt.AllowMultiple ? false : opt.CacheOnClose;
            UIHandle handle = await factory.OpenAsync(UIKind.Panel, UILayer.Panel, prefabPath, args, opt.AllowMultiple, cacheOnClose, null);

            if (!opt.AllowMultiple && handle.IsValid)
            {
                activePanelsByPath[prefabPath] = handle;
                activePanelOptionsByPath[prefabPath] = opt;
            }

            return handle;
        }

        public bool Hide(string prefabPath)
        {
            if (!activePanelsByPath.TryGetValue(prefabPath, out UIHandle handle))
            {
                return TryPopStackTop(prefabPath);
            }

            activePanelsByPath.Remove(prefabPath);
            activePanelOptionsByPath.Remove(prefabPath);
            factory.Close(handle, false, true);
            return true;
        }

        public async Task<UIHandle> ToggleAsync(string prefabPath, object args = null)
        {
            if (IsShown(prefabPath))
            {
                Hide(prefabPath);
                return default;
            }

            return await ShowAsync(prefabPath, args);
        }

        public void RegisterExclusivePanel(string groupId, string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(prefabPath))
            {
                return;
            }

            if (!exclusiveGroups.TryGetValue(groupId, out HashSet<string> group))
            {
                group = new HashSet<string>();
                exclusiveGroups.Add(groupId, group);
            }

            group.Add(prefabPath);
        }

        public async Task<UIHandle> ShowExclusiveAsync(string groupId, string prefabPath, object args = null, PanelOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return await ToggleAsync(prefabPath, args);
            }

            RegisterExclusivePanel(groupId, prefabPath);

            if (IsShown(prefabPath))
            {
                Hide(prefabPath);
                return default;
            }

            HideExclusiveGroup(groupId, prefabPath);
            return await ShowAsync(prefabPath, args, options);
        }

        public bool HideExclusiveGroup(string groupId, string exceptPrefabPath = null)
        {
            if (string.IsNullOrWhiteSpace(groupId) || !exclusiveGroups.TryGetValue(groupId, out HashSet<string> group))
            {
                return false;
            }

            bool hiddenAny = false;
            List<string> paths = new List<string>(group);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (string.Equals(path, exceptPrefabPath, System.StringComparison.Ordinal))
                {
                    continue;
                }

                hiddenAny |= Hide(path);
            }

            return hiddenAny;
        }

        public async Task<UIHandle> PushInGroupAsync(string groupId, string prefabPath, object args = null, PanelOptions options = null)
        {
            return await PushStackAsync(groupId, prefabPath, args, options);
        }

        public async Task<UIHandle> PushStackAsync(string groupId, string prefabPath, object args = null, PanelOptions options = null)
        {
            PanelOptions opt = options ?? new PanelOptions();

            if (!stackGroups.TryGetValue(groupId, out Stack<PanelStackEntry> stack))
            {
                stack = new Stack<PanelStackEntry>();
                stackGroups.Add(groupId, stack);
            }

            PanelStackEntry top = stack.Count > 0 ? stack.Peek() : null;
            if (top != null && string.Equals(top.PrefabPath, prefabPath, System.StringComparison.Ordinal))
            {
                top.Options = opt;
                if (top.Handle.View != null)
                {
                    top.Handle.View.gameObject.SetActive(true);
                    top.Handle.View.InternalOnOpen(args);
                }

                return top.Handle;
            }

            if (top != null && top.Handle.View != null)
            {
                top.Handle.View.gameObject.SetActive(false);
            }

            UIHandle handle;
            if (activePanelsByPath.TryGetValue(prefabPath, out UIHandle activeHandle) && activeHandle.IsValid)
            {
                activePanelsByPath.Remove(prefabPath);
                activePanelOptionsByPath.Remove(prefabPath);
                handle = activeHandle;
                if (handle.View != null)
                {
                    handle.View.gameObject.SetActive(true);
                    handle.View.InternalOnOpen(args);
                }
            }
            else
            {
                handle = await factory.OpenAsync(UIKind.Panel, UILayer.Panel, prefabPath, args, true, false, null);
            }

            if (handle.IsValid)
            {
                stack.Push(new PanelStackEntry(prefabPath, handle, opt));
            }

            return handle;
        }

        public bool PopGroup(string groupId)
        {
            return PopStack(groupId);
        }

        public bool PopStack(string groupId)
        {
            if (!stackGroups.TryGetValue(groupId, out Stack<PanelStackEntry> stack) || stack.Count == 0)
            {
                return false;
            }

            PanelStackEntry top = stack.Pop();
            factory.Close(top.Handle, false, true);

            if (stack.Count == 0)
            {
                stackGroups.Remove(groupId);
                return true;
            }

            PanelStackEntry next = stack.Peek();
            if (next.Handle.View != null)
            {
                next.Handle.View.gameObject.SetActive(true);
                next.Handle.View.InternalOnOpen(null);
            }

            return true;
        }

        public bool HideStack(string groupId)
        {
            if (!stackGroups.TryGetValue(groupId, out Stack<PanelStackEntry> stack) || stack.Count == 0)
            {
                return false;
            }

            while (stack.Count > 0)
            {
                PanelStackEntry entry = stack.Pop();
                factory.Close(entry.Handle, false, true);
            }

            stackGroups.Remove(groupId);
            return true;
        }

        public bool HideAnyBackClosablePanel()
        {
            foreach (KeyValuePair<string, Stack<PanelStackEntry>> pair in stackGroups)
            {
                Stack<PanelStackEntry> stack = pair.Value;
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }

                PanelStackEntry entry = stack.Peek();
                if (entry.Handle.View is UIPanel panel && panel.CanCloseBy(UICloseReason.Back))
                {
                    PopStack(pair.Key);
                    return true;
                }
            }

            List<string> paths = new List<string>(activePanelsByPath.Keys);
            for (int i = paths.Count - 1; i >= 0; i--)
            {
                string path = paths[i];
                UIHandle h = activePanelsByPath[path];
                if (h.View is UIPanel panel && panel.CanCloseBy(UICloseReason.Back))
                {
                    Hide(path);
                    return true;
                }
            }

            return false;
        }

        public void HideAll(bool destroy = false)
        {
            foreach (UIHandle handle in activePanelsByPath.Values)
            {
                factory.Close(handle, destroy, !destroy);
            }

            activePanelsByPath.Clear();
            activePanelOptionsByPath.Clear();

            foreach (Stack<PanelStackEntry> stack in stackGroups.Values)
            {
                while (stack.Count > 0)
                {
                    PanelStackEntry entry = stack.Pop();
                    factory.Close(entry.Handle, destroy, !destroy);
                }
            }

            stackGroups.Clear();
        }

        private bool IsStackTop(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return false;
            }

            foreach (Stack<PanelStackEntry> stack in stackGroups.Values)
            {
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }

                PanelStackEntry top = stack.Peek();
                if (top.Handle.IsValid && string.Equals(top.PrefabPath, prefabPath, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryPopStackTop(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return false;
            }

            string targetGroupId = null;
            foreach (KeyValuePair<string, Stack<PanelStackEntry>> pair in stackGroups)
            {
                Stack<PanelStackEntry> stack = pair.Value;
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }

                PanelStackEntry top = stack.Peek();
                if (string.Equals(top.PrefabPath, prefabPath, System.StringComparison.Ordinal))
                {
                    targetGroupId = pair.Key;
                    break;
                }
            }

            return !string.IsNullOrEmpty(targetGroupId) && PopStack(targetGroupId);
        }

        internal bool HandleOutsidePointer(UIOutsidePointerEvent pointerEvent, IReadOnlyList<RaycastResult> raycastResults)
        {
            if (!TryGetOutsideTarget(pointerEvent, raycastResults, out OutsideClickTarget target))
            {
                return false;
            }

            return CloseOutsideTarget(target);
        }

        internal bool HasOutsideClickTarget()
        {
            return TryGetTopOutsideTarget(out _);
        }

        private bool TryGetOutsideTarget(
            UIOutsidePointerEvent pointerEvent,
            IReadOnlyList<RaycastResult> raycastResults,
            out OutsideClickTarget target)
        {
            if (!TryGetTopOutsideTarget(out target))
            {
                return false;
            }

            UIView view = target.Handle.View;
            if (IsPointerInsideView(view, raycastResults))
            {
                return false;
            }

            UICloseReason reason = UICloseTriggerUtility.ToOutsideReason(pointerEvent.PointerReason, pointerEvent.TouchLike, view);
            return view != null && view.CanCloseBy(reason);
        }

        private bool TryGetTopOutsideTarget(out OutsideClickTarget target)
        {
            target = default;
            bool found = false;
            int topSiblingIndex = int.MinValue;

            foreach (KeyValuePair<string, Stack<PanelStackEntry>> pair in stackGroups)
            {
                Stack<PanelStackEntry> stack = pair.Value;
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }

                PanelStackEntry entry = stack.Peek();
                if (!IsOutsideClickEnabled(entry.Handle, entry.Options))
                {
                    continue;
                }

                int siblingIndex = GetSiblingIndex(entry.Handle);
                if (!found || siblingIndex >= topSiblingIndex)
                {
                    found = true;
                    topSiblingIndex = siblingIndex;
                    target = new OutsideClickTarget(entry.PrefabPath, entry.Handle, pair.Key, true);
                }
            }

            foreach (KeyValuePair<string, UIHandle> pair in activePanelsByPath)
            {
                PanelOptions options = activePanelOptionsByPath.TryGetValue(pair.Key, out PanelOptions storedOptions)
                    ? storedOptions
                    : null;

                if (!IsOutsideClickEnabled(pair.Value, options))
                {
                    continue;
                }

                int siblingIndex = GetSiblingIndex(pair.Value);
                if (!found || siblingIndex >= topSiblingIndex)
                {
                    found = true;
                    topSiblingIndex = siblingIndex;
                    target = new OutsideClickTarget(pair.Key, pair.Value, null, false);
                }
            }

            return found;
        }

        private static bool IsOutsideClickEnabled(UIHandle handle, PanelOptions options)
        {
            if (!handle.IsValid || handle.View == null || !handle.View.IsOpen || !handle.View.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (options != null && !options.UseOutsideClickDetector)
            {
                return false;
            }

            if (handle.View.LastOpenFrame == Time.frameCount)
            {
                return false;
            }

            UICloseTriggers outsideTriggers = UICloseTriggers.LeftOutside | UICloseTriggers.RightOutside;
            return (handle.View.CloseTriggers & outsideTriggers) != 0;
        }

        private static bool IsPointerInsideView(UIView view, IReadOnlyList<RaycastResult> raycastResults)
        {
            if (view == null || raycastResults == null)
            {
                return false;
            }

            Transform viewTransform = view.transform;
            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject hitObject = raycastResults[i].gameObject;
                Transform hitTransform = hitObject != null ? hitObject.transform : null;
                if (hitTransform != null && (hitTransform == viewTransform || hitTransform.IsChildOf(viewTransform)))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetSiblingIndex(UIHandle handle)
        {
            return handle.View != null ? handle.View.transform.GetSiblingIndex() : int.MinValue;
        }

        private bool CloseOutsideTarget(OutsideClickTarget target)
        {
            if (target.IsStack)
            {
                return PopStack(target.StackGroupId);
            }

            return Hide(target.PrefabPath);
        }

        private sealed class PanelStackEntry
        {
            public PanelStackEntry(string prefabPath, UIHandle handle, PanelOptions options)
            {
                PrefabPath = prefabPath;
                Handle = handle;
                Options = options ?? new PanelOptions();
            }

            public string PrefabPath { get; }
            public UIHandle Handle { get; }
            public PanelOptions Options { get; set; }
        }

        private struct OutsideClickTarget
        {
            public OutsideClickTarget(string prefabPath, UIHandle handle, string stackGroupId, bool isStack)
            {
                PrefabPath = prefabPath;
                Handle = handle;
                StackGroupId = stackGroupId;
                IsStack = isStack;
            }

            public string PrefabPath { get; }
            public UIHandle Handle { get; }
            public string StackGroupId { get; }
            public bool IsStack { get; }
        }
    }
}
