using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game
{
    internal static class WorldPanelBindingUtility
    {
        public static Transform FindFirst(Transform root, params string[] paths)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                Transform transform = root.Find(paths[i]);
                if (transform != null)
                {
                    return transform;
                }
            }

            return null;
        }

        public static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child == null)
            {
                return null;
            }

            TMP_Text text = child.GetComponent<TMP_Text>();
            return text != null ? text : child.GetComponentInChildren<TMP_Text>(true);
        }

        public static void SetChildText(Transform parent, string childName, string content)
        {
            TMP_Text text = FindText(parent, childName);
            if (text != null)
            {
                text.text = content;
            }
        }

        public static void BindButton(Transform transform, UnityAction clicked, string label)
        {
            if (transform == null)
            {
                return;
            }

            Button button = transform.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"[WorldMainPanel] Missing static Button for {label}. Configure it on the prefab node: {GetTransformPath(transform)}");
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
        }

        public static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
