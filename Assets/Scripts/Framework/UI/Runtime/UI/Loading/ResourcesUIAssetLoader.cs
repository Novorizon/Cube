using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public class ResourcesUIAssetLoader : IUIAssetLoader
    {
        public async Task<UIAssetLoadResult> LoadPrefabAsync(string prefabPath)
        {
            await Task.Yield();
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            return new UIAssetLoadResult(prefab, EmptyLease.Instance);
        }

        sealed class EmptyLease : IDisposable
        {
            public static readonly EmptyLease Instance = new EmptyLease();

            public void Dispose()
            {
            }
        }
    }
    public sealed class ResourcesAssetLoader : ResourcesUIAssetLoader
    {
    }
}
