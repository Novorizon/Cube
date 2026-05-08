using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Framework;
using UnityEngine;

namespace UI
{
    public sealed class ResourceManagerUIAssetLoader : IUIAssetLoader
    {
        readonly Dictionary<string, int> refCounts = new Dictionary<string, int>();

        public async Task<UIAssetLoadResult> LoadPrefabAsync(string prefabPath)
        {
            GameObject prefab = await ResourceManager.Instance.LoadGameObjectAsync(prefabPath);
            if (prefab == null)
            {
                return default;
            }

            AddRef(prefabPath);
            return new UIAssetLoadResult(prefab, new ResourceLease(this, prefabPath));
        }

        void AddRef(string prefabPath)
        {
            refCounts.TryGetValue(prefabPath, out int count);
            refCounts[prefabPath] = count + 1;
        }

        void Release(string prefabPath)
        {
            if (!refCounts.TryGetValue(prefabPath, out int count))
            {
                return;
            }

            count--;
            if (count > 0)
            {
                refCounts[prefabPath] = count;
                return;
            }

            refCounts.Remove(prefabPath);
            ResourceManager.Instance.ReleaseHandle(prefabPath);
        }

        sealed class ResourceLease : IDisposable
        {
            readonly ResourceManagerUIAssetLoader owner;
            readonly string prefabPath;
            bool disposed;

            public ResourceLease(ResourceManagerUIAssetLoader owner, string prefabPath)
            {
                this.owner = owner;
                this.prefabPath = prefabPath;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.Release(prefabPath);
            }
        }
    }
}
