using System;
using System.IO;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string path) { }
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static string dataPath => Path.Combine(Directory.GetCurrentDirectory(), "Assets");
    }

    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        public static void LogWarning(object message) => Console.WriteLine(message);
        public static void LogError(object message) => Console.Error.WriteLine(message);
    }
}

namespace Game.Ability
{
    // Definitions.cs only needs this surface for LevelValue resolution. Validation never creates
    // runtime Ability instances, so the CLI deliberately avoids linking Unity runtime assemblies.
    public sealed class Ability
    {
        public int Level => 1;
        public float GetSpecialValue(string name) => 0f;
    }
}
