using UnityEngine;

namespace UI
{
    public abstract class UIView : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        internal int InstanceId { get; set; }

        internal void InternalOnCreate()
        {
            OnCreate();
        }

        internal void InternalOnOpen(object? args)
        {
            IsOpen = true;
            OnOpen(args);
        }

        internal void InternalOnClose()
        {
            IsOpen = false;
            OnClose();
        }

        internal void InternalOnDestroyed()
        {
            OnDestroyed();
        }

        protected virtual void OnCreate()
        {
        }

        protected virtual void OnOpen(object? args)
        {
        }

        protected virtual void OnClose()
        {
        }

        protected virtual void OnDestroyed()
        {
        }
    }
}
