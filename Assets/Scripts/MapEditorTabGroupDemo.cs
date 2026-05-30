using Sirenix.OdinInspector;
using UnityEngine;

public class MapEditorTabGroupDemo : MonoBehaviour
{
    [Title("地形编辑")]
    [TabGroup("Tabs", "地形")]
    [LabelText("笔刷大小")]
    [MinValue(1)]
    public int brushSize = 1;

    [TabGroup("Tabs", "地形")]
    [LabelText("地形高度")]
    public int height = 1;

    [TabGroup("Tabs", "地形")]
    [LabelText("当前地形类型")]
    public TerrainTileType terrainType = TerrainTileType.Grass;

    [TabGroup("Tabs", "地形")]
    [Button("绘制地形")]
    public void PaintTerrain()
    {
        Debug.Log($"Paint Terrain: {terrainType}, BrushSize: {brushSize}, Height: {height}");
    }

    [Title("树木编辑")]
    [TabGroup("Tabs", "树木")]
    [LabelText("树木 Prefab")]
    public GameObject treePrefab;

    [TabGroup("Tabs", "树木")]
    [LabelText("树木密度")]
    [Range(0f, 1f)]
    public float treeDensity = 0.5f;

    [TabGroup("Tabs", "树木")]
    [Button("生成树木")]
    public void GenerateTrees()
    {
        Debug.Log($"Generate Trees: {treePrefab}, Density: {treeDensity}");
    }

    [Title("草地编辑")]
    [TabGroup("Tabs", "草地")]
    [LabelText("草 Prefab")]
    public GameObject grassPrefab;

    [TabGroup("Tabs", "草地")]
    [LabelText("草密度")]
    [Range(0f, 1f)]
    public float grassDensity = 0.5f;

    [TabGroup("Tabs", "草地")]
    [Button("生成草")]
    public void GenerateGrass()
    {
        Debug.Log($"Generate Grass: {grassPrefab}, Density: {grassDensity}");
    }

    [Title("设置")]
    [TabGroup("Tabs", "设置")]
    [LabelText("显示网格")]
    public bool showGrid = true;

    [TabGroup("Tabs", "设置")]
    [LabelText("自动保存")]
    public bool autoSave = false;

    [TabGroup("Tabs", "设置")]
    [Button("重新生成地图")]
    public void RegenerateMap()
    {
        Debug.Log("Regenerate Map");
    }
}

public enum TerrainTileType
{
    Grass,
    Hill,
    Water,
    Snow
}