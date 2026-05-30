using UnityEngine;

public class MapEditorToolbarDemo : MonoBehaviour
{
    public int brushSize = 1;

    public int height = 1;

    public TerrainTileType terrainType = TerrainTileType.Grass;

    public GameObject treePrefab;

    [Range(0f, 1f)]
    public float treeDensity = 0.5f;

    public GameObject grassPrefab;

    [Range(0f, 1f)]
    public float grassDensity = 0.5f;

    public bool showGrid = true;

    public bool autoSave = false;

    public void PaintTerrain()
    {
        Debug.Log($"Paint Terrain: {terrainType}, BrushSize: {brushSize}, Height: {height}");
    }

    public void GenerateTrees()
    {
        Debug.Log($"Generate Trees: {treePrefab}, Density: {treeDensity}");
    }

    public void GenerateGrass()
    {
        Debug.Log($"Generate Grass: {grassPrefab}, Density: {grassDensity}");
    }

    public void RegenerateMap()
    {
        Debug.Log("Regenerate Map");
    }
}