using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Art
{
    public static class MeshyPirateLowProcessor
    {
        private const string FbxPath = "Assets/Arts/Character/Incoming/Pirate_Meshy/low/Processed/Pirate_Low_QGenericRigV2.fbx";
        private const string MaterialPath = "Assets/Arts/Character/Incoming/Pirate_Meshy/low/Processed/Pirate_Low_StylizedMatte.mat";
        private const string PrefabPath = "Assets/Arts/Character/Incoming/Pirate_Meshy/low/Processed/Pirate_Low_QGenericRigV2.prefab";
        private const string ControllerPath = "Assets/Arts/Character/Incoming/Pirate_Meshy/low/Processed/Pirate_Low_QGenericRigV2.controller";

        [MenuItem("CubeTD/Art/Process Meshy Pirate Low V2")]
        public static void Process()
        {
            ConfigureModelImporter();
            AnimatorController controller = CreateAnimatorController();
            CreatePrefab(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Processed Meshy pirate low V2 prefab: {PrefabPath}");
        }

        public static void ProcessBatch()
        {
            Process();
        }

        private static void ConfigureModelImporter()
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
            {
                Debug.LogError($"ModelImporter not found: {FbxPath}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.animationRotationError = 0.25f;
            importer.animationPositionError = 0.25f;
            importer.animationScaleError = 0.25f;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.SaveAndReimport();
        }

        private static AnimatorController CreateAnimatorController()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath) ?? string.Empty);

            if (File.Exists(ControllerPath))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Walk", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                .ToArray();

            AnimatorState idle = null;
            AnimatorState walk = null;

            foreach (AnimationClip clip in clips)
            {
                string stateName = StripRigPrefix(clip.name);
                AnimatorState state = stateMachine.AddState(stateName);
                state.motion = clip;
                state.writeDefaultValues = true;

                if (stateName == "Idle")
                {
                    idle = state;
                }
                else if (stateName == "Walk")
                {
                    walk = state;
                }
            }

            if (idle != null)
            {
                stateMachine.defaultState = idle;
            }

            if (idle != null && walk != null)
            {
                AnimatorStateTransition toWalk = idle.AddTransition(walk);
                toWalk.hasExitTime = false;
                toWalk.duration = 0.08f;
                toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walk");

                AnimatorStateTransition toIdle = walk.AddTransition(idle);
                toIdle.hasExitTime = false;
                toIdle.duration = 0.08f;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walk");
            }

            AddAnyStateTrigger(stateMachine, "Attack");
            AddAnyStateTrigger(stateMachine, "Hit");
            AddAnyStateTrigger(stateMachine, "Die");

            return controller;
        }

        private static void AddAnyStateTrigger(AnimatorStateMachine stateMachine, string stateName)
        {
            AnimatorState target = stateMachine.states.Select(child => child.state).FirstOrDefault(state => state.name == stateName);
            AnimatorState idle = stateMachine.states.Select(child => child.state).FirstOrDefault(state => state.name == "Idle");

            if (target == null)
            {
                return;
            }

            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(target);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0, stateName);

            if (idle != null && stateName != "Die")
            {
                AnimatorStateTransition back = target.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.92f;
                back.duration = 0.08f;
            }
        }

        private static void CreatePrefab(RuntimeAnimatorController controller)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (model == null)
            {
                Debug.LogError($"Model not found: {FbxPath}");
                return;
            }

            if (material == null)
            {
                Debug.LogError($"Material not found: {MaterialPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Pirate_Low_QGenericRigV2";

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }

        private static string StripRigPrefix(string clipName)
        {
            int separator = clipName.LastIndexOf('|');
            return separator >= 0 && separator + 1 < clipName.Length ? clipName[(separator + 1)..] : clipName;
        }
    }
}
