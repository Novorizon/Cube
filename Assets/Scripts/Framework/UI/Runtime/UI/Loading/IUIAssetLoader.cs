using System.Threading.Tasks;

namespace UI
{
    public interface IUIAssetLoader
    {
        Task<UIAssetLoadResult> LoadPrefabAsync(string prefabPath);
    }
}
