#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class TowerDefenseMapEditorWindow : MapEditorWindow
    {
        protected override string EditorTitle => "Tower Defense Map Editor";
        protected override bool UsesFixedValidationMode => true;
        protected override MapEditorValidationMode DefaultValidationMode => MapEditorValidationMode.TowerDefense;
        protected override bool SupportsPointsTab => true;
        protected override bool SupportsResourcesTab => false;

        [MenuItem("Tools/Map/Tower Defense Map Editor")]
        public static void OpenTowerDefenseMapEditor()
        {
            TowerDefenseMapEditorWindow window = GetWindow<TowerDefenseMapEditorWindow>();
            window.titleContent = new GUIContent("Tower Defense Map Editor");
            window.Show();
        }
    }
}

#endif
