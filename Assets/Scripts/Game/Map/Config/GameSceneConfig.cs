using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/Game Scene Config")]
public sealed class GameSceneConfig : ScriptableObject
{
    public MapLoadMode mapLoadMode = MapLoadMode.ById;
    public string mapId = "new_map";
    public string mapName = "New Map";
    public CameraDriverMode cameraDriverMode = CameraDriverMode.DirectMainCamera;
    public SceneCameraConfig camera = new SceneCameraConfig();
    public ScenePhysicsConfig physics = new ScenePhysicsConfig();
    public SceneViewConfig view = new SceneViewConfig();
}

public enum MapLoadMode
{
    ById = 0,
    ByName = 1
}

public enum CameraDriverMode
{
    DirectMainCamera = 0,
    VirtualCamera = 1
}

[Serializable]
public sealed class SceneCameraConfig
{
    public bool useOrthographic = false;
    public float fieldOfView = 45f;
    public float orthographicSize = 12f;
    public Vector3 position = new Vector3(8f, 12f, -8f);
    public Vector3 rotationEuler = new Vector3(45f, 45f, 0f);
    public bool lockMainCameraOnStart = true;
    public bool enableMousePan = true;
    public bool enableMouseZoom = true;
    public float panSpeed = 15f;
    public float zoomSpeed = 8f;
    public float minZoom = 6f;
    public float maxZoom = 24f;
}

[Serializable]
public sealed class ScenePhysicsConfig
{
    public bool usePhysics = false;
    public bool autoSimulation = false;
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    public float fixedDeltaTime = 0.02f;
}

[Serializable]
public sealed class SceneViewConfig
{
    public float cellSize = 1f;
    public float heightStep = 1f;
}
