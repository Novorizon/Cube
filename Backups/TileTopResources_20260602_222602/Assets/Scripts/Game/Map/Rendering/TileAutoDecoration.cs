using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [ExecuteAlways]
    public sealed class TileAutoDecoration : MonoBehaviour
    {
        [Serializable]
        public sealed class DecorationOption
        {
            public GameObject Prefab;
            public int Weight = 1;
            public Vector2 ScaleRange = Vector2.one;
        }

        private const string GeneratedRootName = "__AutoDecorations";

        [SerializeField]
        private bool generateInEditMode = true;

        [SerializeField]
        private int maxCount = 3;

        [SerializeField]
        private float spawnChance = 0.65f;

        [SerializeField]
        private float topY = 1.035f;

        [SerializeField]
        private Vector2 xRange = new Vector2(-0.36f, 0.36f);

        [SerializeField]
        private Vector2 zRange = new Vector2(-0.36f, 0.36f);

        [SerializeField]
        private List<DecorationOption> options = new List<DecorationOption>();

        private Vector3 lastPosition;

        private void OnEnable()
        {
            if (!Application.isPlaying && !generateInEditMode)
            {
                return;
            }

            if (ShouldSkipEditModeGeneration())
            {
                return;
            }

            Refresh();
        }

        private void Update()
        {
            if (Application.isPlaying || !generateInEditMode || ShouldSkipEditModeGeneration())
            {
                return;
            }

            if ((transform.position - lastPosition).sqrMagnitude < 0.0001f)
            {
                return;
            }

            Refresh();
        }

        public void Refresh()
        {
            lastPosition = transform.position;
            ClearGenerated();

            if (options == null || options.Count == 0 || maxCount <= 0)
            {
                return;
            }

            int seed = GetStableSeed();
            System.Random random = new System.Random(seed);
            Transform root = CreateGeneratedRoot();

            int count = 0;
            for (int i = 0; i < maxCount; i++)
            {
                if (random.NextDouble() > spawnChance)
                {
                    continue;
                }

                DecorationOption option = PickOption(random);
                if (option == null || option.Prefab == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(option.Prefab, root);
                instance.name = option.Prefab.name;
                instance.transform.localPosition = new Vector3(
                    Lerp(random, xRange.x, xRange.y),
                    topY,
                    Lerp(random, zRange.x, zRange.y));
                instance.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

                float minScale = option.ScaleRange.x <= 0f ? 1f : option.ScaleRange.x;
                float maxScale = option.ScaleRange.y <= 0f ? minScale : option.ScaleRange.y;
                float scale = Lerp(random, minScale, maxScale);
                instance.transform.localScale = Vector3.one * scale;
                count++;
            }

            if (count == 0)
            {
                DestroyGeneratedRoot(root.gameObject);
            }
        }

        private DecorationOption PickOption(System.Random random)
        {
            int totalWeight = 0;
            for (int i = 0; i < options.Count; i++)
            {
                DecorationOption option = options[i];
                if (option != null && option.Prefab != null && option.Weight > 0)
                {
                    totalWeight += option.Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = random.Next(0, totalWeight);
            for (int i = 0; i < options.Count; i++)
            {
                DecorationOption option = options[i];
                if (option == null || option.Prefab == null || option.Weight <= 0)
                {
                    continue;
                }

                if (roll < option.Weight)
                {
                    return option;
                }

                roll -= option.Weight;
            }

            return null;
        }

        private int GetStableSeed()
        {
            Vector3 position = transform.position;
            int x = Mathf.RoundToInt(position.x * 100f);
            int y = Mathf.RoundToInt(position.y * 100f);
            int z = Mathf.RoundToInt(position.z * 100f);
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                hash = hash * 31 + gameObject.name.GetHashCode();
                return hash;
            }
        }

        private Transform CreateGeneratedRoot()
        {
            GameObject root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private void ClearGenerated()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == GeneratedRootName)
                {
                    DestroyGeneratedRoot(child.gameObject);
                }
            }
        }

        private void DestroyGeneratedRoot(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static float Lerp(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private bool ShouldSkipEditModeGeneration()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return false;
            }

            return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            return false;
#endif
        }
    }
}
