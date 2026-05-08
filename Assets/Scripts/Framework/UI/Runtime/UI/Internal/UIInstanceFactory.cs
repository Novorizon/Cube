using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public sealed class UIInstanceFactory
    {
        readonly IUIAssetLoader loader;
        readonly Dictionary<UILayer, Transform> layerRoots;
        readonly Dictionary<int, UIInstanceRecord> recordsById = new Dictionary<int, UIInstanceRecord>();
        readonly Dictionary<string, UIInstanceRecord> singletonActive = new Dictionary<string, UIInstanceRecord>();
        readonly Dictionary<string, Stack<UIInstanceRecord>> cachedByPath = new Dictionary<string, Stack<UIInstanceRecord>>();
        readonly Dictionary<string, Task<UIHandle>> openingSingletonTasks = new Dictionary<string, Task<UIHandle>>();

        int nextId;
        int nextVersion;

        public UIInstanceFactory(IUIAssetLoader loader, Dictionary<UILayer, Transform> layerRoots, int startId, int startVersion)
        {
            this.loader = loader;
            this.layerRoots = layerRoots;
            nextId = startId;
            nextVersion = startVersion;
        }

        public int NextId => nextId;
        public int NextVersion => nextVersion;

        public Task<UIHandle> OpenAsync(UIKind kind, UILayer layer, string prefabPath, object args, bool allowMultiple, bool cacheOnClose, Func<Transform, UIBlocker> blockerFactory)
        {
            if (!allowMultiple)
            {
                if (singletonActive.TryGetValue(prefabPath, out UIInstanceRecord existing) && existing.Handle.IsValid)
                {
                    existing.IsCached = false;
                    existing.Handle.View.gameObject.SetActive(true);
                    existing.Handle.View.InternalOnOpen(args);
                    return Task.FromResult(existing.Handle);
                }

                if (openingSingletonTasks.TryGetValue(prefabPath, out Task<UIHandle> openingTask))
                {
                    return openingTask;
                }

                Task<UIHandle> task = OpenInternalAsync(kind, layer, prefabPath, args, allowMultiple, cacheOnClose, blockerFactory);
                openingSingletonTasks[prefabPath] = task;
                return AwaitAndRemoveOpening(prefabPath, task);
            }

            return OpenInternalAsync(kind, layer, prefabPath, args, allowMultiple, cacheOnClose, blockerFactory);
        }

        async Task<UIHandle> AwaitAndRemoveOpening(string prefabPath, Task<UIHandle> task)
        {
            try
            {
                return await task;
            }
            finally
            {
                openingSingletonTasks.Remove(prefabPath);
            }
        }

        async Task<UIHandle> OpenInternalAsync(UIKind kind, UILayer layer, string prefabPath, object args, bool allowMultiple, bool cacheOnClose, Func<Transform, UIBlocker> blockerFactory)
        {
            if (cacheOnClose && TryTakeCached(prefabPath, out UIInstanceRecord cached))
            {
                cached.IsCached = false;
                cached.Handle.View.gameObject.SetActive(true);
                cached.Handle.View.InternalOnOpen(args);

                if (!allowMultiple)
                {
                    singletonActive[prefabPath] = cached;
                }

                return cached.Handle;
            }

            UIAssetLoadResult asset = await loader.LoadPrefabAsync(prefabPath);
            if (!asset.IsValid)
            {
                Debug.LogError($"[UI] Prefab not found: {prefabPath}");
                asset.Lease?.Dispose();
                return default;
            }

            Transform parent = layerRoots[layer];
            UIBlocker blocker = blockerFactory != null ? blockerFactory(parent) : null;

            GameObject go = UnityEngine.Object.Instantiate(asset.Prefab, parent);
            go.name = asset.Prefab.name;

            UIView view = go.GetComponent<UIView>();
            if (view == null)
            {
                Debug.LogError($"[UI] Prefab must have a UIView-derived component. path={prefabPath}, instance={go.name}");
                view = go.AddComponent<MissingUIViewMarker>();
            }

            int id = nextId++;
            int version = nextVersion++;
            view.InstanceId = id;
            view.InstanceVersion = version;

            if (blocker != null)
            {
                int viewIndex = go.transform.GetSiblingIndex();
                blocker.transform.SetSiblingIndex(viewIndex);
            }

            UIHandle handle = new UIHandle(id, version, prefabPath, kind, layer, view);
            UIInstanceRecord record = new UIInstanceRecord(handle, cacheOnClose, asset.Lease);
            recordsById[id] = record;

            view.InternalOnCreate();
            view.InternalOnOpen(args);

            if (!allowMultiple)
            {
                singletonActive[prefabPath] = record;
            }

            return handle;
        }

        public void Close(UIHandle handle, bool destroy, bool cacheOnClose)
        {
            if (!TryGetRecord(handle, out UIInstanceRecord record))
            {
                return;
            }

            UIView view = record.Handle.View;
            view.InternalOnClose();
            singletonActive.Remove(record.Handle.PrefabPath);

            bool shouldCache = !destroy && cacheOnClose && record.CacheOnClose && view != null && !view.IsDestroyed;
            if (shouldCache)
            {
                view.gameObject.SetActive(false);
                record.IsCached = true;
                PushCached(record.Handle.PrefabPath, record);
                return;
            }

            DestroyRecord(record);
        }

        public void DestroyAll()
        {
            List<UIInstanceRecord> records = new List<UIInstanceRecord>(recordsById.Values);
            for (int i = 0; i < records.Count; i++)
            {
                DestroyRecord(records[i]);
            }

            singletonActive.Clear();
            cachedByPath.Clear();
            openingSingletonTasks.Clear();
        }

        bool TryGetRecord(UIHandle handle, out UIInstanceRecord record)
        {
            record = null;
            if (!handle.IsValid)
            {
                return false;
            }

            if (!recordsById.TryGetValue(handle.Id, out record))
            {
                return false;
            }

            return record.Handle.Version == handle.Version;
        }

        bool TryTakeCached(string prefabPath, out UIInstanceRecord record)
        {
            record = null;
            if (!cachedByPath.TryGetValue(prefabPath, out Stack<UIInstanceRecord> stack))
            {
                return false;
            }

            while (stack.Count > 0)
            {
                UIInstanceRecord candidate = stack.Pop();
                if (candidate.Handle.IsValid)
                {
                    record = candidate;
                    return true;
                }

                DestroyRecord(candidate);
            }

            cachedByPath.Remove(prefabPath);
            return false;
        }

        void PushCached(string prefabPath, UIInstanceRecord record)
        {
            if (!cachedByPath.TryGetValue(prefabPath, out Stack<UIInstanceRecord> stack))
            {
                stack = new Stack<UIInstanceRecord>();
                cachedByPath[prefabPath] = stack;
            }

            stack.Push(record);
        }

        void DestroyRecord(UIInstanceRecord record)
        {
            if (record == null)
            {
                return;
            }

            recordsById.Remove(record.Handle.Id);
            singletonActive.Remove(record.Handle.PrefabPath);
            record.DisposeAssetLease();

            if (record.Handle.View != null)
            {
                UnityEngine.Object.Destroy(record.Handle.View.gameObject);
            }
        }

        sealed class MissingUIViewMarker : UIView
        {
        }
    }
}
