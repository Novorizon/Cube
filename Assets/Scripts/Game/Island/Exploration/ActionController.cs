using System;
using UnityEngine;

namespace Game
{
    public enum ActionId
    {
        None,
        Pickup,
        Gather,
        Mine,
        CultivateFarm,
    }

    public enum ActionStopReason
    {
        None,
        UserInput,
        Movement,
        TargetInvalid,
        Replaced,
        Completed,
        StartFailed,
        Disabled,
    }

    public enum ActionExitMode
    {
        KeepPlayback,
        ToIdle,
        ToMove,
    }

    internal enum ActionPlaybackType
    {
        None,
        Pickup,
        Tool,
    }

    public readonly struct ActionRequest
    {
        private ActionRequest(
            ActionId id,
            ActionPlaybackType playbackType,
            ToolKitActionType toolAction,
            float timeoutSeconds,
            float markerNormalizedTime)
        {
            Id = id;
            PlaybackType = playbackType;
            ToolAction = toolAction;
            TimeoutSeconds = Mathf.Max(0.01f, timeoutSeconds);
            MarkerNormalizedTime = Mathf.Clamp01(markerNormalizedTime);
        }

        public ActionId Id { get; }
        public float TimeoutSeconds { get; }
        public float MarkerNormalizedTime { get; }
        internal ActionPlaybackType PlaybackType { get; }
        internal ToolKitActionType ToolAction { get; }

        public static ActionRequest Pickup(float markerNormalizedTime, float timeoutSeconds)
        {
            return new ActionRequest(
                ActionId.Pickup,
                ActionPlaybackType.Pickup,
                ToolKitActionType.None,
                timeoutSeconds,
                markerNormalizedTime);
        }

        public static ActionRequest Tool(
            ActionId id,
            ToolKitActionType toolAction,
            float markerNormalizedTime,
            float timeoutSeconds)
        {
            return new ActionRequest(
                id,
                ActionPlaybackType.Tool,
                toolAction,
                timeoutSeconds,
                markerNormalizedTime);
        }
    }

    public readonly struct ActionCallbacks
    {
        public ActionCallbacks(
            Action onMarker = null,
            Action onCompleted = null,
            Action<ActionStopReason> onStopped = null)
        {
            OnMarker = onMarker;
            OnCompleted = onCompleted;
            OnStopped = onStopped;
        }

        public Action OnMarker { get; }
        public Action OnCompleted { get; }
        public Action<ActionStopReason> OnStopped { get; }

        public static ActionCallbacks AtMarker(Action callback)
        {
            return new ActionCallbacks(onMarker: callback);
        }

        public static ActionCallbacks WhenCompleted(Action callback)
        {
            return new ActionCallbacks(onCompleted: callback);
        }
    }

    public sealed class ActionController
    {
        private readonly Func<WorldPlayerView> viewProvider;
        private ActionRequest currentRequest;
        private ActionCallbacks currentCallbacks;
        private float timeoutAtTime;
        private bool markerFired;
        private bool playbackObserved;
        private int actionVersion;
        private int startedFrame = -1;

        public ActionController(Func<WorldPlayerView> viewProvider)
        {
            this.viewProvider = viewProvider;
        }

        public bool IsRunning { get; private set; }
        public ActionId CurrentActionId => IsRunning ? currentRequest.Id : ActionId.None;

        public bool TryStart(ActionRequest request, ActionCallbacks callbacks = default)
        {
            if (IsRunning || request.Id == ActionId.None)
            {
                return false;
            }

            WorldPlayerView view = viewProvider?.Invoke();
            if (view == null)
            {
                return false;
            }

            if (!view.TryPlayAction(request.PlaybackType, request.ToolAction))
            {
                return false;
            }

            actionVersion++;
            currentRequest = request;
            currentCallbacks = callbacks;
            timeoutAtTime = Time.time + request.TimeoutSeconds;
            markerFired = false;
            playbackObserved = false;
            startedFrame = Time.frameCount;
            IsRunning = true;
            return true;
        }

        public bool Stop(ActionStopReason reason, ActionExitMode exitMode)
        {
            bool stopped = IsRunning;
            Action<ActionStopReason> stoppedCallback = currentCallbacks.OnStopped;
            actionVersion++;
            ResetState();
            ApplyExitMode(exitMode);
            if (stopped)
            {
                stoppedCallback?.Invoke(reason);
            }

            return stopped;
        }

        public void Tick()
        {
            if (!IsRunning)
            {
                return;
            }

            if (Time.frameCount == startedFrame)
            {
                return;
            }

            TickPlayback();
        }

        private void TickPlayback()
        {
            int version = actionVersion;
            WorldPlayerView view = viewProvider?.Invoke();
            bool timedOut = Time.time >= timeoutAtTime;
            float normalizedTime = 0f;
            bool hasProgress = view != null &&
                               view.TryGetActionNormalizedTime(currentRequest.PlaybackType, out normalizedTime);
            if (hasProgress)
            {
                playbackObserved = true;
            }

            bool playbackFinished = playbackObserved && !hasProgress;

            if (!markerFired &&
                (timedOut || playbackFinished ||
                 hasProgress && normalizedTime >= currentRequest.MarkerNormalizedTime))
            {
                markerFired = true;
                currentCallbacks.OnMarker?.Invoke();
                if (!IsRunning || actionVersion != version)
                {
                    return;
                }
            }

            if (timedOut || playbackFinished || hasProgress && normalizedTime >= 1f)
            {
                Complete(timedOut);
            }
        }

        private void Complete(bool timedOut)
        {
            ActionRequest completedRequest = currentRequest;
            Action completedCallback = currentCallbacks.OnCompleted;
            actionVersion++;
            ResetState();
            viewProvider?.Invoke()?.CompleteActionPlayback(completedRequest.PlaybackType, timedOut);
            completedCallback?.Invoke();
        }

        private void ApplyExitMode(ActionExitMode exitMode)
        {
            WorldPlayerView view = viewProvider?.Invoke();
            switch (exitMode)
            {
                case ActionExitMode.ToIdle:
                    view?.CancelActionPlayback(false);
                    break;

                case ActionExitMode.ToMove:
                    view?.CancelActionPlayback(true);
                    break;
            }
        }

        private void ResetState()
        {
            IsRunning = false;
            currentRequest = default;
            currentCallbacks = default;
            timeoutAtTime = 0f;
            markerFired = false;
            playbackObserved = false;
            startedFrame = -1;
        }
    }
}
