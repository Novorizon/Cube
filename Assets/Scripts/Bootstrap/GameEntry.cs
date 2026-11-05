using System;
using Game.Bootstrap;
using UnityEngine;

namespace Game
{
    [DisallowMultipleComponent]
    public class GameEntry : MonoBehaviour
    {
        [SerializeField] private int startupWorldMapId = 1001;

        private static GameApplication activeApplication;
        private GameApplication application;

        public GameApplicationState State => application?.State ?? GameApplicationState.Created;
        public Exception LastError => application?.LastError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetApplicationOwner()
        {
            activeApplication = null;
        }

        private void Awake()
        {
            if (activeApplication != null && activeApplication.State != GameApplicationState.Stopped &&
                activeApplication.State != GameApplicationState.Failed)
            {
                Debug.LogError("A game application is already active. The duplicate GameEntry is disabled.", this);
                enabled = false;
                return;
            }

            application = new GameApplication(new CubeGameLifecycle(startupWorldMapId), error => Debug.LogException(error));
            activeApplication = application;
        }

        // async void is confined to the Unity event boundary; startup faults are observed here.
        private async void Start()
        {
            if (application == null)
            {
                return;
            }

            try
            {
                await application.StartAsync();
            }
            catch (OperationCanceledException) when (application.State == GameApplicationState.Stopping ||
                                                     application.State == GameApplicationState.Stopped)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private void Update()
        {
            application?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            RequestStop();
        }

        private void OnDestroy()
        {
            RequestStop();
        }

        private void RequestStop()
        {
            if (application != null)
            {
                // Unity cannot await these callbacks. Stop disables Tick synchronously and
                // observes cleanup errors itself; pending startup settles before cleanup runs.
                _ = application.StopAsync();
            }
        }
    }
}


