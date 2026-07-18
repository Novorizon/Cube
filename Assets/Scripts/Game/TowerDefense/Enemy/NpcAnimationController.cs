using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game
{
    internal static class NpcAnimationController
    {
        [System.Flags]
        private enum ParameterFlags
        {
            None = 0,
            Walk = 1 << 0,
            Attack = 1 << 1,
            Die = 1 << 2,
        }

        private const int BaseLayerIndex = 0;
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int DieStateHash = Animator.StringToHash("Base Layer.Die");
        private static readonly int DieShortStateHash = Animator.StringToHash("Die");
        private static readonly Dictionary<int, ParameterFlags> ParameterCache =
            new Dictionary<int, ParameterFlags>();

        public static bool SetWalking(Animator animator, bool value)
        {
            if (!HasParameter(animator, WalkHash, AnimatorControllerParameterType.Bool))
            {
                return false;
            }

            animator.SetBool(WalkHash, value);
            return true;
        }

        public static bool PlayAttack(Animator animator)
        {
            return TrySetTrigger(animator, AttackHash);
        }

        public static async Task<bool> PlayDeathAsync(
            Animator animator,
            CancellationToken cancellationToken,
            float timeoutSeconds = 2f)
        {
            if (!TrySetTrigger(animator, DieHash))
            {
                return false;
            }

            float startedAt = Time.unscaledTime;
            bool enteredState = false;
            await Task.Yield();

            while (Time.unscaledTime - startedAt <= Mathf.Max(0.01f, timeoutSeconds))
            {
                if (cancellationToken.IsCancellationRequested || animator == null)
                {
                    return false;
                }

                if (TryGetDeathProgress(animator, out float normalizedTime))
                {
                    enteredState = true;
                    if (normalizedTime >= 1f && !animator.IsInTransition(BaseLayerIndex))
                    {
                        return true;
                    }
                }
                else if (enteredState)
                {
                    return true;
                }

                await Task.Yield();
            }

            Debug.LogWarning("Wait npc death animation timeout. State: Base Layer.Die");
            return false;
        }

        private static bool TrySetTrigger(Animator animator, int triggerHash)
        {
            if (!HasParameter(animator, triggerHash, AnimatorControllerParameterType.Trigger))
            {
                return false;
            }

            animator.ResetTrigger(triggerHash);
            animator.SetTrigger(triggerHash);
            return true;
        }

        private static bool HasParameter(
            Animator animator,
            int parameterHash,
            AnimatorControllerParameterType parameterType)
        {
            if (animator == null ||
                !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
            {
                return false;
            }

            ParameterFlags requiredFlag = GetParameterFlag(parameterHash, parameterType);
            if (requiredFlag == ParameterFlags.None)
            {
                return false;
            }

            int controllerId = animator.runtimeAnimatorController.GetInstanceID();
            if (!ParameterCache.TryGetValue(controllerId, out ParameterFlags availableFlags))
            {
                availableFlags = CacheParameters(animator);
                ParameterCache[controllerId] = availableFlags;
            }

            return (availableFlags & requiredFlag) != 0;
        }

        private static ParameterFlags CacheParameters(Animator animator)
        {
            ParameterFlags flags = ParameterFlags.None;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                flags |= GetParameterFlag(parameter.nameHash, parameter.type);
            }

            return flags;
        }

        private static ParameterFlags GetParameterFlag(
            int parameterHash,
            AnimatorControllerParameterType parameterType)
        {
            if (parameterHash == WalkHash && parameterType == AnimatorControllerParameterType.Bool)
            {
                return ParameterFlags.Walk;
            }

            if (parameterType == AnimatorControllerParameterType.Trigger)
            {
                if (parameterHash == AttackHash)
                {
                    return ParameterFlags.Attack;
                }

                if (parameterHash == DieHash)
                {
                    return ParameterFlags.Die;
                }
            }

            return ParameterFlags.None;
        }

        private static bool TryGetDeathProgress(Animator animator, out float normalizedTime)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
            if (IsDeathState(currentState))
            {
                normalizedTime = currentState.normalizedTime;
                return true;
            }

            if (animator.IsInTransition(BaseLayerIndex))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayerIndex);
                if (IsDeathState(nextState))
                {
                    normalizedTime = nextState.normalizedTime;
                    return true;
                }
            }

            normalizedTime = 0f;
            return false;
        }

        private static bool IsDeathState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.fullPathHash == DieStateHash ||
                   stateInfo.shortNameHash == DieShortStateHash;
        }
    }
}
