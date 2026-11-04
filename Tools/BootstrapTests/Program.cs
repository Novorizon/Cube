using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Game.Bootstrap;
using Game.Framework;
using Newtonsoft.Json;

internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("Tick waits for successful startup", TickWaitsForStartup),
            ("Start and Stop are idempotent", RepeatedCalls),
            ("Stop before Start touches no project objects", StopBeforeStart),
            ("Uncancelable startup settles before cleanup", StopDuringStartup),
            ("Cooperative startup cancellation", CooperativeCancellation),
            ("Startup failure rolls back before it is observed", StartupFailure),
            ("Runtime failure stops future frames", RuntimeFailure),
            ("Cleanup continues after a failing action", CleanupFailure),
            ("Reentrant stop during initialization", ReentrantInitializationStop),
            ("Reentrant stop waits for the current Tick", ReentrantTickStop),
            ("Cancellation callback can request stop again", ReentrantCancellation),
            ("Failing cancellation callback cannot skip cleanup", FailingCancellation),
            ("Reentrant cancellation failure is reflected in the final state", ReentrantFailingCancellation),
            ("Diagnostic reentrancy preserves runtime failure reason", ReentrantDiagnostics),
            ("Shutdown can request stop again", ReentrantShutdown),
            ("Diagnostic failures cannot break shutdown", FailingDiagnostics),
            ("Cleanup scope rejects late registration", ClosedCleanupScope),
            ("Cube cannot Tick during resource loading", CubeResourceGate),
            ("Destroy during resource loading builds no project objects", CubeResourceCancellation),
            ("Cube waits for world UI before opening the session", CubeUiGate),
            ("Stop during world UI loading preserves the save", CubeUiCancellation),
            ("Missing camera fails and releases input", CubeCameraFailure),
            ("Invalid world UI fails startup", CubeUiFailure),
            ("Missing map fails startup", CubeMapFailure),
            ("Missing map presentation config fails startup", CubeMapConfigFailure),
            ("Corrupt save is never overwritten", CorruptSave),
            ("Incompatible save is never overwritten", IncompatibleSave),
            ("Partial restore failure is not saveable", PartialRestoreFailure),
            ("Saving requires a completed session", SessionSaveGate),
            ("Player cache survives the pre-spawn window", PlayerFallback),
            ("Project cleanup continues after subscription release fails", CubeCleanupFailure),
            ("Final save failure cannot skip project cleanup", FinalSaveFailure),
            ("Unity duplicate entry and repeated exit are harmless", UnityEntryOwnership)
        };

        int failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().WaitAsync(TimeSpan.FromSeconds(5));
                Console.WriteLine("PASS " + test.Name);
            }
            catch (Exception error)
            {
                failures++;
                Console.WriteLine("FAIL " + test.Name + "\n" + error);
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} scenarios passed.");
        return failures == 0 ? 0 : 1;
    }

    private static async Task TickWaitsForStartup()
    {
        var gate = Gate();
        var project = new FakeLifecycle { Initialize = token => gate.Task };
        var app = new GameApplication(project);
        app.Tick(1f, 2f);
        Task start = app.StartAsync();
        app.Tick(1f, 2f);
        Check(project.Ticks == 0 && app.State == GameApplicationState.Starting);
        gate.SetResult(true);
        await start;
        app.Tick(1f, 2f);
        Check(project.Ticks == 1 && project.Delta == 1f && project.UnscaledDelta == 2f);
        await app.StopAsync();
        app.Tick(1f, 2f);
        Check(project.Ticks == 1 && project.Shutdowns == 1 && app.State == GameApplicationState.Stopped);
    }

    private static async Task RepeatedCalls()
    {
        var gate = Gate();
        var project = new FakeLifecycle { Initialize = token => gate.Task };
        var app = new GameApplication(project);
        Task start = app.StartAsync();
        Check(ReferenceEquals(start, app.StartAsync()));
        gate.SetResult(true);
        await start;
        Check(ReferenceEquals(start, app.StartAsync()));
        Task stop = app.StopAsync();
        Check(ReferenceEquals(stop, app.StopAsync()));
        await stop;
        Check(project.Starts == 1 && project.Shutdowns == 1);
        await Throws<InvalidOperationException>(() => app.StartAsync());
    }

    private static async Task StopBeforeStart()
    {
        var project = new FakeLifecycle();
        var app = new GameApplication(project);
        await app.StopAsync();
        Check(project.Starts == 0 && project.Shutdowns == 0);
        await Throws<InvalidOperationException>(() => app.StartAsync());
    }

    private static async Task StopDuringStartup()
    {
        var gate = Gate();
        CancellationToken observed = default;
        var project = new FakeLifecycle { Initialize = token => { observed = token; return gate.Task; } };
        var app = new GameApplication(project);
        Task start = app.StartAsync();
        Task stop = app.StopAsync();
        app.Tick(1f, 1f);
        Check(observed.IsCancellationRequested && !stop.IsCompleted && project.Shutdowns == 0 && project.Ticks == 0);
        gate.SetResult(true);
        await Throws<OperationCanceledException>(() => start);
        await stop;
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Stopped);
    }

    private static async Task CooperativeCancellation()
    {
        var project = new FakeLifecycle { Initialize = token => Task.Delay(Timeout.Infinite, token) };
        var app = new GameApplication(project);
        Task start = app.StartAsync();
        await app.StopAsync();
        await Throws<OperationCanceledException>(() => start);
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Stopped);
    }

    private static async Task StartupFailure()
    {
        var cleanup = new GameCleanupScope();
        var released = new List<int>();
        var failure = new InvalidOperationException("startup failed");
        var project = new FakeLifecycle
        {
            Initialize = token =>
            {
                cleanup.Add(() => released.Add(1));
                cleanup.Add(() => released.Add(2));
                return Task.FromException(failure);
            },
            Release = reason => cleanup.Dispose()
        };
        var app = new GameApplication(project);
        Exception observed = await Throws<InvalidOperationException>(() => app.StartAsync());
        Check(ReferenceEquals(observed, failure) && ReferenceEquals(app.LastError, failure));
        Check(released.SequenceEqual(new[] { 2, 1 }) && project.Reason == GameShutdownReason.StartupFailure);
        Check(app.State == GameApplicationState.Failed);
        await app.StopAsync();
        Check(project.Shutdowns == 1);
    }

    private static async Task RuntimeFailure()
    {
        var reports = new List<Exception>();
        var project = new FakeLifecycle { Frame = () => throw new InvalidOperationException("frame failed") };
        var app = new GameApplication(project, reports.Add);
        await app.StartAsync();
        app.Tick(1f, 1f);
        app.Tick(1f, 1f);
        Check(app.State == GameApplicationState.Failed && project.Ticks == 1 && project.Shutdowns == 1);
        Check(project.Reason == GameShutdownReason.RuntimeFailure && reports.Count == 1);
    }

    private static async Task CleanupFailure()
    {
        var scope = new GameCleanupScope();
        var released = new List<int>();
        scope.Add(() => released.Add(1));
        scope.Add(() => throw new InvalidOperationException("release failed"));
        scope.Add(() => released.Add(3));
        var project = new FakeLifecycle { Release = reason => scope.Dispose() };
        var app = new GameApplication(project);
        await app.StartAsync();
        await app.StopAsync();
        scope.Dispose();
        Check(released.SequenceEqual(new[] { 3, 1 }) && app.LastError is AggregateException);
        Check(app.State == GameApplicationState.Failed);
    }

    private static async Task ReentrantInitializationStop()
    {
        GameApplication app = null;
        var project = new FakeLifecycle { Initialize = token => { _ = app.StopAsync(); return Task.CompletedTask; } };
        app = new GameApplication(project);
        await Throws<OperationCanceledException>(() => app.StartAsync());
        await app.StopAsync();
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Stopped);
    }

    private static async Task ReentrantTickStop()
    {
        GameApplication app = null;
        var project = new FakeLifecycle();
        project.Frame = () =>
        {
            Task stop = app.StopAsync();
            Check(app.State == GameApplicationState.Stopping && !stop.IsCompleted && project.Shutdowns == 0);
        };
        app = new GameApplication(project);
        await app.StartAsync();
        app.Tick(1f, 1f);
        Check(app.State == GameApplicationState.Stopped && project.Shutdowns == 1);
    }

    private static async Task ReentrantCancellation()
    {
        GameApplication app = null;
        var project = new FakeLifecycle { Initialize = token => { token.Register(() => { _ = app.StopAsync(); }); return Task.CompletedTask; } };
        app = new GameApplication(project);
        await app.StartAsync();
        await app.StopAsync();
        Check(project.Shutdowns == 1 && app.LastError == null);
    }

    private static async Task FailingCancellation()
    {
        var project = new FakeLifecycle
        {
            Initialize = token => { token.Register(() => throw new InvalidOperationException("cancel failed")); return Task.CompletedTask; }
        };
        var app = new GameApplication(project);
        await app.StartAsync();
        await app.StopAsync();
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Failed);
    }

    private static async Task ReentrantShutdown()
    {
        GameApplication app = null;
        var project = new FakeLifecycle { Release = reason => { _ = app.StopAsync(); } };
        app = new GameApplication(project);
        await app.StartAsync();
        await app.StopAsync();
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Stopped);
    }

    private static async Task ReentrantFailingCancellation()
    {
        GameApplication app = null;
        var project = new FakeLifecycle
        {
            Initialize = token =>
            {
                token.Register(() => throw new InvalidOperationException("cancel failed"));
                token.Register(() => { _ = app.StopAsync(); });
                return Task.CompletedTask;
            }
        };
        app = new GameApplication(project);
        await app.StartAsync();
        await app.StopAsync();
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Failed && app.LastError != null);
    }

    private static async Task ReentrantDiagnostics()
    {
        GameApplication app = null;
        var project = new FakeLifecycle { Frame = () => throw new InvalidOperationException("frame failed") };
        app = new GameApplication(project, error => { _ = app.StopAsync(); });
        await app.StartAsync();
        app.Tick(1f, 1f);
        Check(project.Reason == GameShutdownReason.RuntimeFailure && project.Shutdowns == 1);
    }

    private static async Task FailingDiagnostics()
    {
        var project = new FakeLifecycle { Frame = () => throw new InvalidOperationException("frame failed") };
        var app = new GameApplication(project, error => throw new InvalidOperationException("logging failed"));
        await app.StartAsync();
        app.Tick(1f, 1f);
        Check(project.Shutdowns == 1 && app.State == GameApplicationState.Failed);
    }

    private static Task ClosedCleanupScope()
    {
        var scope = new GameCleanupScope();
        scope.Dispose();
        return Throws<ObjectDisposedException>(() => { scope.Add(() => { }); return Task.CompletedTask; });
    }

    private static Task CubeResourceGate() => InSession(async () =>
    {
        var gate = Gate();
        Boundary.ResourceTask = gate.Task;
        var app = Cube();
        Task start = app.StartAsync();
        app.Tick(1f, 1f);
        Check(!Boundary.Events.Contains("DataManager:init") && !Boundary.Events.Any(x => x.EndsWith(":tick")));
        gate.SetResult(true);
        await start;
        Check(app.State == GameApplicationState.Running && StorageManager.Instance.SessionReady);
        app.Tick(1f, 1f);
        Check(Boundary.Events.Contains("WorldIncomeManager:tick"));
        await app.StopAsync();
        Check(File.Exists(StorageManager.Instance.SavePath) && !StorageManager.Instance.SessionReady);
    });

    private static Task CubeResourceCancellation() => InSession(async () =>
    {
        var gate = Gate();
        Boundary.ResourceTask = gate.Task;
        var app = Cube();
        Task start = app.StartAsync();
        Task stop = app.StopAsync();
        gate.SetResult(true);
        await Throws<OperationCanceledException>(() => start);
        await stop;
        Check(!Boundary.Events.Contains("DataManager:init") && !File.Exists(StorageManager.Instance.SavePath));
    });

    private static Task CubeUiGate() => InSession(async () =>
    {
        var panel = new TaskCompletionSource<UI.UIHandle>();
        Boundary.PanelTask = panel.Task;
        var app = Cube();
        Task start = app.StartAsync();
        Check(app.State == GameApplicationState.Starting && !StorageManager.Instance.SessionReady);
        Check(GameInputManager.Instance.CurrentMode == InputMode.UI && Boundary.Events.Contains("map:create"));
        app.Tick(1f, 1f);
        Check(!Boundary.Events.Any(x => x.EndsWith(":tick")));
        panel.SetResult(new UI.UIHandle { IsValid = true, View = new WorldMainPanel() });
        await start;
        Check(Boundary.Events.IndexOf("ui:refresh") < Boundary.Events.IndexOf("input:World"));
        Check(StorageManager.Instance.SessionReady && app.State == GameApplicationState.Running);
        await app.StopAsync();
    });

    private static Task CubeUiCancellation() => InSession(async () =>
    {
        string original = JsonConvert.SerializeObject(new SaveData { Player = new SavePlayerData { MapId = 1001, X = 7f } });
        File.WriteAllText(StorageManager.Instance.SavePath, original);
        var panel = new TaskCompletionSource<UI.UIHandle>();
        Boundary.PanelTask = panel.Task;
        var app = Cube();
        Task start = app.StartAsync();
        Task stop = app.StopAsync();
        Check(!stop.IsCompleted && !StorageManager.Instance.Save());
        panel.SetResult(new UI.UIHandle { IsValid = true, View = new WorldMainPanel() });
        await Throws<OperationCanceledException>(() => start);
        await stop;
        Check(File.ReadAllText(StorageManager.Instance.SavePath) == original);
        Check(Boundary.Events.Count(x => x == "input:release") == 1 && Boundary.Events.Contains("map:clear"));
        Check(!Boundary.Events.Contains("input:World"));
    });

    private static Task CubeCameraFailure() => InSession(async () =>
    {
        Boundary.CameraSucceeds = false;
        var app = Cube();
        await Throws<InvalidOperationException>(() => app.StartAsync());
        Check(app.State == GameApplicationState.Failed && Boundary.Events.Contains("input:release"));
        Check(!Boundary.Events.Contains("DataManager:init"));
    });

    private static Task CubeUiFailure() => InSession(async () =>
    {
        Boundary.PanelTask = Task.FromResult(new UI.UIHandle());
        var app = Cube();
        await Throws<InvalidOperationException>(() => app.StartAsync());
        Check(app.State == GameApplicationState.Failed && !StorageManager.Instance.SessionReady);
        Check(!File.Exists(StorageManager.Instance.SavePath) && Boundary.Events.Contains("map:clear"));
    });

    private static Task CubeMapFailure() => InSession(async () =>
    {
        Boundary.MapSucceeds = false;
        var app = Cube();
        await Throws<InvalidOperationException>(() => app.StartAsync());
        Check(!Boundary.Events.Contains("ui:show") && !File.Exists(StorageManager.Instance.SavePath));
    });

    private static Task CubeMapConfigFailure() => InSession(async () =>
    {
        Boundary.MapConfigSucceeds = false;
        var app = Cube();
        await Throws<InvalidOperationException>(() => app.StartAsync());
        Check(!Boundary.Events.Contains("map:create") && app.State == GameApplicationState.Failed);
    });

    private static Task CorruptSave() => ProtectedSave("{broken");
    private static Task IncompatibleSave() => ProtectedSave(JsonConvert.SerializeObject(new SaveData { Version = -1 }));

    private static Task ProtectedSave(string original) => InSession(async () =>
    {
        File.WriteAllText(StorageManager.Instance.SavePath, original);
        var app = Cube();
        await Throws<InvalidOperationException>(() => app.StartAsync());
        await app.StopAsync();
        StorageManager.Instance.MarkDirty();
        UnityEngine.Time.unscaledTime += 10f;
        StorageManager.Instance.Update();
        Check(!StorageManager.Instance.Save() && File.ReadAllText(StorageManager.Instance.SavePath) == original);
        Check(app.State == GameApplicationState.Failed);
    });

    private static Task PartialRestoreFailure() => InSession(async () =>
    {
        string original = JsonConvert.SerializeObject(new SaveData());
        File.WriteAllText(StorageManager.Instance.SavePath, original);
        Boundary.ThrowOnRestore = nameof(TechManager);
        var app = Cube();
        await Throws<InvalidOperationException>(() => app.StartAsync());
        Check(!StorageManager.Instance.Save() && File.ReadAllText(StorageManager.Instance.SavePath) == original);
        Check(!Boundary.Events.Contains("map:create"));
    });

    private static Task SessionSaveGate() => InSession(async () =>
    {
        StorageManager storage = StorageManager.Instance;
        Check(!storage.Save());
        storage.Initialize();
        await Throws<InvalidOperationException>(() => { storage.BeginSession(); return Task.CompletedTask; });
        Check(storage.LoadSession() == SaveLoadResult.NotFound && !storage.Save());
        storage.MarkDirty();
        UnityEngine.Time.unscaledTime += 10f;
        storage.Update();
        Check(!File.Exists(storage.SavePath));
        storage.BeginSession();
        Check(storage.Save());
        string original = File.ReadAllText(storage.SavePath);
        storage.EndSession();
        Check(!storage.Save() && original == File.ReadAllText(storage.SavePath));
    });

    private static Task PlayerFallback() => InSession(async () =>
    {
        File.WriteAllText(StorageManager.Instance.SavePath, JsonConvert.SerializeObject(new SaveData
        {
            Player = new SavePlayerData { MapId = 1001, X = 7f, RotationY = 42f }
        }));
        var app = Cube();
        await app.StartAsync();
        Check(GameplayController.Instance != null && GameplayController.Instance.Player == null);
        await app.StopAsync();
        SaveData saved = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(StorageManager.Instance.SavePath));
        Check(saved.Player != null && saved.Player.X == 7f && saved.Player.RotationY == 42f);
    });

    private static Task CubeCleanupFailure() => InSession(async () =>
    {
        var app = Cube();
        await app.StartAsync();
        Boundary.ThrowOnRelease = nameof(BagManager);
        await app.StopAsync();
        await app.StopAsync();
        Check(Boundary.Events.Contains("StoryManager:release") && Boundary.Events.Count(x => x == "input:release") == 1);
        Check(app.State == GameApplicationState.Failed && !StorageManager.Instance.SessionReady);
    });

    private static Task UnityEntryOwnership() => InSession(() =>
    {
        typeof(GameEntry).GetMethod("ResetApplicationOwner", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var first = new GameEntry();
        Invoke(first, "Awake");
        Invoke(first, "Start");
        Check(first.State == GameApplicationState.Running);
        var duplicate = new GameEntry();
        Invoke(duplicate, "Awake");
        Check(!duplicate.enabled);
        Invoke(duplicate, "OnDestroy");
        Check(first.State == GameApplicationState.Running);
        Invoke(first, "OnApplicationQuit");
        Invoke(first, "OnDestroy");
        Check(first.State == GameApplicationState.Stopped && Boundary.Events.Count(x => x == "input:release") == 1);
        return Task.CompletedTask;
    });

    private static Task FinalSaveFailure() => InSession(async () =>
    {
        var app = Cube();
        await app.StartAsync();
        Directory.CreateDirectory(StorageManager.Instance.SavePath + ".tmp");
        await app.StopAsync();
        Check(app.State == GameApplicationState.Failed && !StorageManager.Instance.SessionReady);
        Check(Boundary.Events.Contains("input:release") && Boundary.Events.Contains("map:clear"));
    });

    private static GameApplication Cube() => new GameApplication(new CubeGameLifecycle(1001));
    private static TaskCompletionSource<bool> Gate() => new TaskCompletionSource<bool>();
    private static void Check(bool condition) { if (!condition) { throw new Exception("Assertion failed."); } }
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static async Task<Exception> Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static async Task InSession(Func<Task> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "cube-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        UnityEngine.Application.persistentDataPath = directory;
        Boundary.Reset();
        try { await action(); }
        finally
        {
            StorageManager.Instance.EndSession();
            Directory.Delete(directory, true);
        }
    }

    private sealed class FakeLifecycle : IGameLifecycle
    {
        public Func<CancellationToken, Task> Initialize = token => Task.CompletedTask;
        public Action Frame;
        public Action<GameShutdownReason> Release;
        public int Starts;
        public int Ticks;
        public int Shutdowns;
        public float Delta;
        public float UnscaledDelta;
        public GameShutdownReason Reason;

        public Task InitializeAsync(CancellationToken token) { Starts++; return Initialize(token); }
        public void Tick(float deltaTime, float unscaledDeltaTime) { Ticks++; Delta = deltaTime; UnscaledDelta = unscaledDeltaTime; Frame?.Invoke(); }
        public void Shutdown(GameShutdownReason reason) { Shutdowns++; Reason = reason; Release?.Invoke(reason); }
    }
}
