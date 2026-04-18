using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public sealed class UIInstanceFactory
    {
        readonly IUIAssetLoader loader;
        readonly Dictionary<UILayer, Transform> layerRoots;

        readonly Dictionary<string, UIView> singletonActive = new();
        readonly Dictionary<string, UIView> cached = new();

        int nextId;

        public UIInstanceFactory(IUIAssetLoader loader, Dictionary<UILayer, Transform> layerRoots, int startId)
        {
            this.loader = loader;
            this.layerRoots = layerRoots;
            nextId = startId;
        }

        public int NextId => nextId;

        public async Task<UIHandle> OpenAsync(
            UIKind kind,
            UILayer layer,
            string prefabPath,
            object? args,
            bool allowMultiple,
            bool cacheOnClose,
            System.Func<Transform, UIBlocker?>? blockerFactory)
        {
            if (!allowMultiple && singletonActive.TryGetValue(prefabPath, out UIView? existing) && existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.InternalOnOpen(args);
                return new UIHandle(existing.InstanceId, prefabPath, kind, layer, existing);
            }

            if (cacheOnClose && cached.TryGetValue(prefabPath, out UIView? cachedView) && cachedView != null)
            {
                cached.Remove(prefabPath);

                cachedView.gameObject.SetActive(true);
                cachedView.InternalOnOpen(args);

                if (!allowMultiple)
                {
                    singletonActive[prefabPath] = cachedView;
                }

                return new UIHandle(cachedView.InstanceId, prefabPath, kind, layer, cachedView);
            }

            GameObject? prefab = await loader.LoadPrefabAsync(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[UI] Prefab not found: {prefabPath}");
                return default;
            }

            Transform parent = layerRoots[layer];

            UIBlocker? blocker = null;
            if (blockerFactory != null)
            {
                blocker = blockerFactory(parent);
            }

            GameObject go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;

            UIView? view = go.GetComponent<UIView>();
            if (view == null)
            {
                Debug.LogError($"[UI] Prefab must have a UIView-derived component. path={prefabPath}, instance={go.name}");
                view = go.AddComponent<MissingUIViewMarker>();
            }

            int id = nextId++;
            view.InstanceId = id;

            if (blocker != null)
            {
                int popupIndex = go.transform.GetSiblingIndex();
                blocker.transform.SetSiblingIndex(popupIndex);
            }

            view.InternalOnCreate();
            view.InternalOnOpen(args);

            if (!allowMultiple)
            {
                singletonActive[prefabPath] = view;
            }

            return new UIHandle(id, prefabPath, kind, layer, view);
        }

        public void Close(UIHandle handle, bool destroy, bool cacheOnClose)
        {
            if (!handle.IsValid || handle.View == null)
            {
                return;
            }

            UIView view = handle.View;

            view.InternalOnClose();

            if (!destroy && cacheOnClose)
            {
                view.gameObject.SetActive(false);
                cached[handle.PrefabPath] = view;
                return;
            }

            Object.Destroy(view.gameObject);
        }

        sealed class MissingUIViewMarker : UIView
        {
        }
    }
}
