using System.Threading;
using System.Threading.Tasks;

namespace Game.Bootstrap
{
    /// <summary>Project composition. All callbacks run on the caller's synchronization context.</summary>
    public interface IGameLifecycle
    {
        Task InitializeAsync(CancellationToken cancellationToken);
        void Tick(float deltaTime, float unscaledDeltaTime);

        /// <summary>Release everything acquired so far, including a partially initialized project.</summary>
        void Shutdown(GameShutdownReason reason);
    }
}
