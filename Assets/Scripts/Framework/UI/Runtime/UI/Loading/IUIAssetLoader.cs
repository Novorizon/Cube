using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public interface IUIAssetLoader
    {
        Task<GameObject?> LoadPrefabAsync(string prefabPath);
        void ReleasePrefab(GameObject prefab);
    }
}
