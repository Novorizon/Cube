using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UI
{
    public abstract class UIView : MonoBehaviour
    {
        readonly List<IDisposable> disposables = new List<IDisposable>();

        CancellationTokenSource openCancellation;
        bool created;
        bool destroyed;

        public bool IsOpen { get; private set; }
        public bool IsDestroyed => destroyed;
        public virtual UICloseTriggers CloseTriggers => UICloseTriggers.None;

        internal int InstanceId { get; set; }
        internal int InstanceVersion { get; set; }
        internal int LastOpenFrame { get; private set; }

        protected CancellationToken OpenCancellationToken => openCancellation != null ? openCancellation.Token : CancellationToken.None;

        internal void InternalOnCreate()
        {
            if (created)
            {
                return;
            }

            created = true;
            OnCreate();
        }

        internal void InternalOnOpen(object args)
        {
            CancelOpenToken();
            openCancellation = new CancellationTokenSource();
            IsOpen = true;
            LastOpenFrame = Time.frameCount;
            OnOpen(args);
        }

        internal void InternalOnClose()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            CancelOpenToken();
            DisposeRegisteredDisposables();
            OnClose();
        }

        protected void RegisterDisposable(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            disposables.Add(disposable);
        }

        public bool CanCloseBy(UICloseReason reason)
        {
            UICloseTriggers trigger = UICloseTriggerUtility.ToTrigger(reason);
            return trigger != UICloseTriggers.None &&
                   (CloseTriggers & trigger) != 0 &&
                   CanClose(reason);
        }

        protected virtual bool CanClose(UICloseReason reason)
        {
            return true;
        }

        void OnDestroy()
        {
            if (destroyed)
            {
                return;
            }

            destroyed = true;

            if (IsOpen)
            {
                IsOpen = false;
                CancelOpenToken();
                DisposeRegisteredDisposables();
                OnClose();
            }

            OnDestroyed();
        }

        void CancelOpenToken()
        {
            if (openCancellation == null)
            {
                return;
            }

            if (!openCancellation.IsCancellationRequested)
            {
                openCancellation.Cancel();
            }

            openCancellation.Dispose();
            openCancellation = null;
        }

        void DisposeRegisteredDisposables()
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    disposables[i]?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            disposables.Clear();
        }

        protected virtual void OnCreate()
        {
        }

        protected virtual void OnOpen(object args)
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
