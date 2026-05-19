using Game.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public sealed class AnimatorManager : Singleton<AnimatorManager>
    {


        public void PlayBoolAnimator(Animator animator, int animatorHash, bool value)
        {
            if (animator != null)
            {
                animator.SetBool(animatorHash, value);
            }
        }


        public void PlayTriggerAnimator(Animator animator, int animatorHash)
        {
            if (animator != null)
            {
                animator.SetTrigger(animatorHash);
            }
        }

        public async Task<bool> PlayTriggerAnimator(Animator animator, int triggerHash, string stateName, float timeoutSeconds = 3f)
        {
            if (animator == null)
            {
                return false;
            }

            animator.SetTrigger(triggerHash);

            float startTime = Time.time;

            await Task.Yield();

            bool enteredState = false;

            while (Time.time - startTime <= timeoutSeconds)
            {
                if (animator == null)
                {
                    return false;
                }

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                if (stateInfo.IsName(stateName))
                {
                    enteredState = true;
                    break;
                }

                await Task.Yield();
            }

            if (!enteredState)
            {
                Debug.LogWarning($"Wait animation enter state timeout. State: {stateName}");
                return false;
            }

            while (Time.time - startTime <= timeoutSeconds)
            {
                if ( animator == null)
                {
                    return false;
                }

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                if (stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
                {
                    return true;
                }

                await Task.Yield();
            }

            Debug.LogWarning($"Wait animation complete timeout. State: {stateName}");
            return false;
        }

    }
}