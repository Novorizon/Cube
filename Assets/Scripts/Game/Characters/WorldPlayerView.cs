using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class WorldPlayerView : MonoBehaviour
    {
        public const string PrefabPath = "Assets/Arts/Character/Player/Prefabs/WorldPlayer.prefab";
        private const string ToolPrefabRoot = "Assets/Arts/Character/Player/Tools/Prefabs/";
        private const string UseToolLayerName = "UseTool Upper Body";

        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int PickUpHash = Animator.StringToHash("PickUp");
        private static readonly int UseToolHash = Animator.StringToHash("UseTool");
        private static readonly int PullHash = Animator.StringToHash("Pull");
        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int PickUpStateHash = Animator.StringToHash("Base Layer.PickUp");
        private static readonly int PickUpShortStateHash = Animator.StringToHash("PickUp");
        private static readonly int UseToolEmptyStateHash = Animator.StringToHash("UseTool Upper Body.Empty");
        private static readonly int UseToolStateHash = Animator.StringToHash("UseTool Upper Body.UseTool");
        private static readonly int UseToolShortStateHash = Animator.StringToHash("UseTool");

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
        private int useToolLayerIndex = -1;
        private bool hasAnimatorRootTransform;
        private Vector3 animatorRootLocalPosition;
        private Quaternion animatorRootLocalRotation;

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

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animatorRootLocalPosition = animator.transform.localPosition;
                animatorRootLocalRotation = animator.transform.localRotation;
                hasAnimatorRootTransform = true;
                useToolLayerIndex = animator.GetLayerIndex(UseToolLayerName);
                SetUseToolLayerWeight(0f);
            }

            CacheAnimatorParameters();
        }

        private void LateUpdate()
        {
            if (!hasAnimatorRootTransform || animator == null)
            {
                return;
            }

            animator.transform.localPosition = animatorRootLocalPosition;
            animator.transform.localRotation = animatorRootLocalRotation;
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

        internal bool TryPlayAction(ActionPlaybackType playbackType, ToolKitActionType toolAction)
        {
            switch (playbackType)
            {
                case ActionPlaybackType.Pickup:
                    return TryPlayPickUp();

                case ActionPlaybackType.Tool:
                    return TryPlayToolAction(toolAction);

                default:
                    return false;
            }
        }

        internal bool TryGetActionNormalizedTime(
            ActionPlaybackType playbackType,
            out float normalizedTime)
        {
            switch (playbackType)
            {
                case ActionPlaybackType.Pickup:
                    return TryGetStateNormalizedTime(
                        0,
                        PickUpStateHash,
                        PickUpShortStateHash,
                        out normalizedTime);

                case ActionPlaybackType.Tool:
                    return TryGetStateNormalizedTime(
                        useToolLayerIndex,
                        UseToolStateHash,
                        UseToolShortStateHash,
                        out normalizedTime);

                default:
                    normalizedTime = 0f;
                    return false;
            }
        }

        internal void CompleteActionPlayback(ActionPlaybackType playbackType, bool timedOut)
        {
            ResetActionTriggers();
            if (playbackType == ActionPlaybackType.Tool)
            {
                HideTool();
                return;
            }

            if (timedOut && playbackType == ActionPlaybackType.Pickup)
            {
                CrossFadeToLocomotion(false);
            }
        }

        internal void CancelActionPlayback(bool keepMoving)
        {
            HideTool();
            ResetActionTriggers();
            CrossFadeToLocomotion(keepMoving);
        }

        private bool TryPlayPickUp()
        {
            if (animator == null || !hasPickUp || !animator.HasState(0, PickUpStateHash))
            {
                return false;
            }

            HideTool();
            animator.ResetTrigger(PickUpHash);
            animator.SetTrigger(PickUpHash);
            return true;
        }

        private bool TryPlayToolAction(ToolKitActionType actionType)
        {
            if (animator == null ||
                actionType == ToolKitActionType.None ||
                !hasUseTool ||
                useToolLayerIndex < 0 ||
                !animator.HasState(useToolLayerIndex, UseToolEmptyStateHash) ||
                !animator.HasState(useToolLayerIndex, UseToolStateHash))
            {
                return false;
            }

            ToolType toolType = ActionToolResolver.GetRequiredTool(actionType);
            if (toolType == ToolType.None || !TryShowTool(toolType))
            {
                return false;
            }

            SetUseToolLayerWeight(1f);
            ResetUseToolState();
            animator.ResetTrigger(UseToolHash);
            animator.SetTrigger(UseToolHash);
            return true;
        }

        private bool TryShowTool(ToolType toolType)
        {
            if (toolType == ToolType.None)
            {
                HideTool();
                return false;
            }

            if (currentTool != null && currentToolType == toolType)
            {
                currentTool.SetActive(true);
                return true;
            }

            DestroyCurrentTool();

            string prefabPath = GetToolPrefabPath(toolType);
            GameObject prefab = ResourceManager.Instance.LoadGameObject(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing world player tool prefab. type: {toolType}, path: {prefabPath}");
                return false;
            }

            Transform socket = GetToolSocket(toolType);
            currentTool = Instantiate(prefab, socket != null ? socket : transform);
            currentTool.name = $"{prefab.name}_Runtime";
            currentTool.transform.localPosition = Vector3.zero;
            currentTool.transform.localRotation = Quaternion.identity;
            currentTool.transform.localScale = Vector3.one;
            currentToolType = toolType;
            return true;
        }

        private void HideTool()
        {
            if (currentTool != null)
            {
                currentTool.SetActive(false);
            }

            SetUseToolLayerWeight(0f);
            ResetUseToolState();
        }

        private void DestroyCurrentTool()
        {
            if (currentTool != null)
            {
                Destroy(currentTool);
            }

            currentTool = null;
            currentToolType = ToolType.None;
        }

        private void CrossFadeToLocomotion(bool keepMoving)
        {
            if (animator == null)
            {
                return;
            }

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

        private void SetUseToolLayerWeight(float weight)
        {
            if (animator != null && useToolLayerIndex >= 0)
            {
                animator.SetLayerWeight(useToolLayerIndex, weight);
            }
        }

        private bool TryGetStateNormalizedTime(
            int layerIndex,
            int fullPathHash,
            int shortNameHash,
            out float normalizedTime)
        {
            normalizedTime = 0f;
            if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
            {
                return false;
            }

            if (animator.IsInTransition(layerIndex))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
                if (IsState(nextState, fullPathHash, shortNameHash))
                {
                    normalizedTime = nextState.normalizedTime;
                    return true;
                }
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
            if (!IsState(currentState, fullPathHash, shortNameHash))
            {
                return false;
            }

            normalizedTime = currentState.normalizedTime;
            return true;
        }

        private void ResetUseToolState()
        {
            if (animator != null &&
                useToolLayerIndex >= 0 &&
                animator.HasState(useToolLayerIndex, UseToolEmptyStateHash))
            {
                animator.Play(UseToolEmptyStateHash, useToolLayerIndex, 0f);
            }
        }

        private static bool IsState(AnimatorStateInfo stateInfo, int fullPathHash, int shortNameHash)
        {
            return stateInfo.fullPathHash == fullPathHash || stateInfo.shortNameHash == shortNameHash;
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
