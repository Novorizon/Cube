using System;
using System.Threading.Tasks;

namespace UI
{
    public sealed class OverlayManager
    {
        readonly UIInstanceFactory factory;

        UIHandle blockingOverlay;
        int blockingCount;

        public OverlayManager(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public bool HasBlockingOverlay => blockingCount > 0 && blockingOverlay.IsValid;

        public async Task<IDisposable> ShowBlockingAsync(string prefabPath, object args = null)
        {
            blockingCount++;

            if (blockingCount == 1 || !blockingOverlay.IsValid)
            {
                blockingOverlay = await factory.OpenAsync(UIKind.Overlay, UILayer.Overlay, prefabPath, args, false, true, null);
            }
            else if (blockingOverlay.View != null)
            {
                blockingOverlay.View.InternalOnOpen(args);
            }

            return new Token(this);
        }

        public void ForceHideBlocking(bool destroy = false)
        {
            blockingCount = 0;
            if (blockingOverlay.IsValid)
            {
                factory.Close(blockingOverlay, destroy, !destroy);
                blockingOverlay = default;
            }
        }

        void ReleaseBlocking()
        {
            if (blockingCount <= 0)
            {
                return;
            }

            blockingCount--;
            if (blockingCount == 0 && blockingOverlay.IsValid)
            {
                factory.Close(blockingOverlay, false, true);
                blockingOverlay = default;
            }
        }

        sealed class Token : IDisposable
        {
            readonly OverlayManager owner;
            bool disposed;

            public Token(OverlayManager owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.ReleaseBlocking();
            }
        }
    }
}
