using System.Collections;
using UnityEngine;

namespace UI
{
    public abstract class UIToast : UIView
    {
        ToastManager? manager;

        public virtual float Duration => 2.0f;

        internal void Bind(ToastManager owner)
        {
            manager = owner;
        }

        protected void Complete()
        {
            if (manager == null)
            {
                return;
            }

            manager.NotifyToastCompleted(this);
        }

        protected override void OnOpen(object? args)
        {
            float duration = Duration;
            if (duration > 0f)
            {
                StartCoroutine(AutoComplete(duration));
            }
        }

        IEnumerator AutoComplete(float duration)
        {
            yield return new WaitForSeconds(duration);
            Complete();
        }
    }
}
