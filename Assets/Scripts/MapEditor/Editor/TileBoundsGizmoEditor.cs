#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(TileBoundsGizmo))]
    public sealed class TileBoundsGizmoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            TileBoundsGizmo gizmo = (TileBoundsGizmo)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Renderer Bounds", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Size", gizmo.BoundsSize);
                EditorGUILayout.Vector3Field("Center", gizmo.BoundsCenter);
            }

            if (GUILayout.Button("Refresh Bounds"))
            {
                Repaint();
                SceneView.RepaintAll();
            }
        }
    }
}

#endif
