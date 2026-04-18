using Game.Framework;
using UnityEngine;

public sealed class GameSceneManager : Singleton<GameSceneManager>
{
    [SerializeField] private GameSceneConfig sceneConfig;
    [SerializeField] private Camera mainCamera;

    public GameSceneConfig SceneConfig => sceneConfig;

    public void Initialize()
    {
        ApplySceneConfig();
        LoadConfiguredMap();
    }

    private void ApplySceneConfig()
    {
        if (sceneConfig == null)
        {
            return;
        }

        ApplyPhysicsConfig(sceneConfig.physics);
        ApplyViewConfig(sceneConfig.view);
        ApplyCameraConfig(sceneConfig.cameraDriverMode, sceneConfig.camera);
    }

    private void LoadConfiguredMap()
    {
        if (sceneConfig == null)
        {
            return;
        }

        MapManager.Instance.Initialize();

        if (sceneConfig.mapLoadMode == MapLoadMode.ById)
        {
            MapManager.Instance.LoadById(sceneConfig.mapId);
        }
        else
        {
            MapManager.Instance.LoadByName(sceneConfig.mapName);
        }
    }

    private void ApplyPhysicsConfig(ScenePhysicsConfig config)
    {
        if (config == null)
        {
            return;
        }

        Physics.autoSimulation = config.autoSimulation;
        Physics.gravity = config.gravity;
        Time.fixedDeltaTime = config.fixedDeltaTime;
    }

    private void ApplyViewConfig(SceneViewConfig config)
    {
        if (config == null || MapManager.Instance == null)
        {
            return;
        }

        MapVisualLibrary visualLibrary = GetVisualLibrary();
        if (visualLibrary == null)
        {
            return;
        }

        visualLibrary.cellSize = config.cellSize;
        visualLibrary.heightStep = config.heightStep;
    }

    private MapVisualLibrary GetVisualLibrary()
    {
        return MapManager.Instance == null ? null : MapManager.Instance.GetVisualLibrary();
    }

    private void ApplyCameraConfig(CameraDriverMode driverMode, SceneCameraConfig config)
    {
        if (config == null)
        {
            return;
        }

        if (driverMode == CameraDriverMode.VirtualCamera)
        {
            Debug.LogWarning("当前骨架未强依赖 Cinemachine。若使用 Virtual Camera，请在场景中挂 CinemachineBrain，并用你项目里的相机控制脚本接管。当前会回退到直接设置主相机。\n");
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || !config.lockMainCameraOnStart)
        {
            return;
        }

        Transform t = mainCamera.transform;
        t.position = config.position;
        t.rotation = Quaternion.Euler(config.rotationEuler);
        mainCamera.orthographic = config.useOrthographic;

        if (config.useOrthographic)
        {
            mainCamera.orthographicSize = config.orthographicSize;
        }
        else
        {
            mainCamera.fieldOfView = config.fieldOfView;
        }
    }
}
