using System.IO;
using Newtonsoft.Json;

public static class MapJsonIO
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Formatting = Formatting.Indented
    };

    public static string Serialize(MapJsonData data, bool indented = true)
    {
        Formatting formatting = indented ? Formatting.Indented : Formatting.None;
        return JsonConvert.SerializeObject(data, formatting, Settings);
    }

    public static MapJsonData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MapJsonData();
        }

        MapJsonData data = JsonConvert.DeserializeObject<MapJsonData>(json, Settings);
        return data ?? new MapJsonData();
    }

    public static MapJsonData LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new MapJsonData();
        }

        string json = File.ReadAllText(path);
        return Deserialize(json);
    }

    public static void SaveToFile(string path, MapJsonData data, bool indented = true)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = Serialize(data, indented);
        File.WriteAllText(path, json);
    }
}
