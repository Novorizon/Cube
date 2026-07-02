#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    public static class WorldPlayerAssetBuilder
    {
        private const string SourceRoot = "Assets/Arts/Character/Player/Meshy_AI_Forestbound_Adventure_biped";
        private const string CharacterFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Character_output.fbx";
        private const string BaseTexturePath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_texture_0.png";
        private const string NormalTexturePath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_texture_0_normal.png";
        private const string MetallicTexturePath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_texture_0_metallic.png";

        private const string PlayerAssetRoot = "Assets/Arts/Character/Player";
        private const string PrefabFolder = PlayerAssetRoot + "/Prefabs";
        private const string MaterialFolder = PlayerAssetRoot + "/Materials";
        private const string AnimatorFolder = PlayerAssetRoot + "/Animators";
        private const string MaterialPath = MaterialFolder + "/WorldPlayer_Forestbound.mat";
        private const string AnimatorControllerPath = AnimatorFolder + "/WorldPlayer.controller";

        private const string IdleFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Animation_Idle_5_withSkin.fbx";
        private const string WalkFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Animation_Walking_withSkin.fbx";
        private const string RunFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Animation_Running_withSkin.fbx";
        private const string PickUpFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Animation_Male_Bend_Over_Pick_Up_withSkin.fbx";
        private const string UseToolFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Animation_Heavy_Hammer_Swing_withSkin.fbx";
        private const string PullFbxPath = SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_Animation_Pull_Radish_withSkin.fbx";

        [MenuItem("Debug/World/Build Player Character Assets")]
        public static void BuildPlayerCharacterAssets()
        {
            EnsureFolders();
            ConfigureModelImporter(CharacterFbxPath, false);
            ConfigureAnimationImporter(IdleFbxPath, true, "Idle");
            ConfigureAnimationImporter(WalkFbxPath, true, "Walk");
            ConfigureAnimationImporter(RunFbxPath, true, "Run");
            ConfigureAnimationImporter(PickUpFbxPath, false, "PickUp");
            ConfigureAnimationImporter(UseToolFbxPath, false, "UseTool");
            ConfigureAnimationImporter(PullFbxPath, false, "Pull");
            ConfigureTextureImporters();

            Material material = CreateOrUpdateMaterial();
            AnimatorController controller = CreateOrUpdateAnimatorController();
            CreateOrUpdatePrefab(material, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldPlayerAssetBuilder] Built player assets. Prefab: {WorldPlayerView.PrefabPath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(PlayerAssetRoot, "Prefabs");
            EnsureFolder(PlayerAssetRoot, "Materials");
            EnsureFolder(PlayerAssetRoot, "Animators");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureModelImporter(string path, bool importAnimation)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[WorldPlayerAssetBuilder] Missing model importer: {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = importAnimation;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureAnimationImporter(string path, bool loop, string clipName)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[WorldPlayerAssetBuilder] Missing animation importer: {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.optimizeGameObjects = false;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = clipName;
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                    clips[i].lockRootRotation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporters()
        {
            ConfigureTextureImporter(BaseTexturePath, TextureImporterType.Default, true, 2048);
            ConfigureTextureImporter(NormalTexturePath, TextureImporterType.NormalMap, false, 2048);
            ConfigureTextureImporter(MetallicTexturePath, TextureImporterType.Default, false, 1024);
            ConfigureTextureImporter(SourceRoot + "/Meshy_AI_Forestbound_Adventure_biped_texture_0_roughness.png", TextureImporterType.Default, false, 1024);
        }

        private static void ConfigureTextureImporter(string path, TextureImporterType type, bool sRGB, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[WorldPlayerAssetBuilder] Missing texture importer: {path}");
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            Texture2D baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexturePath);
            Texture2D normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexturePath);
            Texture2D metallicTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicTexturePath);

            SetTexture(material, "_BaseMap", "_MainTex", baseTexture);
            SetTexture(material, "_BumpMap", "_BumpMap", normalTexture);
            SetTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", metallicTexture);
            SetFloat(material, "_Metallic", 0.05f);
            SetFloat(material, "_Smoothness", 0.35f);
            SetFloat(material, "_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetTexture(Material material, string urpName, string fallbackName, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty(urpName))
            {
                material.SetTexture(urpName, texture);
            }
            else if (material.HasProperty(fallbackName))
            {
                material.SetTexture(fallbackName, texture);
            }
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material != null && material.HasProperty(name))
            {
                material.SetFloat(name, value);
            }
        }

        private static AnimatorController CreateOrUpdateAnimatorController()
        {
            if (File.Exists(AnimatorControllerPath))
            {
                AssetDatabase.DeleteAsset(AnimatorControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("PickUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("UseTool", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Pull", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimationClip idle = LoadClip(IdleFbxPath, "Idle");
            AnimationClip walk = LoadClip(WalkFbxPath, "Walk");
            AnimationClip run = LoadClip(RunFbxPath, "Run");
            AnimationClip pickUp = LoadClip(PickUpFbxPath, "PickUp");
            AnimationClip useTool = LoadClip(UseToolFbxPath, "UseTool");
            AnimationClip pull = LoadClip(PullFbxPath, "Pull");

            AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250f, 80f, 0f));
            idleState.motion = idle;
            stateMachine.defaultState = idleState;

            AnimatorState walkState = stateMachine.AddState("Walk", new Vector3(250f, 180f, 0f));
            walkState.motion = walk != null ? walk : run;

            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.12f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "MoveSpeed");

            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.12f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "MoveSpeed");

            AddActionState(stateMachine, "PickUp", pickUp, "PickUp", idleState, new Vector3(530f, 40f, 0f));
            AddActionState(stateMachine, "UseTool", useTool, "UseTool", idleState, new Vector3(530f, 140f, 0f));
            AddActionState(stateMachine, "Pull", pull, "Pull", idleState, new Vector3(530f, 240f, 0f));

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddActionState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Motion motion,
            string triggerName,
            AnimatorState returnState,
            Vector3 position)
        {
            if (motion == null)
            {
                return;
            }

            AnimatorState state = stateMachine.AddState(stateName, position);
            state.motion = motion;

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.08f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            AnimatorStateTransition exit = state.AddTransition(returnState);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.12f;
        }

        private static AnimationClip LoadClip(string path, string preferredName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && IsUsableClip(clip, preferredName))
                {
                    return clip;
                }
            }

            assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && IsUsableClip(clip, preferredName))
                {
                    return clip;
                }
            }

            Debug.LogWarning($"[WorldPlayerAssetBuilder] Missing animation clip. path: {path}, preferredName: {preferredName}");
            return null;
        }

        private static bool IsUsableClip(AnimationClip clip, string preferredName)
        {
            if (clip == null || clip.name.StartsWith("__", StringComparison.Ordinal))
            {
                return false;
            }

            return clip.name.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   clip.name.IndexOf("baselayer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CreateOrUpdatePrefab(Material material, RuntimeAnimatorController controller)
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"[WorldPlayerAssetBuilder] Missing character model: {CharacterFbxPath}");
                return;
            }

            GameObject root = new GameObject("WorldPlayer");
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;

            WorldPlayerView view = root.AddComponent<WorldPlayerView>();
            GameObject model = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (model == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                Debug.LogError("[WorldPlayerAssetBuilder] Instantiate character model failed.");
                return;
            }

            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            ApplyMaterial(model, material);

            Transform rightSocket = EnsureSocket(model.transform, "RightHand", "RightHandToolSocket", new Vector3(0.03f, -0.02f, 0.06f), new Vector3(0f, 90f, 90f));
            Transform leftSocket = EnsureSocket(model.transform, "LeftHand", "LeftHandToolSocket", new Vector3(-0.03f, -0.02f, 0.06f), new Vector3(0f, -90f, -90f));
            Transform backSocket = EnsureSocket(model.transform, "Spine02", "BackToolSocket", new Vector3(0f, 0.05f, -0.12f), new Vector3(0f, 0f, 35f));

            SerializedObject serializedView = new SerializedObject(view);
            serializedView.FindProperty("animator").objectReferenceValue = animator;
            serializedView.FindProperty("rightHandToolSocket").objectReferenceValue = rightSocket;
            serializedView.FindProperty("leftHandToolSocket").objectReferenceValue = leftSocket;
            serializedView.FindProperty("backToolSocket").objectReferenceValue = backSocket;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, WorldPlayerView.PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ApplyMaterial(GameObject root, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Transform EnsureSocket(Transform root, string boneName, string socketName, Vector3 localPosition, Vector3 localEuler)
        {
            Transform parent = FindDeepChild(root, boneName);
            if (parent == null)
            {
                parent = root;
                Debug.LogWarning($"[WorldPlayerAssetBuilder] Bone not found: {boneName}. Socket {socketName} attached to model root.");
            }

            Transform existing = parent.Find(socketName);
            GameObject socket = existing != null ? existing.gameObject : new GameObject(socketName);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = Quaternion.Euler(localEuler);
            socket.transform.localScale = Vector3.one;
            return socket.transform;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindDeepChild(root.GetChild(i), name);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
#endif
