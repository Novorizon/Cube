using Game.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed partial class MapManager : Singleton<MapManager>
{
    [SerializeField] private List<TextAsset> mapJsonAssets = new();
    [SerializeField] private string defaultMapId;
    [SerializeField] private MapVisualLibrary visualLibrary;
    [SerializeField] private Transform mapRoot;

    public MapData CurrentMap { get; private set; }

    private readonly List<MapEntry> entries = new();
    private readonly Dictionary<string, MapEntry> entryById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MapEntry> entryByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int3, TileView> tileViews = new();

    public IReadOnlyList<MapEntry> Entries => entries;

    public void Initialize()
    {
        BuildIndex();

        if (!string.IsNullOrWhiteSpace(defaultMapId))
        {
            LoadById(defaultMapId);
        }
    }
}

public sealed class MapEntry
{
    public string MapId;
    public string MapName;
    public int Version;
    public TextAsset JsonAsset;
    public MapJsonData CachedJson;
}
