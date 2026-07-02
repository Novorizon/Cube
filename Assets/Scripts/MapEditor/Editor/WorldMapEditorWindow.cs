#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class WorldMapEditorWindow : MapEditorWindow
    {
        protected override string EditorTitle => "World Map Editor";
        protected override bool UsesFixedValidationMode => true;
        protected override MapEditorValidationMode DefaultValidationMode => MapEditorValidationMode.World;
        protected override bool SupportsPointsTab => false;
        protected override bool SupportsResourcesTab => true;

        [MenuItem("Tools/Map/World Map Editor")]
        public static void OpenWorldMapEditor()
        {
            WorldMapEditorWindow window = GetWindow<WorldMapEditorWindow>();
            window.titleContent = new GUIContent("World Map Editor");
            window.Show();
        }
    }
}

#endif
