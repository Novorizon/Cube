using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object item)
        {
            if (item is GameObject go)
            {
                go.Destroyed = true;
                go.DestroyedNotification?.TrySetResult();
            }
        }
    }

    public class GameObject : Object
    {
        public string name;
        public bool activeInHierarchy = true;
        public bool Destroyed;
        public TaskCompletionSource DestroyedNotification;
        public Transform transform = new Transform();
        public GameObject(string name = "")
        {
            this.name = name;
        }

        public static GameObject Instantiate(GameObject p, Vector3 v, Quaternion q, Transform parent = null) => new()
        {
            transform = new()
            {
                position = v
            }
        };
        public T GetComponent<T>()
            where T : new() => new();
        public T GetComponentInChildren<T>()
            where T : new() => new();
        public T AddComponent<T>()
            where T : new() => new();
        public static GameObject Find(string name) => new(name);
    }

    public class Transform
    {
        public Vector3 position;
        public Vector3 localScale;
        public Quaternion rotation;
        public void SetParent(Transform value, bool world)
        {
        }
    }

    public class TextAsset : Object
    {
        public string text;
        public TextAsset(string text)
        {
            this.text = text;
        }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 one => new(1, 1, 1);

        public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt((a - b).sqrMagnitude);
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float amount) => Distance(a, b) <= amount ? b : a + (b - a).normalized * amount;
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 forward => new(0, 0, 1);
        public static Vector3 right => new(1, 0, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized => sqrMagnitude > 0 ? this * (1 / MathF.Sqrt(sqrMagnitude)) : zero;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }

    public static class Mathf
    {
        public static int CeilToInt(float a) => (int)MathF.Ceiling(a);
        public static int RoundToInt(float a) => (int)MathF.Round(a);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Clamp(int a, int b, int c) => Math.Clamp(a, b, c);
        public static float Clamp(float a, float b, float c) => Math.Clamp(a, b, c);
    }

    public static class Debug
    {
        public static int LoggedExceptions;
        public static void LogException(Exception e)
        {
            LoggedExceptions++;
        }

        public static void Log(object o)
        {
        }

        public static void LogWarning(object o)
        {
        }

        public static void LogError(object o)
        {
        }
    }

    public static class Time
    {
        public static float time;
        public static float deltaTime = 0.1f;
        public static float timeScale = 1;
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = false });
    }
}

namespace UnityEngine.Pool
{
    public static class ListPool<T>
    {
        public static List<T> Get() => new();
        public static void Release(List<T> list) => list.Clear();
    }
}

namespace UnityEngine
{
    public struct Vector3Int
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public struct Quaternion
    {
        public static Quaternion identity => new();

        public static Quaternion LookRotation(Vector3 f, Vector3 u) => new();
    }

    public class Animator : Object
    {
    }

    public static class Random
    {
        public static int Range(int a, int b) => a;
    }
}
