using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class WorldPlayerView : MonoBehaviour
    {
        public const string PrefabPath = "Assets/Arts/Character/Player/Prefabs/WorldPlayer.prefab";
        private const string ToolPrefabRoot = "Assets/Arts/Character/Player/Tools/Prefabs/";
        private const float ToolVisibleSeconds = 1.1f;

        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int PickUpHash = Animator.StringToHash("PickUp");
        private static readonly int UseToolHash = Animator.StringToHash("UseTool");
        private static readonly int PullHash = Animator.StringToHash("Pull");
        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.Walk");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform rightHandToolSocket;
        [SerializeField] private Transform leftHandToolSocket;
        [SerializeField] private Transform backToolSocket;

        private bool hasMoveSpeed;
        private bool hasIsMoving;
        private bool hasPickUp;
        private bool hasUseTool;
        private bool hasPull;
        private GameObject currentTool;
        private ToolType currentToolType = ToolType.None;
        private float hideToolAtTime = -1f;

        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;
        public Vector3 CameraTargetPosition => CameraTarget.position;
        public Transform RightHandToolSocket => rightHandToolSocket;
        public Transform LeftHandToolSocket => leftHandToolSocket;
        public Transform BackToolSocket => backToolSocket;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            CacheAnimatorParameters();
        }

        private void Update()
        {
            if (hideToolAtTime > 0f && Time.time >= hideToolAtTime)
            {
                HideTool();
            }
        }

        public void SetMoveSpeed(float speed)
        {
            if (animator == null)
            {
                return;
            }

            float normalizedSpeed = Mathf.Max(0f, speed);
            if (hasMoveSpeed)
            {
                animator.SetFloat(MoveSpeedHash, normalizedSpeed);
            }

            if (hasIsMoving)
            {
                animator.SetBool(IsMovingHash, normalizedSpeed > 0.01f);
            }
        }

        public void PlayPickUp()
        {
            HideTool();
            SetTrigger(PickUpHash, hasPickUp);
        }

        public void PlayUseTool()
        {
            SetTrigger(UseToolHash, hasUseTool);
        }

        public void PlayPull()
        {
            SetTrigger(PullHash, hasPull);
        }

        public void PlayToolAction(ToolKitActionType actionType)
        {
            ToolType toolType = ActionToolResolver.GetRequiredTool(actionType);
            ShowTool(toolType);

            switch (actionType)
            {
                case ToolKitActionType.None:
                    return;

                default:
                    PlayUseTool();
                    break;
            }

            hideToolAtTime = Time.time + ToolVisibleSeconds;
        }

        public void ShowTool(ToolType toolType)
        {
            if (toolType == ToolType.None)
            {
                HideTool();
                return;
            }

            if (currentTool != null && currentToolType == toolType)
            {
                currentTool.SetActive(true);
                return;
            }

            HideTool();

            string prefabPath = GetToolPrefabPath(toolType);
            GameObject prefab = ResourceManager.Instance.LoadGameObject(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing world player tool prefab. type: {toolType}, path: {prefabPath}");
                return;
            }

            Transform socket = GetToolSocket(toolType);
            currentTool = Instantiate(prefab, socket != null ? socket : transform);
            currentTool.name = $"{prefab.name}_Runtime";
            currentTool.transform.localPosition = Vector3.zero;
            currentTool.transform.localRotation = Quaternion.identity;
            currentTool.transform.localScale = Vector3.one;
            currentToolType = toolType;
            hideToolAtTime = -1f;
        }

        public void HideTool()
        {
            if (currentTool != null)
            {
                Destroy(currentTool);
            }

            currentTool = null;
            currentToolType = ToolType.None;
            hideToolAtTime = -1f;
        }

        public void CancelActionPlayback(bool keepMoving)
        {
            HideTool();
            if (animator == null)
            {
                return;
            }

            ResetActionTriggers();
            int targetState = keepMoving ? WalkStateHash : IdleStateHash;
            if (animator.HasState(0, targetState))
            {
                animator.CrossFade(targetState, 0.08f, 0, 0f);
            }
        }

        private void ResetActionTriggers()
        {
            if (animator == null)
            {
                return;
            }

            if (hasPickUp)
            {
                animator.ResetTrigger(PickUpHash);
            }

            if (hasUseTool)
            {
                animator.ResetTrigger(UseToolHash);
            }

            if (hasPull)
            {
                animator.ResetTrigger(PullHash);
            }
        }

        private void SetTrigger(int hash, bool exists)
        {
            if (animator != null && exists)
            {
                animator.SetTrigger(hash);
            }
        }

        private Transform GetToolSocket(ToolType toolType)
        {
            switch (toolType)
            {
                case ToolType.WateringCan:
                case ToolType.FishingRod:
                case ToolType.Axe:
                case ToolType.Pickaxe:
                case ToolType.Hoe:
                case ToolType.Shovel:
                case ToolType.Hammer:
                    return rightHandToolSocket != null ? rightHandToolSocket : transform;

                default:
                    return transform;
            }
        }

        private static string GetToolPrefabPath(ToolType toolType)
        {
            switch (toolType)
            {
                case ToolType.Axe:
                    return ToolPrefabRoot + "Tool_Axe.prefab";

                case ToolType.Pickaxe:
                    return ToolPrefabRoot + "Tool_Pickaxe.prefab";

                case ToolType.Hoe:
                    return ToolPrefabRoot + "Tool_Hoe.prefab";

                case ToolType.Shovel:
                    return ToolPrefabRoot + "Tool_Shovel.prefab";

                case ToolType.WateringCan:
                    return ToolPrefabRoot + "Tool_WateringCan.prefab";

                case ToolType.FishingRod:
                    return ToolPrefabRoot + "Tool_FishingRod.prefab";

                case ToolType.Hammer:
                    return ToolPrefabRoot + "Tool_Hammer.prefab";

                default:
                    return string.Empty;
            }
        }

        private void CacheAnimatorParameters()
        {
            hasMoveSpeed = false;
            hasIsMoving = false;
            hasPickUp = false;
            hasUseTool = false;
            hasPull = false;

            if (animator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                int hash = parameters[i].nameHash;
                hasMoveSpeed |= hash == MoveSpeedHash;
                hasIsMoving |= hash == IsMovingHash;
                hasPickUp |= hash == PickUpHash;
                hasUseTool |= hash == UseToolHash;
                hasPull |= hash == PullHash;
            }
        }
    }
}
