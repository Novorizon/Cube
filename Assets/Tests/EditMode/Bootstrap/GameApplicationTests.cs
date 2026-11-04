using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Bootstrap;
using NUnit.Framework;

namespace Game.Tests.Bootstrap
{
    public sealed class GameApplicationTests
    {
        [Test]
        public async Task StartupGate_BlocksTickUntilReady()
        {
            var gate = new TaskCompletionSource<bool>();
            var project = new TestLifecycle { Initialize = token => gate.Task };
            var application = new GameApplication(project);
            Task start = application.StartAsync();
            application.Tick(1f, 1f);
            Assert.That(project.Ticks, Is.Zero);
            gate.SetResult(true);
            await start;
            application.Tick(1f, 1f);
            Assert.That(project.Ticks, Is.EqualTo(1));
            await application.StopAsync();
            application.Tick(1f, 1f);
            Assert.That(project.Ticks, Is.EqualTo(1));
        }

        [Test]
        public async Task StopDuringUncancelableStartup_WaitsThenCleansOnce()
        {
            var gate = new TaskCompletionSource<bool>();
            var project = new TestLifecycle { Initialize = token => gate.Task };
            var application = new GameApplication(project);
            Task start = application.StartAsync();
            Task stop = application.StopAsync();
            Assert.That(stop.IsCompleted, Is.False);
            Assert.That(project.Stops, Is.Zero);
            gate.SetResult(true);
            try
            {
                await start;
                Assert.Fail("Startup must be canceled.");
            }
            catch (OperationCanceledException)
            {
            }

            await stop;
            await application.StopAsync();
            Assert.That(project.Stops, Is.EqualTo(1));
            Assert.That(application.State, Is.EqualTo(GameApplicationState.Stopped));
        }

        [Test]
        public async Task StartupFault_IsObservedAfterRollback()
        {
            var project = new TestLifecycle { Initialize = token => Task.FromException(new InvalidOperationException("failed")) };
            var application = new GameApplication(project);
            try
            {
                await application.StartAsync();
                Assert.Fail("Startup must fail.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(project.Stops, Is.EqualTo(1));
            Assert.That(project.Reason, Is.EqualTo(GameShutdownReason.StartupFailure));
            Assert.That(application.State, Is.EqualTo(GameApplicationState.Failed));
        }

        [Test]
        public void CleanupFault_ReleasesRemainingActionsInReverseOrder()
        {
            var releases = new List<int>();
            var scope = new GameCleanupScope();
            scope.Add(() => releases.Add(1));
            scope.Add(() => throw new InvalidOperationException("failed"));
            scope.Add(() => releases.Add(3));
            Assert.Throws<AggregateException>(() => scope.Dispose());
            scope.Dispose();
            Assert.That(releases, Is.EqualTo(new[] { 3, 1 }));
        }

        private sealed class TestLifecycle : IGameLifecycle
        {
            public Func<CancellationToken, Task> Initialize = token => Task.CompletedTask;
            public int Ticks;
            public int Stops;
            public GameShutdownReason Reason;
            public Task InitializeAsync(CancellationToken token) => Initialize(token);
            public void Tick(float deltaTime, float unscaledDeltaTime) => Ticks++;
            public void Shutdown(GameShutdownReason reason) { Stops++; Reason = reason; }
        }
    }
}
