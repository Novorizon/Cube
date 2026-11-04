using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Bootstrap
{
    /// <summary>
    /// One application lifetime. Call StartAsync, Tick and StopAsync on the owning context
    /// (the Unity main thread in a player). A new lifetime requires a new instance.
    /// </summary>
    public sealed class GameApplication
    {
        private readonly IGameLifecycle lifecycle;
        private readonly Action<Exception> reportError;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CancellationToken startupToken;
        private TaskCompletionSource<bool> startup;
        private TaskCompletionSource<bool> stopping;
        private GameShutdownReason shutdownReason;
        private bool initializing;
        private bool ticking;
        private bool canceling;
        private bool shutdownStarted;

        public GameApplication(IGameLifecycle lifecycle, Action<Exception> reportError = null)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.reportError = reportError;
            startupToken = cancellation.Token;
        }

        public GameApplicationState State { get; private set; } = GameApplicationState.Created;
        public Exception LastError { get; private set; }

        /// <summary>Completes after startup, or after rollback if startup fails. Never restarts a stopped instance.</summary>
        public Task StartAsync()
        {
            if (State == GameApplicationState.Starting || State == GameApplicationState.Running)
            {
                return startup.Task;
            }

            if (State != GameApplicationState.Created)
            {
                throw new InvalidOperationException("A stopped or failed game application cannot be restarted.");
            }

            startup = NewCompletion();
            initializing = true;
            State = GameApplicationState.Starting;
            _ = InitializeAsync();
            return startup.Task;
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (State != GameApplicationState.Running)
            {
                return;
            }

            try
            {
                ticking = true;
                lifecycle.Tick(deltaTime, unscaledDeltaTime);
            }
            catch (Exception error)
            {
                RecordError(error);
                RequestStop(GameShutdownReason.RuntimeFailure);
                Report(error);
            }
            finally
            {
                ticking = false;
                if (stopping != null)
                {
                    CompleteShutdown();
                }
            }
        }

        /// <summary>
        /// Stops Tick immediately. Cleanup waits for in-flight initialization to settle, even
        /// when an underlying API cannot cancel. Cleanup errors are reported through LastError/reportError.
        /// </summary>
        public Task StopAsync()
        {
            return RequestStop(GameShutdownReason.Requested);
        }

        private async Task InitializeAsync()
        {
            Exception startupError = null;
            bool canceled = false;
            try
            {
                await lifecycle.InitializeAsync(startupToken);
                startupToken.ThrowIfCancellationRequested();
                State = GameApplicationState.Running;
            }
            catch (OperationCanceledException) when (startupToken.IsCancellationRequested)
            {
                canceled = true;
            }
            catch (Exception error)
            {
                startupError = error;
                RecordError(error);
            }
            finally
            {
                initializing = false;
                if (startupError != null)
                {
                    _ = RequestStop(GameShutdownReason.StartupFailure);
                }
                else if (canceled)
                {
                    _ = RequestStop(GameShutdownReason.Requested);
                }
            }

            if (startupError != null)
            {
                startup.TrySetException(startupError);
            }
            else if (canceled)
            {
                startup.TrySetCanceled(startupToken);
            }
            else
            {
                startup.TrySetResult(true);
            }
        }

        private Task RequestStop(GameShutdownReason reason)
        {
            if (stopping == null)
            {
                stopping = NewCompletion();
                shutdownReason = reason;
                State = GameApplicationState.Stopping;
                try
                {
                    canceling = true;
                    cancellation.Cancel();
                }
                catch (Exception error)
                {
                    RecordError(error);
                    Report(error);
                }
                finally
                {
                    canceling = false;
                }
            }
            else if (!shutdownStarted && reason != GameShutdownReason.Requested)
            {
                shutdownReason = reason;
            }

            CompleteShutdown();

            return stopping.Task;
        }

        private void CompleteShutdown()
        {
            if (shutdownStarted || initializing || ticking || canceling)
            {
                return;
            }

            shutdownStarted = true;
            try
            {
                if (startup != null)
                {
                    lifecycle.Shutdown(shutdownReason);
                }
            }
            catch (Exception error)
            {
                RecordError(error);
                Report(error);
            }
            finally
            {
                cancellation.Dispose();
                State = LastError == null ? GameApplicationState.Stopped : GameApplicationState.Failed;
                stopping.TrySetResult(true);
            }
        }

        private void RecordError(Exception error)
        {
            LastError = LastError == null ? error : new AggregateException(LastError, error);
        }

        private void Report(Exception error)
        {
            // A failing diagnostic callback must not interrupt cleanup.
            try
            {
                reportError?.Invoke(error);
            }
            catch
            {
            }
        }

        private static TaskCompletionSource<bool> NewCompletion()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
