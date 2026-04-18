using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public sealed class ResourcesAssetLoader : IUIAssetLoader
    {
        public async Task<GameObject?> LoadPrefabAsync(string prefabPath)
        {
            await Task.Yield();
            return Resources.Load<GameObject>(prefabPath);
        }

        public void ReleasePrefab(GameObject prefab)
        {
        }
    }
}
