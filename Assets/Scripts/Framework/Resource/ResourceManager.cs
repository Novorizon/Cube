///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2025-12-10
/// Description：资源管理器
///------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using YooAsset;
using Object = UnityEngine.Object;

namespace Game.Framework
{
    public partial class ResourceManager : Singleton<ResourceManager>
    {
        private bool initialized = false;
        public bool Initialized
        {
            get { return initialized; }
        }

        private ResourcePackage package = null;

        //缓存资源句柄
        private readonly Dictionary<string, AssetHandle> assetHandles = new Dictionary<string, AssetHandle>();

        //缓存场景句柄
        private readonly Dictionary<string, SceneHandle> sceneHandles = new Dictionary<string, SceneHandle>();

        /// <summary>
        /// 异步初始化
        /// 适配原先逻辑，增加回调版本
        /// </summary>
        public async Task InitializeAsync(Action<bool> callback)
        {
            bool result = await InitializeAsync();
            callback?.Invoke(result);
            SpriteAtlasManager.atlasRequested += RequestAtlas;
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            YooAssets.Initialize();

            package = YooAssets.TryGetPackage("DefaultPackage");
            if (package == null)
            {
                package = YooAssets.CreatePackage("DefaultPackage");
            }

            YooAssets.SetDefaultPackage(package);

            InitializeParameters parameters = YooAssetsSettings.CreateParameters();
            initialized = await InitPackageAsync(package, parameters);

            //await package.CreateResourceImporter(files, 10, 1);
            return initialized;
        }
        public async Task<bool> InitPackageAsync(ResourcePackage package, InitializeParameters parameters)
        {
            // 1. 初始化package
            var initOp = package.InitializeAsync(parameters);
            await initOp.Task;
            if (initOp.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"InitializeAsync 失败：{initOp.Error}");
                return false;
            }

            // 2. 请求资源清单的版本信息
            var verOp = package.RequestPackageVersionAsync();
            await verOp.Task;
            if (verOp.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"RequestPackageVersionAsync 失败：{verOp.Error}");
                return false;
            }
            string version = verOp.PackageVersion;

            // 3. 用上一步拿到的版本号更新资源清单
            var manifestOp = package.UpdatePackageManifestAsync(version);
            await manifestOp.Task;
            if (manifestOp.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"UpdatePackageManifestAsync 失败：{manifestOp.Error}");
                return false;
            }

            return true;
        }


        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async Task<T> LoadAssetAsync<T>(string location) where T : Object
        {
            T obj = await LoadAssetInternalAsync<T>(location);
            return obj;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async Task LoadAssetAsync<T>(string location, Action<T> callback) where T : Object
        {
            T obj = await LoadAssetInternalAsync<T>(location);
            callback?.Invoke(obj);
        }

        private async Task<T> LoadAssetInternalAsync<T>(string location) where T : Object
        {
            if (string.IsNullOrEmpty(location))
                return null;

            if (package == null)
                return null;

            if (assetHandles.TryGetValue(location, out var cached) && cached.IsValid)
            {
                if (cached.AssetObject != null)
                {
                    var t = cached.AssetObject as T;
                    if (t != null)
                    {
                        return t as T;
                    }
                }
                assetHandles.Remove(location);
            }

            AssetHandle handle = package.LoadAssetAsync<T>(location);
            await handle.Task;
            assetHandles[location] = handle;

            return handle.AssetObject as T;
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T LoadAsset<T>(string location) where T : Object
        {
            T asset = LoadAssetInternal<T>(location);
            return asset;
        }


        /// <summary>
        /// 同步加载资源
        /// </summary>
        public void LoadAsset<T>(string location, Action<T> callback) where T : Object
        {
            T asset = LoadAssetInternal<T>(location);
            callback?.Invoke(asset);
        }

        private T LoadAssetInternal<T>(string location) where T : Object
        {
            if (string.IsNullOrEmpty(location))
                return null;

            if (package == null)
            {
                Debug.LogErrorFormat("package is null", location);
                return null;
            }

            if (assetHandles.TryGetValue(location, out var cached) && cached.IsValid)
            {
                if (cached.AssetObject)
                {
                    var t = cached.AssetObject as T;
                    if (t)
                    {
                        return t;
                    }
                }
                assetHandles.Remove(location);
            }

            AssetHandle handle = package.LoadAssetSync<T>(location);
            assetHandles[location] = handle;

            return handle.AssetObject as T;
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async Task<Scene> LoadSceneAsync(string location)
        {
            Scene scene = await LoadSceneInternaAsync(location, LoadSceneMode.Single);
            return scene;
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async Task<Scene> LoadSceneAsync(string location, LoadSceneMode mode)
        {
            Scene scene = await LoadSceneInternaAsync(location, mode);
            return scene;
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async Task LoadSceneAsync(string location, Action<Scene> callback)
        {
            Scene scene = await LoadSceneInternaAsync(location, LoadSceneMode.Single);
            callback?.Invoke(scene);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async Task LoadSceneAsync(string location, LoadSceneMode mode, Action<Scene> callback)
        {
            Scene scene = await LoadSceneInternaAsync(location, mode);
            callback?.Invoke(scene);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        private async Task<Scene> LoadSceneInternaAsync(string location, LoadSceneMode mode)
        {

            if (package == null)
            {
                throw new Exception("package is null");
            }

            if (sceneHandles.TryGetValue(location, out var cached) && cached.IsValid)
            {
                return cached.SceneObject;
            }

            SceneHandle handle = package.LoadSceneAsync(location, mode);
            await handle.Task;
            sceneHandles[location] = handle;

            return handle.SceneObject;
        }


        /// <summary>
        /// 卸载 location 对应的句柄 
        /// </summary>
        public void ReleaseHandle(string location)
        {
            if (assetHandles.TryGetValue(location, out var handle))
            {
                if (handle.IsValid)
                    handle.Release();

                assetHandles.Remove(location);
            }
        }

        /// <summary>
        /// 卸载 location 
        /// </summary>
        public void TryUnloadAsset(string location, int loopCount = 10)
        {
            if (assetHandles.TryGetValue(location, out var handle))
            {
                if (handle.IsValid)
                    handle.Release();

                assetHandles.Remove(location);
            }
            package.TryUnloadUnusedAsset(location, loopCount);
        }

        /// <summary>
        /// 卸载 
        /// </summary>
        public async Task TryUnloadUnusedAsset(int loopCount = 10)
        {
            await package.UnloadUnusedAssetsAsync(loopCount).Task;
            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 卸载所有通过本管理器加载的资源句柄
        /// </summary>
        public void ReleaseAllAssets()
        {
            foreach (var handle in assetHandles)
            {
                if (handle.Value.IsValid)
                {
                    handle.Value.Release();
                }
            }

            assetHandles.Clear();
        }


        /// <summary>
        /// 卸载场景
        /// </summary>
        public async Task UnloadSceneAsync(string location)
        {
            if (sceneHandles.TryGetValue(location, out var handle) && handle.IsValid)
            {
                UnloadSceneOperation unloadOp = handle.UnloadAsync();
                sceneHandles.Remove(location);
                await unloadOp.Task;
            }
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        public async Task UnloadSceneAsync(Scene scene)
        {
            if (sceneHandles.TryGetValue(scene.name, out var handle) && handle.IsValid)
            {
                UnloadSceneOperation unloadOp = handle.UnloadAsync();
                sceneHandles.Remove(scene.name);
                await unloadOp.Task;
            }
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        public async Task UnloadSceneAsync(string location, Action callback)
        {
            await UnloadSceneAsync(location);
            callback?.Invoke();
        }
    }
}
